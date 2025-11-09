using UnityEngine;
using UnityEngine.InputSystem;
using Portal;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;         // Max distance for picking up
    public Transform holdParent;           // Empty child of camera for object to float in front
    public float holdDistance = 2f;        // Base distance forward from camera to position holdParent (scales with player scale)
    public float moveSpeed = 10f;          // Speed at which object moves to hold position
    public float rotationSpeed = 10f;      // Speed at which object rotates to hold rotation
    
    private float _basePlayerScale = 1f;  // Store base player scale for reference

    [Header("Debug")]
    public bool debugRay = true;

    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private Collider heldObjectCollider;
    private PortalTraveller heldObjectTraveller;
    private PortalCloneSystem heldObjectCloneSystem;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    private Input.PlayerInput _controls;
    
    // Static reference for checking if an object is held
    private static PlayerPickup _instance;
    
    public static bool IsObjectHeld(GameObject obj)
    {
        return _instance != null && _instance.heldObject == obj;
    }
    
    public bool IsHoldingObject()
    {
        return heldObject != null;
    }
    
    public GameObject GetHeldObject()
    {
        return heldObject;
    }

    private void Start()
    {
        // Initialize input controls if not already done
        if (_controls == null)
        {
            _controls = InputManager.PlayerInput;
        }
        
        // Set static instance
        _instance = this;
        
        // Store base player scale (use average of x, y, z scales)
        Vector3 baseScale = transform.localScale;
        _basePlayerScale = (baseScale.x + baseScale.y + baseScale.z) / 3f;
        
        // Subscribe to player teleportation events
        var fpsController = GetComponent<FPSController>();
        if (fpsController)
        {
            // We'll detect teleportation through PortalTravellerHandler
        }
    }
    
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void TryPickup()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        RaycastHit[] hits = Physics.RaycastAll(ray, pickupRange);

        float closestDistance = Mathf.Infinity;
        GameObject closestObject = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Interactable") && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestObject = hit.collider.gameObject;
            }
        }

        if (closestObject != null)
        {
            heldObject = closestObject;
            heldObjectRb = heldObject.GetComponent<Rigidbody>();
            heldObjectCollider = heldObject.GetComponent<Collider>();

            // Ensure object has a PortalTraveller component to pass through portals
            heldObjectTraveller = heldObject.GetComponent<PortalTraveller>();
            if (!heldObjectTraveller)
            {
                heldObjectTraveller = heldObject.AddComponent<PortalTraveller>();
            }

            // Ensure object has PortalCloneSystem for portal clone effect
            heldObjectCloneSystem = heldObject.GetComponent<PortalCloneSystem>();
            if (!heldObjectCloneSystem)
            {
                heldObjectCloneSystem = heldObject.AddComponent<PortalCloneSystem>();
            }
            heldObjectCloneSystem.SetHeld(true);

            if (heldObjectRb != null)
            {
                // Keep physics enabled but disable gravity
                heldObjectRb.linearVelocity = Vector3.zero;
                heldObjectRb.angularVelocity = Vector3.zero;
                heldObjectRb.useGravity = false;
                heldObjectRb.isKinematic = false; // Keep non-kinematic for collision detection
                heldObjectRb.linearDamping = 5f; // Add drag to smooth movement
                heldObjectRb.angularDamping = 5f;
            }
            
            // Keep collider enabled so it can collide with walls and portals
            // Collider stays enabled - physics will handle collisions

            // Don't parent - we'll move it with physics instead
            UpdateTargetPosition();

            Debug.Log("Picked up: " + heldObject.name);
        }
    }


    private void DropObject()
    {
        if (heldObject == null) return;

        // Notify clone system that we're dropping (will swap if clone exists)
        if (heldObjectCloneSystem != null)
        {
            heldObjectCloneSystem.SetHeld(false);
            heldObjectCloneSystem = null;
        }

        // Re-enable physics
        if (heldObjectRb != null)
        {
            heldObjectRb.useGravity = true;
            heldObjectRb.linearDamping = 0f;
            heldObjectRb.angularDamping = 0.05f;
            
            // Give a small forward push
            if (Camera.main != null)
            {
                heldObjectRb.AddForce(Camera.main.transform.forward * 2f, ForceMode.VelocityChange);
            }
        }

        Debug.Log("Dropped: " + heldObject.name);
        heldObject = null;
        heldObjectRb = null;
        heldObjectCollider = null;
        heldObjectTraveller = null;
    }
    
    // Called when player teleports - swap held object with clone if it exists
    public void OnPlayerTeleport()
    {
        if (heldObjectCloneSystem != null)
        {
            heldObjectCloneSystem.OnPlayerTeleport();
        }
    }

    private void Update()
    {
        // Check for interact input
        if (_controls != null && _controls.Player.Interact.WasPerformedThisFrame())
        {
            if (heldObject == null)
                TryPickup();
            else
                DropObject();
        }

        // Position holdParent at holdDistance from camera (scaled by player scale)
        if (holdParent != null && Camera.main != null)
        {
            // Calculate current player scale factor
            Vector3 currentScale = transform.localScale;
            float currentScaleFactor = (currentScale.x + currentScale.y + currentScale.z) / 3f / _basePlayerScale;
            
            // Scale hold distance by player scale
            float scaledHoldDistance = holdDistance * currentScaleFactor;
            
            holdParent.position = Camera.main.transform.position + Camera.main.transform.forward * scaledHoldDistance;
            holdParent.rotation = Camera.main.transform.rotation;
        }

        // Update target position every frame based on camera position
        if (heldObject != null)
        {
            UpdateTargetPosition();
        }
    }

    private void FixedUpdate()
    {
        // Move held object using physics in FixedUpdate
        if (heldObject != null && heldObjectRb != null)
        {
            // Calculate desired position and rotation
            Vector3 desiredPosition = targetPosition;
            Quaternion desiredRotation = targetRotation;

            // Calculate spring-like force to reach target position
            // This approach naturally stops when hitting walls because physics handles collisions
            Vector3 positionDifference = desiredPosition - heldObjectRb.position;
            Vector3 desiredVelocity = positionDifference * moveSpeed;
            Vector3 force = (desiredVelocity - heldObjectRb.linearVelocity) * heldObjectRb.mass;

            // Apply force to move toward target (physics will prevent clipping through walls)
            heldObjectRb.AddForce(force, ForceMode.Force);

            // Rotate toward target rotation
            heldObjectRb.MoveRotation(Quaternion.Slerp(heldObjectRb.rotation, desiredRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    private void UpdateTargetPosition()
    {
        if (Camera.main != null)
        {
            // Use holdParent position directly (it's already positioned at scaled holdDistance)
            if (holdParent != null)
            {
                targetPosition = holdParent.position;
                targetRotation = holdParent.rotation;
            }
            else
            {
                // Fallback: calculate from camera if no holdParent assigned (scaled by player scale)
                Vector3 currentScale = transform.localScale;
                float currentScaleFactor = (currentScale.x + currentScale.y + currentScale.z) / 3f / _basePlayerScale;
                float scaledHoldDistance = holdDistance * currentScaleFactor;
                
                targetPosition = Camera.main.transform.position + Camera.main.transform.forward * scaledHoldDistance;
                targetRotation = Camera.main.transform.rotation;
            }
        }
    }

}
