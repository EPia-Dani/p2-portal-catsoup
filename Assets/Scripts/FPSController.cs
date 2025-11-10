using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSController : PortalTraveller {

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintMultiplier = 1.5f; // Speed multiplier when sprinting
    public float groundAcceleration = 50f; // How quickly you reach max speed on ground (higher = more responsive)
    public float groundFriction = 30f; // How quickly you stop on ground (higher = less sliding)
    
    [Header("Air Movement")]
    public float jumpForce = 8f;
    public float gravity = 18f;
    [Range(0f, 1f)]
    public float airControl = 0.5f; // 0 = no control, 1 = full control. Controls how much you can accelerate in air
    public float airAcceleration = 15f; // How quickly you can change direction in air
    public float airDrag = 0.1f; // Air resistance (0 = no drag, 1 = full drag)
    public float terminalVelocity = 50f; // Maximum velocity magnitude to prevent physics breaking

    public bool lockCursor;
    public float mouseSensitivity = 10;
    public Vector2 pitchMinMax = new Vector2 (-40, 85);

    CharacterController controller;
    Camera cam;
    public float yaw;
    public float pitch;

    float verticalVelocity;
    Vector3 velocity;
    
    // Public property to access current velocity (for objects to inherit momentum when dropped)
    public Vector3 CurrentVelocity => velocity;

    // External horizontal momentum applied after teleport (preserved across frames)
    Vector3 externalVelocity = Vector3.zero;
    public float portalMomentumDamping = 2f; // higher = faster decay

    bool jumping;
    float lastGroundedTime;
    bool disabled;
    bool cameraRotationEnabled = true;
    
    /// <summary>
    /// Disables player control (e.g., when dead or in menu).
    /// </summary>
    public void SetDisabled(bool value)
    {
        disabled = value;
        
        // Unlock cursor when disabled
        if (disabled)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    /// <summary>
    /// Checks if player control is currently disabled.
    /// </summary>
    public bool IsDisabled => disabled;
    
    /// <summary>
    /// Enables or disables camera rotation (pitch/yaw).
    /// </summary>
    public void SetCameraRotationEnabled(bool enabled)
    {
        cameraRotationEnabled = enabled;
    }
    
    // Store horizontal velocity when jumping to preserve momentum
    Vector3 airHorizontalVelocity = Vector3.zero;
    
    
    private Input.PlayerInput _controls;

    void Start () {
        cam = Camera.main;
        if (lockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        controller = GetComponent<CharacterController> ();

        yaw = transform.eulerAngles.y;
        pitch = cam.transform.localEulerAngles.x;

        // Initialize input controls if not already done
        if (_controls == null) {
            _controls = InputManager.PlayerInput;
        }

        // Ensure we have a Rigidbody (kinematic) for reliable trigger detection
        // CharacterController + Rigidbody (kinematic) = reliable OnTriggerEnter
        Rigidbody rb = GetComponent<Rigidbody>();
        if (!rb) {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Update () {
        

        if (disabled) {
            return;
        }

        // Use new input system
        Vector2 moveInput = _controls.Player.Move.ReadValue<Vector2>();
        Vector2 lookInput = _controls.Player.Look.ReadValue<Vector2>();
        bool jumpPressed = _controls.Player.Jump.WasPerformedThisFrame();
        bool isSprinting = _controls.Player.Sprint.IsPressed();

        // Get input direction in world space
        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        Vector3 worldInputDir = transform.TransformDirection(inputDir);

        // Check if player is grounded (with small buffer for edge cases)
        bool isGrounded = controller.isGrounded || (Time.time - lastGroundedTime < 0.1f);
        
        // Apply sprint multiplier when sprinting
        float speedMultiplier = isSprinting ? sprintMultiplier : 1f;
        float targetSpeed = walkSpeed * speedMultiplier;
        
        // Get current horizontal velocity
        Vector3 currentHorizontal = isGrounded ? new Vector3(velocity.x, 0, velocity.z) : airHorizontalVelocity;
        
        // Calculate desired velocity based on input
        Vector3 desiredVelocity = Vector3.zero;
        if (moveInput.sqrMagnitude > 0.01f) {
            desiredVelocity = worldInputDir * targetSpeed;
        }
        
        // Handle external velocity damping (from portals)
        bool isActivelyMoving = moveInput.sqrMagnitude > 0.01f;
        if (isActivelyMoving && externalVelocity.sqrMagnitude > 0.01f) {
            // When player is moving, damp external velocity that opposes input
            float inputVelMagnitude = desiredVelocity.magnitude;
            if (inputVelMagnitude > 0.01f) {
                Vector3 inputDirNormalized = desiredVelocity / inputVelMagnitude;
                float alignment = Vector3.Dot(externalVelocity.normalized, inputDirNormalized);
                
                float dampingFactor = alignment > 0.3f ? portalMomentumDamping * 0.3f : portalMomentumDamping * 1.5f;
                
                Vector3 parallelComponent = Vector3.Project(externalVelocity, inputDirNormalized);
                Vector3 perpendicularComponent = externalVelocity - parallelComponent;
                
                perpendicularComponent = Vector3.Lerp(perpendicularComponent, Vector3.zero, portalMomentumDamping * Time.deltaTime);
                parallelComponent = Vector3.Lerp(parallelComponent, Vector3.zero, dampingFactor * Time.deltaTime);
                
                externalVelocity = parallelComponent + perpendicularComponent;
            } else {
                externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, portalMomentumDamping * Time.deltaTime);
            }
        } else {
            externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, portalMomentumDamping * Time.deltaTime);
        }

        Vector3 horizontal;
        
        if (isGrounded) {
            // Grounded movement: responsive acceleration with strong friction
            if (moveInput.sqrMagnitude > 0.01f) {
                // Accelerate towards desired velocity quickly for responsive feel
                Vector3 velocityDiff = desiredVelocity - currentHorizontal;
                float acceleration = groundAcceleration * Time.deltaTime;
                horizontal = Vector3.MoveTowards(currentHorizontal, desiredVelocity, acceleration);
            } else {
                // Strong friction when not moving - stop quickly
                float friction = groundFriction * Time.deltaTime;
                horizontal = Vector3.MoveTowards(currentHorizontal, Vector3.zero, friction);
                
                // If velocity is very small, snap to zero to prevent micro-sliding
                if (horizontal.magnitude < 0.1f) {
                    horizontal = Vector3.zero;
                }
            }
            
            // Add external velocity (from portals) - but apply friction to it too
            if (externalVelocity.sqrMagnitude > 0.01f) {
                // Damp external velocity more aggressively when grounded
                externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, groundFriction * Time.deltaTime);
                if (externalVelocity.magnitude < 0.1f) {
                    externalVelocity = Vector3.zero;
                }
            }
            
            horizontal += externalVelocity;
            
            // Reset air velocity when grounded
            airHorizontalVelocity = horizontal;
        } else {
            // Air movement: Source-engine style air control
            // You can accelerate in any direction, but speed is limited
            Vector3 currentVel = airHorizontalVelocity;
            
            if (moveInput.sqrMagnitude > 0.01f) {
                // Calculate acceleration in desired direction
                Vector3 desiredDir = desiredVelocity.normalized;
                float accel = airAcceleration * airControl * Time.deltaTime;
                
                // Accelerate in the desired direction
                Vector3 accelVector = desiredDir * accel;
                horizontal = currentVel + accelVector;
                
                // Limit horizontal speed to prevent infinite acceleration
                // Allow slightly higher speed than ground speed for air strafing
                float maxAirSpeed = targetSpeed * 1.2f;
                if (horizontal.magnitude > maxAirSpeed) {
                    horizontal = horizontal.normalized * maxAirSpeed;
                }
            } else {
                // No input, just apply air drag
                horizontal = currentVel;
            }
            
            // Apply air drag (gradual slowdown)
            horizontal *= (1f - airDrag * Time.deltaTime);
            
            // Add external velocity (from portals)
            horizontal += externalVelocity;
            
            // Store for next frame
            airHorizontalVelocity = horizontal;
        }

        velocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);

        verticalVelocity -= gravity * Time.deltaTime;
        velocity = new Vector3 (velocity.x, verticalVelocity, velocity.z);

        // Clamp velocity to terminal velocity to prevent physics breaking
        if (velocity.magnitude > terminalVelocity) {
            velocity = velocity.normalized * terminalVelocity;
            verticalVelocity = velocity.y;
        }

        var flags = controller.Move (velocity * Time.deltaTime);
        if (flags == CollisionFlags.Below) {
            jumping = false;
            lastGroundedTime = Time.time;
            verticalVelocity = 0;
            // Reset air velocity when landing (will be set from ground movement next frame)
            airHorizontalVelocity = horizontal;
        }

        if (jumpPressed) {
            float timeSinceLastTouchedGround = Time.time - lastGroundedTime;
            if (controller.isGrounded || (!jumping && timeSinceLastTouchedGround < 0.15f)) {
                jumping = true;
                verticalVelocity = jumpForce;
                // Capture horizontal velocity at moment of jump (use the calculated horizontal, not velocity)
                airHorizontalVelocity = horizontal;
            }
        }

        // Accumulate mouse input (frame-rate independent)
        // Only apply rotation if camera rotation is enabled
        if (cameraRotationEnabled)
        {
            float mX = lookInput.x * mouseSensitivity;
            float mY = lookInput.y * mouseSensitivity;

            yaw += mX;
            pitch -= mY;
            pitch = Mathf.Clamp (pitch, pitchMinMax.x, pitchMinMax.y);

            transform.eulerAngles = Vector3.up * yaw;
            cam.transform.localEulerAngles = Vector3.right * pitch;
        }
    }

    public override void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot, float scaleRatio = 1f) {
        // Capture velocity BEFORE position change
        Vector3 currentVelocity = velocity;
        
        // Scale velocity based on portal size difference
        currentVelocity *= scaleRatio;
        
        // Teleport the player position
        transform.position = pos;
        
        // ===== UNIVERSAL VELOCITY TRANSFORMATION =====
        // Rotate the entire velocity vector from the source portal's orientation to the destination's.
        // We include a 180° flip around the portal's local up so 'entering' becomes 'exiting'.
        Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
        Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);
        
        // Transform the player's forward direction through the portal (same transformation as velocity)
        // This preserves relative orientation: looking left relative to source portal = looking left relative to dest portal
        Vector3 currentForward = transform.forward;
        Vector3 transformedForward = relativeRotation * currentForward;
        
        // Project onto horizontal plane and extract yaw
        Vector3 horizontalForward = new Vector3(transformedForward.x, 0, transformedForward.z);
        if (horizontalForward.sqrMagnitude > 0.01f) {
            horizontalForward.Normalize();
            yaw = Mathf.Atan2(horizontalForward.x, horizontalForward.z) * Mathf.Rad2Deg;
        }
        // If horizontal component is too small, keep current yaw (rare edge case)
        transform.eulerAngles = Vector3.up * yaw;

        // Rotate the captured world-space velocity into the destination orientation
        velocity = relativeRotation * currentVelocity;

        // Apply minimum exit velocity using base class method (handles non-vertical to vertical transitions)
        velocity = ApplyMinimumExitVelocity(fromPortal, toPortal, velocity);

        // Update verticalVelocity to match the new world-space Y component
        verticalVelocity = velocity.y;

        // Determine if the source portal is 'non-vertical' (e.g. on the floor/ceiling).
        // We treat portals whose forward/normal has a significant Y component as non-vertical.
        float fromPortalUpDot = Mathf.Abs(Vector3.Dot(fromPortal.forward.normalized, Vector3.up));
        const float nonVerticalThreshold = 0.5f; // tweakable: >0.5 means noticeably tilted towards horizontal plane
        bool fromPortalIsNonVertical = fromPortalUpDot > nonVerticalThreshold;

        if (fromPortalIsNonVertical) {
            // Preserve horizontal components as external momentum so Update doesn't overwrite them
            externalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            
            // Also update airHorizontalVelocity to ensure momentum is preserved in air
            airHorizontalVelocity = externalVelocity;
        } else {
            // For vertical portals (walls), don't inject external momentum so movement stays fluid
            externalVelocity = Vector3.zero;
        }

        // Call base Teleport to handle scaling
        base.Teleport(fromPortal, toPortal, pos, rot, scaleRatio);

        // Notify PlayerPickup that player teleported (for held object clone swap)
        var playerPickup = GetComponent<PlayerPickup>();
        if (playerPickup != null)
        {
            playerPickup.OnPlayerTeleport();
        }

        // Sync physics to prevent collision detection issues
        Physics.SyncTransforms();
    }

}