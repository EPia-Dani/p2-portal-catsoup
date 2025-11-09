using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;         // Max distance for picking up
    public Transform holdParent;           // Empty child of camera for object to float in front
    public float holdDistance = 2f;        // Distance forward from camera to position holdParent
    public float moveSpeed = 10f;          // Speed at which object moves to hold position
    public float rotationSpeed = 10f;      // Speed at which object rotates to hold rotation

    [Header("Debug")]
    public bool debugRay = true;

    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private Collider heldObjectCollider;
    private PortalTraveller heldObjectTraveller;
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
            
            // Keep collider enabled so it can collide with walls
            // Collider stays enabled - physics will handle collisions

            // Don't parent - we'll move it with physics instead
            UpdateTargetPosition();

            Debug.Log("Picked up: " + heldObject.name);
        }
    }


    private void DropObject()
    {
        if (heldObject == null) return;

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

        // Position holdParent at holdDistance from camera
        if (holdParent != null && Camera.main != null)
        {
            holdParent.position = Camera.main.transform.position + Camera.main.transform.forward * holdDistance;
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
            // Use holdParent position directly (it's already positioned at holdDistance)
            if (holdParent != null)
            {
                targetPosition = holdParent.position;
                targetRotation = holdParent.rotation;
            }
            else
            {
                // Fallback: calculate from camera if no holdParent assigned
                targetPosition = Camera.main.transform.position + Camera.main.transform.forward * holdDistance;
                targetRotation = Camera.main.transform.rotation;
            }
        }
    }

}
