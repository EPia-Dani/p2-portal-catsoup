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

    // External horizontal momentum applied after teleport (preserved across frames)
    Vector3 externalVelocity = Vector3.zero;
    public float portalMomentumDamping = 2f; // higher = faster decay

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
        // Compute player's input-based horizontal velocity
        Vector3 inputVel = new Vector3(worldInputDir.x * currentSpeed, 0, worldInputDir.z * currentSpeed);

        // Combine input velocity with any external (portal) momentum
        Vector3 horizontal = inputVel + externalVelocity;

        velocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);

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

        // Decay external velocity over time so momentum fades
        externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, portalMomentumDamping * Time.deltaTime);
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
        // Rotate the entire velocity vector from the source portal's orientation to the destination's.
        // We include a 180° flip around the portal's local up so 'entering' becomes 'exiting'.
        Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
        Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);

        // Rotate the captured world-space velocity into the destination orientation
        velocity = relativeRotation * currentVelocity;

        // Update verticalVelocity to match the new world-space Y component
        verticalVelocity = velocity.y;

        // Determine if the source portal is 'non-vertical' (e.g. on the floor/ceiling).
        // We treat portals whose forward/normal has a significant Y component as non-vertical.
        float portalUpDot = Mathf.Abs(Vector3.Dot(fromPortal.forward.normalized, Vector3.up));
        const float nonVerticalThreshold = 0.5f; // tweakable: >0.5 means noticeably tilted towards horizontal plane

        if (portalUpDot > nonVerticalThreshold) {
            // Preserve horizontal components as external momentum so Update doesn't overwrite them
            externalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        } else {
            // For vertical portals (walls), don't inject external momentum so movement stays fluid
            externalVelocity = Vector3.zero;
        }

        // Sync physics to prevent collision detection issues
        Physics.SyncTransforms();
    }

}