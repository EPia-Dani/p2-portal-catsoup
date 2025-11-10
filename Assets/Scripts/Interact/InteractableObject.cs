using UnityEngine;
using Portal;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class InteractableObject : PortalTraveller
{
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

    public bool IsHeld => _isHeld;
    public Rigidbody Rigidbody => _rigidbody;
    public PortalCloneSystem PortalCloneSystem => _portalCloneSystem;

    void Awake()
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
        
        _isHeld = false;
        
        // Notify clone system that we're dropping (will swap if clone exists)
        if (_portalCloneSystem != null)
        {
            _portalCloneSystem.SetHeld(false);
        }
        
        // Restore physics
        _rigidbody.useGravity = _originalUseGravity;
        _rigidbody.linearDamping = _originalLinearDamping;
        _rigidbody.angularDamping = _originalAngularDamping;
        
        // Give a small forward push
        if (Camera.main != null)
        {
            _rigidbody.AddForce(Camera.main.transform.forward * dropForce, ForceMode.VelocityChange);
        }
        
        _holder = null;
        
        Debug.Log($"[InteractableObject] {gameObject.name} dropped");
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

        // Capture velocity BEFORE teleporting
        Vector3 velocityBeforeTeleport = _rigidbody.linearVelocity;
        Vector3 angularVelocityBeforeTeleport = _rigidbody.angularVelocity;

        // Call base Teleport to handle position, rotation, and scaling
        base.Teleport(fromPortal, toPortal, pos, rot, scaleRatio);

        // Calculate portal transformation for velocity
        Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
        Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);

        // Transform velocity through portal
        Vector3 finalVelocity = Vector3.zero;
        if (velocityBeforeTeleport.sqrMagnitude > 0.001f)
        {
            // Scale velocity by portal size difference
            Vector3 transformedVelocity = velocityBeforeTeleport * scaleRatio;

            // Rotate velocity through portal (same transformation as player)
            // Include 180° flip so 'entering' becomes 'exiting'
            transformedVelocity = relativeRotation * transformedVelocity;

            finalVelocity = transformedVelocity;

            // Transform angular velocity too
            if (angularVelocityBeforeTeleport.sqrMagnitude > 0.001f)
            {
                Vector3 transformedAngularVelocity = relativeRotation * angularVelocityBeforeTeleport;
                _rigidbody.angularVelocity = transformedAngularVelocity;
            }
        }

        // Apply minimum exit velocity using base class method (handles non-vertical to vertical transitions)
        finalVelocity = ApplyMinimumExitVelocity(fromPortal, toPortal, finalVelocity);

        // Apply final velocity
        _rigidbody.linearVelocity = finalVelocity;

        // Sync physics to prevent collision detection issues
        Physics.SyncTransforms();
    }

    void UpdateHeldMovement()
    {
        if (_rigidbody == null) return;
        
        // Calculate spring-like force to reach target position
        Vector3 positionDifference = _targetPosition - _rigidbody.position;
        Vector3 desiredVelocity = positionDifference * moveSpeed;
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
}

