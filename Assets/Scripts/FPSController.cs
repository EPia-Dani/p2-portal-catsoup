using UnityEngine;

/// <summary>
/// Industry-standard FPS controller with frame-rate independent camera DPI.
/// Uses proper mouse delta accumulation and sensitivity scaling following Quake/Source engine conventions.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform yawTransform;
    [SerializeField] private Transform pitchTransform;
    [SerializeField] private float mouseSensitivityYaw = 1.0f;
    [SerializeField] private float mouseSensitivityPitch = 1.0f;
    [SerializeField] private float pitchClampMin = -89f;
    [SerializeField] private float pitchClampMax = 89f;

    [Header("Movement Settings")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float maxSpeedOnGround = 4f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float timeToApex = 0.45f;
    [SerializeField] private float terminalVelocity = -53f;
    [SerializeField] private float groundAcceleration = 40f;
    [SerializeField] private float airAcceleration = 10f;
    [SerializeField] private float groundFriction = 8f;

    private Input.PlayerInput _controls;

    // Movement state
    private float _gravity;
    private float _jumpSpeed;
    private float _verticalSpeed;
    private bool _isGrounded;
    private Vector2 _moveInput;
    private Vector3 _horizontalVelocity;
    
    // Cached Vector3 to avoid per-frame allocations
    private Vector3 _cachedHorizontal;
    private Vector3 _cachedMove;
    private Vector3 _cachedWishDir;
    private Vector3 _cachedTargetVel;
    private Vector3 _cachedDelta;
    private Vector3 _cachedVerticalVel;
    
    // Cached transform directions to avoid property access overhead
    private Transform _cachedTransform;
    private Vector3 _cachedForward;
    private Vector3 _cachedRight;
    
    // Cached Quaternion for rotations
    private Quaternion _cachedYawRotation;
    private Quaternion _cachedPitchRotation;

    // Look state - using Euler angles for simplicity (quaternion would be more robust for extreme angles)
    private float _currentYaw;
    private float _currentPitch;
    
    // Mouse delta accumulation for frame-rate independence
    private Vector2 _accumulatedMouseDelta;

    private void Awake() {
        if (!characterController) characterController = GetComponent<CharacterController>();

        // Cache transform reference
        _cachedTransform = transform;

        // Gravity and jump precompute
        _gravity = -2f * jumpHeight / (timeToApex * timeToApex);
        _jumpSpeed = 2f * jumpHeight / timeToApex;

        // Get the shared input instance
        _controls = InputManager.PlayerInput;

        // Sync initial yaw/pitch from current transforms
        if (yawTransform != null) {
            _currentYaw = yawTransform.eulerAngles.y;
        }
        if (pitchTransform != null) {
            float rawX = pitchTransform.localEulerAngles.x;
            _currentPitch = Mathf.DeltaAngle(0f, rawX);
        }

        _accumulatedMouseDelta = Vector2.zero;
    }

    private void Update() {
        float dt = Time.deltaTime;

        // Input
        ReadInput(out Vector2 moveInput, out Vector2 lookInput, out bool jumpPressed);

        // Look - frame-rate independent mouse handling
        UpdateLookFrameIndependent(lookInput, dt);

        // Vertical motion
        float dy = UpdateVerticalMotion(jumpPressed, dt);

        // Horizontal motion
        UpdateHorizontalMotion(moveInput, dt);

        // Move and resolve collisions
        MoveAndCollide(dy, dt);
    }

    private void ReadInput(out Vector2 moveInput, out Vector2 lookInput, out bool jumpPressed) {
        moveInput = _controls.Player.Move.ReadValue<Vector2>();
        lookInput = _controls.Player.Look.ReadValue<Vector2>();
        jumpPressed = _controls.Player.Jump.WasPerformedThisFrame();
        _moveInput = moveInput;
    }

    /// <summary>
    /// Frame-rate independent camera look handler.
    /// This is the key to fixing the DPI issue - we process raw input without time scaling.
    /// </summary>
    private void UpdateLookFrameIndependent(Vector2 lookInput, float dt) {
        // Accumulate raw mouse delta (not scaled by frame rate)
        _accumulatedMouseDelta += lookInput;

        // Apply accumulated delta to rotation
        // Mouse movement is in pixels, we convert to degrees using sensitivity
        // This approach ensures the same mouse movement = same rotation regardless of framerate
        float yawDelta = _accumulatedMouseDelta.x * mouseSensitivityYaw;
        float pitchDelta = -_accumulatedMouseDelta.y * mouseSensitivityPitch; // Negative because down is positive in screen space

        _currentYaw += yawDelta;
        _currentPitch += pitchDelta;
        _currentPitch = Mathf.Clamp(_currentPitch, pitchClampMin, pitchClampMax);

        // Apply rotations (recreate Quaternions - they're necessary for correct rotation, but we cache to avoid repeated property access)
        if (yawTransform != null) {
            _cachedYawRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            yawTransform.rotation = _cachedYawRotation;
        }
        if (pitchTransform != null) {
            _cachedPitchRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
            pitchTransform.localRotation = _cachedPitchRotation;
        }

        // Clear accumulated delta for next frame
        _accumulatedMouseDelta = Vector2.zero;
    }

    private float UpdateVerticalMotion(bool jumpPressed, float dt) {
        float acceleration = 0f;
        if (_isGrounded) {
            if (_verticalSpeed < 0f) _verticalSpeed = -2.0f;
        } else {
            acceleration = _gravity;
        }
        float dy = _verticalSpeed * dt + 0.5f * acceleration * dt * dt;
        _verticalSpeed += acceleration * dt;
        if (_verticalSpeed < terminalVelocity) _verticalSpeed = terminalVelocity;
        if (jumpPressed && _isGrounded) { _verticalSpeed = _jumpSpeed; _isGrounded = false; }
        return dy;
    }

    private void UpdateHorizontalMotion(Vector2 moveInput, float dt) {
        // Cache transform directions to avoid property access overhead
        _cachedForward = _cachedTransform.forward;
        _cachedRight = _cachedTransform.right;
        
        // Calculate wish direction using cached vectors
        _cachedWishDir.x = _cachedForward.x * moveInput.y + _cachedRight.x * moveInput.x;
        _cachedWishDir.y = 0f;
        _cachedWishDir.z = _cachedForward.z * moveInput.y + _cachedRight.z * moveInput.x;
        
        float wishDirSqrMag = _cachedWishDir.x * _cachedWishDir.x + _cachedWishDir.z * _cachedWishDir.z;
        if (wishDirSqrMag > 1e-4f)
        {
            float invMag = 1f / Mathf.Sqrt(wishDirSqrMag);
            _cachedWishDir.x *= invMag;
            _cachedWishDir.z *= invMag;
        }
        
        float wishSpeed = maxSpeedOnGround;
        float accel = _isGrounded ? groundAcceleration : airAcceleration;
        
        // Calculate target velocity using cached vector
        _cachedTargetVel.x = _cachedWishDir.x * wishSpeed;
        _cachedTargetVel.y = 0f;
        _cachedTargetVel.z = _cachedWishDir.z * wishSpeed;
        
        // Reuse cached Vector3 instead of allocating new one
        _cachedHorizontal.x = _horizontalVelocity.x;
        _cachedHorizontal.y = 0f;
        _cachedHorizontal.z = _horizontalVelocity.z;
        
        // Calculate delta using cached vectors
        _cachedDelta.x = _cachedTargetVel.x - _cachedHorizontal.x;
        _cachedDelta.y = 0f;
        _cachedDelta.z = _cachedTargetVel.z - _cachedHorizontal.z;
        
        float maxDelta = accel * dt;
        float deltaSqrMag = _cachedDelta.x * _cachedDelta.x + _cachedDelta.z * _cachedDelta.z;
        if (deltaSqrMag > maxDelta * maxDelta)
        {
            float invMag = maxDelta / Mathf.Sqrt(deltaSqrMag);
            _cachedDelta.x *= invMag;
            _cachedDelta.z *= invMag;
        }
        _cachedHorizontal.x += _cachedDelta.x;
        _cachedHorizontal.z += _cachedDelta.z;
        
        if (_isGrounded) {
            float originalMag = Mathf.Sqrt(_cachedHorizontal.x * _cachedHorizontal.x + _cachedHorizontal.z * _cachedHorizontal.z);
            float mag = Mathf.Max(0f, originalMag - groundFriction * dt);
            if (mag > 1e-4f && originalMag > 1e-4f)
            {
                // Scale by ratio of new magnitude to original
                float scale = mag / originalMag;
                _cachedHorizontal.x *= scale;
                _cachedHorizontal.z *= scale;
            }
            else
            {
                _cachedHorizontal.x = 0f;
                _cachedHorizontal.z = 0f;
            }
        }
        
        // Update horizontal velocity from cached value
        _horizontalVelocity.x = _cachedHorizontal.x;
        _horizontalVelocity.y = 0f;
        _horizontalVelocity.z = _cachedHorizontal.z;
    }

    private void MoveAndCollide(float dy, float dt) {
        // Use cached Vector3 instead of allocating new one
        _cachedMove.x = _horizontalVelocity.x * dt;
        _cachedMove.y = dy;
        _cachedMove.z = _horizontalVelocity.z * dt;
        
        if (characterController == null || !characterController.enabled) return;
        CollisionFlags flags = characterController.Move(_cachedMove);
        _isGrounded = (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;
    }

    /// <summary>
    /// Called by PortalTraveller to transform velocity when passing through a portal
    /// </summary>
    public void TransformVelocity(Matrix4x4 transformMatrix)
    {
        // Transform horizontal velocity
        _horizontalVelocity = transformMatrix.MultiplyVector(_horizontalVelocity);
        
        // Transform vertical velocity using cached vector
        _cachedVerticalVel.x = 0f;
        _cachedVerticalVel.y = _verticalSpeed;
        _cachedVerticalVel.z = 0f;
        _cachedVerticalVel = transformMatrix.MultiplyVector(_cachedVerticalVel);
        _verticalSpeed = _cachedVerticalVel.y;
        
        // Update internal look angles to match the new rotation
        if (yawTransform != null)
        {
            _currentYaw = yawTransform.eulerAngles.y;
        }
        else
        {
            _currentYaw = transform.eulerAngles.y;
        }
        
        if (pitchTransform != null)
        {
            float rawX = pitchTransform.localEulerAngles.x;
            _currentPitch = Mathf.DeltaAngle(0f, rawX);
        }
    }

    /// <summary>
    /// Called by PortalTraveller to transform rotation through a portal
    /// </summary>
    public void TransformRotation(Matrix4x4 transformMatrix)
    {
        // Extract rotation matrix (remove translation)
        Matrix4x4 rotMatrix = transformMatrix;
        rotMatrix.m03 = 0f;
        rotMatrix.m13 = 0f;
        rotMatrix.m23 = 0f;
        rotMatrix.m33 = 1f;
        
        // Transform yaw transform if it exists
        if (yawTransform != null)
        {
            // Get current forward and up in world space
            Vector3 yawForward = yawTransform.forward;
            Vector3 yawUp = yawTransform.up;
            
            // Transform through portal matrix (this handles mirroring correctly)
            Vector3 transformedForward = rotMatrix.MultiplyVector(yawForward);
            Vector3 transformedUp = rotMatrix.MultiplyVector(yawUp);
            transformedForward.Normalize();
            transformedUp.Normalize();
            
            // Apply transformed rotation
            yawTransform.rotation = Quaternion.LookRotation(transformedForward, transformedUp);
            _currentYaw = yawTransform.eulerAngles.y;
        }
        
        // Transform pitch transform if it exists
        if (pitchTransform != null)
        {
            // Get current forward and up in world space
            Vector3 pitchForward = pitchTransform.forward;
            Vector3 pitchUp = pitchTransform.up;
            
            // Transform through portal matrix
            Vector3 transformedForward = rotMatrix.MultiplyVector(pitchForward);
            Vector3 transformedUp = rotMatrix.MultiplyVector(pitchUp);
            transformedForward.Normalize();
            transformedUp.Normalize();
            
            // Get world rotation
            Quaternion worldRot = Quaternion.LookRotation(transformedForward, transformedUp);
            
            // Convert to local space relative to yaw transform (or base transform)
            Transform parentRot = yawTransform != null ? yawTransform : transform;
            Quaternion localRot = Quaternion.Inverse(parentRot.rotation) * worldRot;
            pitchTransform.localRotation = localRot;
            
            // Update internal pitch angle
            float rawX = pitchTransform.localEulerAngles.x;
            _currentPitch = Mathf.DeltaAngle(0f, rawX);
            _currentPitch = Mathf.Clamp(_currentPitch, pitchClampMin, pitchClampMax);
        }
        
        // Clear accumulated mouse delta to prevent jump
        _accumulatedMouseDelta = Vector2.zero;
    }
}
