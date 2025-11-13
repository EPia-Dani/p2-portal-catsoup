using UnityEngine;
using Portal;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class InteractableObject : PortalTraveller
{
    /// <summary>
    /// Types of interactable objects for filtering purposes
    /// </summary>
    public enum InteractableType
    {
        Radio,              // Radio objects
        SimpleInteractable, // Basic interactable objects (cubes, etc.)
        Other               // Other types of interactables
    }

    [Header("Interactable Type")]
    [Tooltip("Type of interactable for physics surface filtering")]
    public InteractableType interactableType = InteractableType.SimpleInteractable;

    [Header("Physics Settings")]
    [Tooltip("Maximum velocity magnitude to prevent physics breaking")]
    public float terminalVelocity = 50f;
    
    [Tooltip("Maximum angular velocity magnitude")]
    public float maxAngularVelocity = 50f;
    
    [Header("Pickup Settings")]
    [Tooltip("Speed at which object moves to hold position when picked up")]
    public float moveSpeed = 10f;
    
    [Tooltip("Speed at which object rotates to hold rotation when picked up")]
    public float rotationSpeed = 10f;
    
    [Tooltip("Damping applied to rigidbody when held")]
    public float heldDamping = 5f;
    
    [Tooltip("Damping applied to rigidbody when dropped")]
    public float droppedDamping = 0.05f;
    
    [Header("Drop Settings")]
    [Tooltip("Forward force applied when dropped")]
    public float dropForce = 2f;
    
    [Header("RefractionCube")]
    public bool isRefractionCube = false;

    // Components
    private Rigidbody _rigidbody;
    private Collider _collider;
    private PortalCloneSystem _portalCloneSystem;
    
    // State
    private bool _isHeld = false;
    private PlayerPickup _holder = null;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    
    // Store original physics properties
    private bool _originalUseGravity;
    private float _originalLinearDamping;
    private float _originalAngularDamping;
    
    // Track target position velocity (motion from being held/moved)
    private Vector3 _previousTargetPosition;
    private Vector3 _targetVelocity;
    

    public bool IsHeld => _isHeld;
    public Rigidbody Rigidbody => _rigidbody;
    public PortalCloneSystem PortalCloneSystem => _portalCloneSystem;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody == null)
        {
            Debug.LogError($"[InteractableObject] {gameObject.name} requires a Rigidbody component!");
            enabled = false;
            return;
        }
        
        if (_collider == null)
        {
            Debug.LogError($"[InteractableObject] {gameObject.name} requires a Collider component!");
            enabled = false;
            return;
        }
        
        // Store original physics properties
        _originalUseGravity = _rigidbody.useGravity;
        _originalLinearDamping = _rigidbody.linearDamping;
        _originalAngularDamping = _rigidbody.angularDamping;
        
        // PortalCloneSystem will be added when picked up
    }

    void FixedUpdate()
    {
        if (_rigidbody == null) return;
        
        // Apply terminal velocity - clamp linear velocity
        float currentSpeed = _rigidbody.linearVelocity.magnitude;
        if (currentSpeed > terminalVelocity)
        {
            _rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * terminalVelocity;
        }
        
        // Apply terminal velocity - clamp angular velocity
        float currentAngularSpeed = _rigidbody.angularVelocity.magnitude;
        if (currentAngularSpeed > maxAngularVelocity)
        {
            _rigidbody.angularVelocity = _rigidbody.angularVelocity.normalized * maxAngularVelocity;
        }
        
        // Handle held object movement
        if (_isHeld && _holder != null)
        {
            // Track target velocity (how fast the target position is moving)
            // This captures motion from player movement and rotation
            Vector3 targetDelta = _targetPosition - _previousTargetPosition;
            _targetVelocity = targetDelta / Time.fixedDeltaTime;
            _previousTargetPosition = _targetPosition;
            
            UpdateHeldMovement();
        }
    }

    /// <summary>
    /// Called by PlayerPickup when this object is picked up
    /// </summary>
    public void OnPickedUp(PlayerPickup holder)
    {
        if (_isHeld) return;
        
        _isHeld = true;
        _holder = holder;
        
        // Ensure portal components exist
        EnsurePortalCloneSystem();
        
        // Activate clone system
        if (_portalCloneSystem != null)
        {
            _portalCloneSystem.SetHeld(true);
        }
        
        // Initialize target velocity tracking
        _previousTargetPosition = _rigidbody.position;
        _targetVelocity = Vector3.zero;
        
        // Configure physics for being held
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = false; // Keep non-kinematic for collision detection
        _rigidbody.linearDamping = heldDamping;
        _rigidbody.angularDamping = heldDamping;
        
        Debug.Log($"[InteractableObject] {gameObject.name} picked up");
    }

    /// <summary>
    /// Called by PlayerPickup when this object is dropped
    /// </summary>
    public void OnDropped()
    {
        if (!_isHeld) return;
        
        // Safety check: ensure rigidbody is initialized
        if (_rigidbody == null)
        {
            Debug.LogError($"[InteractableObject] {gameObject.name} OnDropped() called but _rigidbody is null! This should not happen.");
            _isHeld = false;
            _holder = null;
            return;
        }
        
        // Capture the CURRENT velocity right before dropping (includes motion from being held/moved)
        // This is the actual velocity the rigidbody has from all the forces applied during holding
        // It already includes motion from player movement AND rotation (tangential velocity)
        Vector3 currentLinearVelocity = _rigidbody.linearVelocity;
        Vector3 currentAngularVelocity = _rigidbody.angularVelocity;
        
        _isHeld = false;
        
        // CRITICAL: Restore physics BEFORE clone system operations
        // This ensures gravity and physics work even if object is inside portal
        _rigidbody.useGravity = _originalUseGravity;
        _rigidbody.linearDamping = _originalLinearDamping;
        _rigidbody.angularDamping = _originalAngularDamping;
        
        // Use the ACTUAL rigidbody velocity - it already includes all motion from forces applied
        // The forces in UpdateHeldMovement() already capture player movement and rotation
        Vector3 finalVelocity = currentLinearVelocity;
        
        // If actual velocity is very small (damping reduced it too much), enhance with target velocity
        // This handles cases where damping has reduced velocity but object should still have momentum
        if (finalVelocity.sqrMagnitude < 0.1f && _targetVelocity.sqrMagnitude > 0.1f)
        {
            // Blend actual velocity with target velocity to preserve momentum
            finalVelocity = Vector3.Lerp(finalVelocity, _targetVelocity, 0.7f);
        }
        
        // Fallback: if both are small, use player's velocity as last resort
        if (finalVelocity.sqrMagnitude < 0.1f && _holder != null)
        {
            var playerController = _holder.GetComponent<FPSController>();
            if (playerController != null)
            {
                finalVelocity = playerController.CurrentVelocity;
            }
        }
        
        // Apply the velocity - this preserves the object's motion at the moment of drop
        _rigidbody.linearVelocity = finalVelocity;
        _rigidbody.angularVelocity = currentAngularVelocity; // Preserve angular velocity
        
        // Notify clone system that we're dropping (will swap if clone exists)
        // This happens AFTER we've set velocity so SwapWithClone can preserve it
        if (_portalCloneSystem != null)
        {
            _portalCloneSystem.SetHeld(false);
        }
        
        _holder = null;
        
        Debug.Log($"[InteractableObject] {gameObject.name} dropped with velocity: {_rigidbody.linearVelocity} (target velocity: {_targetVelocity})");
    }

    /// <summary>
    /// Called by PlayerPickup to update target position/rotation for held movement
    /// </summary>
    public void SetTargetTransform(Vector3 position, Quaternion rotation)
    {
        _targetPosition = position;
        _targetRotation = rotation;
    }

    /// <summary>
    /// Called when player teleports - swap with clone if it exists
    /// </summary>
    public void OnPlayerTeleport()
    {
        if (_portalCloneSystem != null)
        {
            _portalCloneSystem.OnPlayerTeleport();
        }
    }

    /// <summary>
    /// Override Teleport to properly transform velocity through portals
    /// This is critical for objects falling through floor portals to maintain momentum
    /// Uses the exact same method as FPSController for consistency
    /// </summary>
    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot, float scaleRatio = 1f)
    {
        if (_rigidbody == null)
        {
            // If no rigidbody, just use base teleport
            base.Teleport(fromPortal, toPortal, pos, rot, scaleRatio);
            return;
        }

        // Don't transform velocity if object is being held (it's handled by clone system)
        if (_isHeld)
        {
            base.Teleport(fromPortal, toPortal, pos, rot, scaleRatio);
            return;
        }

        // Capture velocity BEFORE position change (exact same as FPSController)
        Vector3 currentVelocity = _rigidbody.linearVelocity;
        Vector3 currentAngularVelocity = _rigidbody.angularVelocity;
        
        // Scale velocity based on portal size difference (exact same as FPSController)
        currentVelocity *= scaleRatio;
        
        // Teleport the object position (exact same as FPSController)
        transform.position = pos;
        
        // ===== UNIVERSAL VELOCITY TRANSFORMATION =====
        // Rotate the entire velocity vector from the source portal's orientation to the destination's.
        // We include a 180° flip around the portal's local up so 'entering' becomes 'exiting'.
        // (exact same as FPSController)
        Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
        Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);
        
        // Transform the object's forward direction through the portal (same transformation as velocity)
        // This preserves relative orientation: looking left relative to source portal = looking left relative to dest portal
        Vector3 currentForward = transform.forward;
        Vector3 transformedForward = relativeRotation * currentForward;
        
        // Project onto horizontal plane and extract yaw
        Vector3 horizontalForward = new Vector3(transformedForward.x, 0, transformedForward.z);
        if (horizontalForward.sqrMagnitude > 0.01f)
        {
            horizontalForward.Normalize();
            float yaw = Mathf.Atan2(horizontalForward.x, horizontalForward.z) * Mathf.Rad2Deg;
            transform.eulerAngles = Vector3.up * yaw;
        }
        // If horizontal component is too small, keep current rotation (rare edge case)
        
        // Rotate the captured world-space velocity into the destination orientation (exact same as FPSController)
        Vector3 velocity = relativeRotation * currentVelocity;

        // Apply minimum exit velocity using base class method (handles non-vertical to vertical transitions)
        // (exact same as FPSController)
        velocity = ApplyMinimumExitVelocity(fromPortal, toPortal, velocity);

        // CRITICAL: Ensure velocity points away from portal to prevent bouncing back
        // Portal forward points INTO the wall, so exit direction is OPPOSITE (exact same logic as player)
        Vector3 exitDirection = -toPortal.forward; // Negative because portal forward points into wall
        Vector3 horizontalExitDirection = new Vector3(exitDirection.x, 0, exitDirection.z).normalized;
        
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;
        
        // Calculate how much velocity is pointing away from portal (exit direction)
        float velocityDotExit = horizontalSpeed > 0.01f 
            ? Vector3.Dot(horizontalVelocity.normalized, horizontalExitDirection) 
            : -1f; // If no horizontal velocity, treat as pointing backwards
        
        // If velocity is pointing back towards portal or is too small, ensure minimum exit component
        if (velocityDotExit < 0.3f || horizontalSpeed < 0.5f)
        {
            float minExitSpeed = Mathf.Max(1.5f, horizontalSpeed * 0.5f + 0.5f); // At least 1.5 units/sec away
            
            // If we have existing horizontal velocity, blend it with exit direction
            if (horizontalSpeed > 0.01f && velocityDotExit > -0.5f)
            {
                // Blend: favor exit direction but keep some of existing velocity
                Vector3 desiredExit = horizontalExitDirection * minExitSpeed;
                horizontalVelocity = Vector3.Lerp(horizontalVelocity, desiredExit, 0.7f);
            }
            else
            {
                // No meaningful horizontal velocity, use pure exit direction
                horizontalVelocity = horizontalExitDirection * minExitSpeed;
            }
            
            // Reconstruct velocity with new horizontal component
            velocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }
        
        // CRITICAL: Push object slightly away from portal to prevent immediate re-entry
        // Use exit direction (away from portal) - portal forward points INTO wall, so we use negative
        float pushDistance = 0.15f; // Small push to clear portal boundary
        transform.position += horizontalExitDirection * pushDistance;

        // Transform angular velocity too (exact same transformation)
        if (currentAngularVelocity.sqrMagnitude > 0.001f)
        {
            Vector3 transformedAngularVelocity = relativeRotation * currentAngularVelocity;
            _rigidbody.angularVelocity = transformedAngularVelocity;
        }

        // Apply final velocity (exact same as FPSController)
        _rigidbody.linearVelocity = velocity;

        // Call base Teleport to handle scaling (exact same as FPSController)
        base.Teleport(fromPortal, toPortal, pos, rot, scaleRatio);

        // CRITICAL: Notify clone system that we teleported via PortalTravellerHandler
        // This prevents clone system from interfering and causing object to disappear
        if (_portalCloneSystem != null)
        {
            _portalCloneSystem.OnObjectTeleported(fromPortal, toPortal);
        }

        // Sync physics to prevent collision detection issues (exact same as FPSController)
        Physics.SyncTransforms();
    }

    void UpdateHeldMovement()
    {
        if (_rigidbody == null) return;
        
        // Calculate spring-like force to reach target position
        Vector3 positionDifference = _targetPosition - _rigidbody.position;
        
        // CRITICAL: Add target velocity to desired velocity so object follows arc motion when camera rotates
        // This prevents objects from falling when rotating the camera
        Vector3 desiredVelocity = (positionDifference * moveSpeed) + _targetVelocity;
        
        Vector3 force = (desiredVelocity - _rigidbody.linearVelocity) * _rigidbody.mass;
        
        // Apply force to move toward target (physics will prevent clipping through walls)
        _rigidbody.AddForce(force, ForceMode.Force);
        
        // Rotate toward target rotation
        _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, _targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    void EnsurePortalCloneSystem()
    {
        if (_portalCloneSystem == null)
        {
            _portalCloneSystem = GetComponent<PortalCloneSystem>();
            if (_portalCloneSystem == null)
            {
                _portalCloneSystem = gameObject.AddComponent<PortalCloneSystem>();
            }
        }
    }

    /// <summary>
    /// Gets the interactable type, automatically detecting Radio if applicable
    /// </summary>
    public InteractableType GetInteractableType()
    {
        // Auto-detect Radio type
        if (this is Radio)
        {
            return InteractableType.Radio;
        }
        
        return interactableType;
    }
    
    
}

