using UnityEngine;
using System.Reflection;
using System.Collections;

public class FPSController : PortalTraveller
{

    [Header("Movement")] public float walkSpeed = 5f;
    public float sprintMultiplier = 1.5f; // Speed multiplier when sprinting
    public float groundAcceleration = 50f; // How quickly you reach max speed on ground (higher = more responsive)
    public float groundFriction = 30f; // How quickly you stop on ground (higher = less sliding)

    [Header("Air Movement")] public float jumpForce = 8f;
    public float gravity = 18f;
    public float airControl = 5f; // Simple air control acceleration (units/sec²)
    public float terminalVelocity = 50f; // Maximum velocity magnitude to prevent physics breaking

    public bool lockCursor;
    public float mouseSensitivity = 10f;
    public Vector2 pitchMinMax = new Vector2(-40, 85);

    CharacterController controller;
    Camera cam;
    public float yaw;
    public float pitch;

    float verticalVelocity;
    Vector3 velocity;

    // Public property to access current velocity (for objects to inherit momentum when dropped)
    public Vector3 CurrentVelocity => velocity;

    bool jumping;
    float lastGroundedTime;
    bool disabled;
    bool cameraRotationEnabled = true;

    // Surface physics tracking
    float baseGroundFriction;
    float baseGroundAcceleration;
    SurfacePhysics currentSurfacePhysics;

    // ======= NEW: Health / Damage =======
    [Header("Health")]
    [Tooltip("Maximum hit points for the player. When current HP reaches 0 the player dies.")]
    public int maxHealth = 3;
    [Tooltip("Time in seconds the player is invulnerable after taking a hit.")]
    public float invulnerabilitySeconds = 0.6f;

    // Camera shake settings for hit feedback
    [Tooltip("Duration of the camera vibration when the player is hit.")]
    public float hitVibrationDuration = 0.25f;
    [Tooltip("Magnitude of the camera vibration (local position offset in meters).")]
    public float hitVibrationMagnitude = 0.06f;

    int _currentHealth;
    bool _isInvulnerable = false;
    Coroutine _shakeRoutine;
    
    // Reference to damage overlay UI (set automatically or manually in inspector)
    [SerializeField] private DamageOverlay _damageOverlay;

    // Store horizontal velocity when jumping to preserve momentum
    Vector3 airHorizontalVelocity = Vector3.zero;

    private Input.PlayerInput _controls;

    // Static dummy transforms for respawn teleportation (reused to avoid allocations)
    private static Transform _dummyFromPortal;
    private static Transform _dummyToPortal;

    void Awake()
    {
        // Initialize health early so other scripts can query it on Start
        _currentHealth = maxHealth;
    }

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
    /// Resets player health to maximum (used on respawn/checkpoint).
    /// </summary>
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        _isInvulnerable = false;
        
        if (_damageOverlay != null)
        {
            _damageOverlay.ClearOverlay();
        }
        
        Debug.Log($"Player health reset to {maxHealth}");
    }

    /// <summary>
    /// Enables or disables camera rotation (pitch/yaw).
    /// </summary>
    public void SetCameraRotationEnabled(bool allow)
    {
        cameraRotationEnabled = allow;
    }

    // Try to resolve overlay reference (handles inactive objects too)
    private void ResolveDamageOverlayReference()
    {
        if (_damageOverlay != null) return;

        // Prefer newer API if available
        #if UNITY_2023_1_OR_NEWER
        _damageOverlay = FindFirstObjectByType<DamageOverlay>(FindObjectsInactive.Include);
        #else
        // Fallback: find all (including inactive) and pick one in a valid scene
        var overlays = Resources.FindObjectsOfTypeAll<DamageOverlay>();
        foreach (var ov in overlays)
        {
            if (ov != null && ov.gameObject.scene.IsValid())
            {
                _damageOverlay = ov;
                break;
            }
        }
        #endif

        if (_damageOverlay == null)
        {
            Debug.LogWarning("FPSController: No DamageOverlay found in scene. Damage visual feedback will not work.");
        }
    }

    /// <summary>
    /// External API: called by damage sources (Projectile.SendMessage("TakeDamage", damage)).
    /// Accepts float damage but treats as integer hit-count by rounding up.
    /// Triggers a short camera vibration and handles death when HP reaches zero.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (_isInvulnerable) return;

        int dmg = Mathf.Max(1, Mathf.CeilToInt(damageAmount));
        _currentHealth -= dmg;

        // Start invulnerability and camera shake feedback
        _isInvulnerable = true;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(DoCameraShake(hitVibrationDuration, hitVibrationMagnitude));
        StartCoroutine(EndInvulnerabilityAfter(invulnerabilitySeconds));

        // Ensure we have a reference and show damage overlay flash
        if (_damageOverlay == null)
        {
            ResolveDamageOverlayReference();
        }
        if (_damageOverlay != null)
        {
            _damageOverlay.ShowDamageFlash();
        }

        Debug.Log($"Player took {dmg} damage, {_currentHealth} HP left.");

        // Handle death if health reaches zero
        if (_currentHealth <= 0)
        {
            Debug.Log("Player died.");
            // Disable further input and actions
            SetDisabled(true);

            // Trigger death handling in PlayerManager if present
            var pm = PlayerManager.Instance;
            if (pm != null)
            {
                pm.OnPlayerDeath();
            }
            else
            {
                // Fallback death handling
                GameSceneManager.ReloadCurrentScene();
            }
        }
    }

    // Camera shake coroutine for hit feedback
    private IEnumerator DoCameraShake(float duration, float magnitude)
    {
        if (cam == null) yield break;
        float elapsed = 0f;
        Vector3 originalPos = cam.transform.localPosition;

        while (elapsed < duration)
        {
            float x = originalPos.x + Random.Range(-magnitude, magnitude);
            float y = originalPos.y + Random.Range(-magnitude, magnitude);
            cam.transform.localPosition = new Vector3(x, y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originalPos;
    }

    // Invulnerability timer coroutine
    private IEnumerator EndInvulnerabilityAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _isInvulnerable = false;
    }

    /// <summary>
    /// Teleports the player to a specific position and rotation.
    /// Uses the exact same Teleport() method that portals use for identical behavior.
    /// </summary>
    /// <param name="position">Target position to teleport to</param>
    /// <param name="rotation">Target rotation to set</param>
    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        // Ensure time scale is normal (critical for builds)
        if (Time.timeScale <= 0f)
        {
            Time.timeScale = 1f;
        }

        // Reset velocity to zero before teleport (for respawn, we want no momentum)
        velocity = Vector3.zero;
        verticalVelocity = 0f;
        airHorizontalVelocity = Vector3.zero;
        jumping = false;
        lastGroundedTime = Time.time;

        // Create or reuse dummy portal transforms
        // Both use target rotation so relativeRotation will be identity (no rotation change)
        if (_dummyFromPortal == null)
        {
            GameObject dummyFromObj = new GameObject("DummyFromPortal");
            dummyFromObj.hideFlags = HideFlags.HideAndDontSave;
            _dummyFromPortal = dummyFromObj.transform;

            GameObject dummyToObj = new GameObject("DummyToPortal");
            dummyToObj.hideFlags = HideFlags.HideAndDontSave;
            _dummyToPortal = dummyToObj.transform;
        }

        // Set both portals to target position/rotation
        // This makes relativeRotation = identity (no transformation)
        _dummyFromPortal.position = transform.position;
        _dummyFromPortal.rotation = rotation;
        _dummyToPortal.position = position;
        _dummyToPortal.rotation = rotation;

        // Use the exact same Teleport method that portals use
        // This ensures identical behavior and smoothness
        Teleport(_dummyFromPortal, _dummyToPortal, position, rotation);

        // Ensure yaw and pitch match target rotation after teleport
        yaw = rotation.eulerAngles.y;
        pitch = 0f;
        if (cam != null)
        {
            cam.transform.localEulerAngles = Vector3.right * pitch;
        }
    }

    void Start()
    {
        cam = Camera.main;
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        controller = GetComponent<CharacterController>();

        yaw = transform.eulerAngles.y;
        pitch = cam != null ? cam.transform.localEulerAngles.x : 0f;

        // Store base values for surface physics modifications
        baseGroundFriction = groundFriction;
        baseGroundAcceleration = groundAcceleration;

        // Initialize input controls if not already done
        if (_controls == null)
        {
            _controls = InputManager.PlayerInput;
        }

        // Find damage overlay UI component
        ResolveDamageOverlayReference();

        // Ensure we have a Rigidbody (kinematic) for reliable trigger detection
        // CharacterController + Rigidbody (kinematic) = reliable OnTriggerEnter
        Rigidbody rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Detect surface physics when CharacterController hits a collider
        // Check if we're hitting from above (grounded) or from below (ceiling)
        if (hit.moveDirection.y < -0.3f || hit.moveDirection.y > 0.3f)
        {
            SurfacePhysics surface = hit.gameObject.GetComponent<SurfacePhysics>();
            if (surface != null)
            {
                currentSurfacePhysics = surface;
            }
        }
    }
    
    void FixedUpdate()
    {
        // Clear surface physics reference if we're not grounded
        // This ensures we don't keep old surface references
        if (!controller.isGrounded && (Time.time - lastGroundedTime > 0.1f))
        {
            currentSurfacePhysics = null;
        }
    }

    void Update()
    {


        if (disabled)
        {
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
        if (moveInput.sqrMagnitude > 0.01f)
        {
            desiredVelocity = worldInputDir * targetSpeed;
        }

        Vector3 horizontal;

        // Apply surface physics modifications
        float effectiveFriction;
        float effectiveAcceleration = baseGroundAcceleration;

        // Check for surface physics on the ground
        if (isGrounded && currentSurfacePhysics != null)
        {
            SurfacePhysics.SurfaceType surfaceType = currentSurfacePhysics.GetSurfaceType();
            
            switch (surfaceType)
            {
                case SurfacePhysics.SurfaceType.Sliding:
                    // Low friction = slippery surface
                    // Convert physics material friction (0-1) to our friction system
                    // Lower friction coefficient = less friction in our system
                    float physicsFriction = currentSurfacePhysics.frictionCoefficient;
                    // Map 0-1 physics friction to our friction range (inverse: low physics friction = high sliding)
                    effectiveFriction = baseGroundFriction * physicsFriction;
                    break;
                    
                case SurfacePhysics.SurfaceType.Bouncy:
                    // Bouncy surfaces have normal friction
                    effectiveFriction = baseGroundFriction;
                    break;
                    
                case SurfacePhysics.SurfaceType.Normal:
                case SurfacePhysics.SurfaceType.Destructive:
                default:
                    // Normal friction
                    effectiveFriction = baseGroundFriction;
                    break;
            }
        }
        else
        {
            // No surface physics detected, use base friction
            effectiveFriction = baseGroundFriction;
        }

        if (isGrounded)
        {
            // Grounded movement: simple acceleration and friction
            if (moveInput.sqrMagnitude > 0.01f)
            {
                // Accelerate towards desired velocity
                float acceleration = effectiveAcceleration * Time.deltaTime;
                horizontal = Vector3.MoveTowards(currentHorizontal, desiredVelocity, acceleration);
            }
            else
            {
                // Apply friction when not moving
                float friction = effectiveFriction * Time.deltaTime;
                horizontal = Vector3.MoveTowards(currentHorizontal, Vector3.zero, friction);

                // Snap to zero if very small
                if (horizontal.magnitude < 0.1f)
                {
                    horizontal = Vector3.zero;
                }
            }

            // Update air velocity when grounded (for jump momentum)
            airHorizontalVelocity = horizontal;
        }
        else
        {
            // Air movement: simple inertia with basic air control
            horizontal = airHorizontalVelocity;

            // Apply air control if input is given
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 accelVector = worldInputDir * (airControl * Time.deltaTime);
                horizontal += accelVector;
            }

            // Store for next frame (preserves inertia naturally)
            airHorizontalVelocity = horizontal;
        }

        velocity = new Vector3(horizontal.x, verticalVelocity, horizontal.z);

        verticalVelocity -= gravity * Time.deltaTime;
        velocity = new Vector3(velocity.x, verticalVelocity, velocity.z);

        // Clamp velocity to terminal velocity to prevent physics breaking
        if (velocity.magnitude > terminalVelocity)
        {
            velocity = velocity.normalized * terminalVelocity;
            verticalVelocity = velocity.y;
        }

        var flags = controller.Move(velocity * Time.deltaTime);
        if (flags == CollisionFlags.Below)
        {
            jumping = false;
            lastGroundedTime = Time.time;

            // Check for bouncy surface
            if (currentSurfacePhysics != null && currentSurfacePhysics.GetSurfaceType() == SurfacePhysics.SurfaceType.Bouncy)
            {
                // Apply bounce effect: reverse vertical velocity with bounce coefficient
                float bounceCoeff = currentSurfacePhysics.bounceCoefficient;
                verticalVelocity = -verticalVelocity * bounceCoeff;
                
                // Clamp bounce to prevent infinite bouncing
                if (Mathf.Abs(verticalVelocity) < 0.5f)
                {
                    verticalVelocity = 0;
                }
            }
            else
            {
                // Normal landing: reset vertical velocity
                verticalVelocity = 0;
            }

            // Reset air velocity when landing (will be set from ground movement next frame)
            airHorizontalVelocity = horizontal;
        }
        else
        {
            // Not grounded, clear surface physics reference
            currentSurfacePhysics = null;
        }

        if (jumpPressed)
        {
            float timeSinceLastTouchedGround = Time.time - lastGroundedTime;
            if (controller.isGrounded || (!jumping && timeSinceLastTouchedGround < 0.15f))
            {
                jumping = true;
                float jumpVelocity = jumpForce;
                
                // Boost jump on bouncy surfaces
                if (currentSurfacePhysics != null && currentSurfacePhysics.GetSurfaceType() == SurfacePhysics.SurfaceType.Bouncy)
                {
                    jumpVelocity *= (1f + currentSurfacePhysics.bounceCoefficient * 0.5f); // Extra bounce boost
                }
                
                verticalVelocity = jumpVelocity;
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
            pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

            transform.eulerAngles = Vector3.up * yaw;
            cam.transform.localEulerAngles = Vector3.right * pitch;
        }
    }

    /// <summary>
    /// Called by external systems (DeathZone, lasers, etc.) to kill the player.
    /// Prefer PlayerManager if it exists; otherwise fallback to reloading the scene.
    /// </summary>
    public void KillFromExternal()
    {
        // 1) Prefer PlayerManager singleton if present
        var pm = PlayerManager.Instance;
        if (pm != null)
        {
            pm.OnPlayerDeath();
            return;
        }

        // 2) Try to find a GameObject named "GameManager" (or similar) and invoke a known death method by reflection
        string[] candidateNames = { "GameManager", "Game Manager", "GM" };
        string[] methodNames = { "OnPlayerDeath", "HandlePlayerDeath", "KillPlayer", "PlayerDied" };

        foreach (var name in candidateNames)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;

            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                var type = comp.GetType();
                foreach (var mname in methodNames)
                {
                    var mi = type.GetMethod(mname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        mi.Invoke(comp, null);
                        Debug.Log($"FPSController.KillFromExternal: Invoked {mname} on {go.name}.{type.Name}");
                        return;
                    }
                }
            }
        }

        // 3) As a last resort, reload the current scene
        Debug.LogWarning("FPSController.KillFromExternal: No manager found to handle death. Reloading scene as fallback.");
        GameSceneManager.ReloadCurrentScene();
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot,
        float scaleRatio = 1f)
    {
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
        if (horizontalForward.sqrMagnitude > 0.01f)
        {
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

        // Simply preserve horizontal velocity naturally (inertia)
        airHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

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
