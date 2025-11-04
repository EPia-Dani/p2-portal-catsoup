using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSController : PortalTraveller {

    public float walkSpeed = 3;
    public float jumpForce = 8;
    public float gravity = 18;

    public bool lockCursor;
    public float mouseSensitivity = 10;
    public Vector2 pitchMinMax = new Vector2 (-40, 85);

    CharacterController controller;
    Camera cam;
    public float yaw;
    public float pitch;

    float verticalVelocity;
    Vector3 velocity;

    bool jumping;
    float lastGroundedTime;
    bool disabled;
    
    
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
            Debug.Log("Added kinematic Rigidbody for portal trigger detection");
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

        Vector3 inputDir = new Vector3 (moveInput.x, 0, moveInput.y).normalized;
        Vector3 worldInputDir = transform.TransformDirection (inputDir);

        float currentSpeed = walkSpeed;
        velocity = new Vector3(worldInputDir.x * currentSpeed, verticalVelocity, worldInputDir.z * currentSpeed);

        verticalVelocity -= gravity * Time.deltaTime;
        velocity = new Vector3 (velocity.x, verticalVelocity, velocity.z);

        var flags = controller.Move (velocity * Time.deltaTime);
        if (flags == CollisionFlags.Below) {
            jumping = false;
            lastGroundedTime = Time.time;
            verticalVelocity = 0;
        }

        if (jumpPressed) {
            float timeSinceLastTouchedGround = Time.time - lastGroundedTime;
            if (controller.isGrounded || (!jumping && timeSinceLastTouchedGround < 0.15f)) {
                jumping = true;
                verticalVelocity = jumpForce;
            }
        }

        // Accumulate mouse input (frame-rate independent)
        float mX = lookInput.x * mouseSensitivity;
        float mY = lookInput.y * mouseSensitivity;

        yaw += mX;
        pitch -= mY;
        pitch = Mathf.Clamp (pitch, pitchMinMax.x, pitchMinMax.y);

        transform.eulerAngles = Vector3.up * yaw;
        cam.transform.localEulerAngles = Vector3.right * pitch;

    }

    public override void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
        // Capture velocity BEFORE position change
        Vector3 currentVelocity = velocity;
        
        // Teleport the player position
        transform.position = pos;
        
        // Rotate the player's yaw based on portal orientation change
        Vector3 eulerRot = rot.eulerAngles;
        float delta = Mathf.DeltaAngle(yaw, eulerRot.y);
        yaw += delta;
        transform.eulerAngles = Vector3.up * yaw;
        
        // ===== UNIVERSAL VELOCITY TRANSFORMATION =====
        // Convert velocity to source portal's local space
        // In portal local space: Z = through portal, X = right (lateral), Y = up
        Vector3 localVel = fromPortal.InverseTransformVector(currentVelocity);
        
        // Decompose velocity into portal-relative components
        float throughPortal = localVel.z;      // Velocity going through portal
        float lateral = localVel.x;            // Lateral velocity (left/right)
        float verticalInPortalSpace = localVel.y;  // Vertical in portal's local space
        
        // Mirror the "through portal" component (reverse it)
        throughPortal = -throughPortal;
        
        // Detect portal orientations (vertical = walls, horizontal = floors/ceilings)
        Vector3 fromNormal = fromPortal.forward;
        Vector3 toNormal = toPortal.forward;
        bool fromIsVertical = Mathf.Abs(Vector3.Dot(fromNormal, Vector3.up)) < 0.707f; // < 45° from vertical
        bool toIsVertical = Mathf.Abs(Vector3.Dot(toNormal, Vector3.up)) < 0.707f;
        
        // Transform velocity components based on portal orientation
        Vector3 newLocalVel;
        
        if (!fromIsVertical && toIsVertical) {
            // Floor/Ceiling → Wall: vertical becomes forward, forward becomes vertical
            newLocalVel = new Vector3(lateral, -throughPortal, verticalInPortalSpace);
        }
        else if (fromIsVertical && !toIsVertical) {
            // Wall → Floor/Ceiling: forward becomes vertical, vertical becomes forward
            newLocalVel = new Vector3(lateral, throughPortal, verticalInPortalSpace);
        }
        else {
            // Same orientation (Wall→Wall or Floor→Floor): standard mirror
            newLocalVel = new Vector3(lateral, verticalInPortalSpace, throughPortal);
        }
        
        // Transform back to world space using destination portal
        velocity = toPortal.TransformVector(newLocalVel);
        
        // Update verticalVelocity to match the new velocity's Y component
        verticalVelocity = velocity.y;
        
        // Sync physics to prevent collision detection issues
        Physics.SyncTransforms();
    }

}