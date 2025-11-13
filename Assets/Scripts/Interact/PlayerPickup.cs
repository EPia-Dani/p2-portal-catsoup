using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;         // Max distance for picking up
    public Transform holdParent;           // Empty child of camera for object to float in front
    public float holdDistance = 2f;        // Base distance forward from camera to position holdParent (scales with player scale)
    
    private float _basePlayerScale = 1f;  // Store base player scale for reference

    [Header("Debug")]
    public bool debugRay = true;

    private InteractableObject _heldObject;
    
    private Input.PlayerInput _controls;
    
    // Static reference for checking if an object is held
    private static PlayerPickup _instance;
    
    public static bool IsObjectHeld(GameObject obj)
    {
        return _instance != null && _instance._heldObject != null && _instance._heldObject.gameObject == obj;
    }
    
    public bool IsHoldingObject()
    {
        return _heldObject != null;
    }
    
    public GameObject GetHeldObject()
    {
        return _heldObject != null ? _heldObject.gameObject : null;
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
        InteractableObject closestObject = null;

        foreach (RaycastHit hit in hits)
        {
            // Check for InteractableObject component directly (works for Radio and all InteractableObject types)
            // This ensures radios can be picked up even if they don't have the "Interactable" tag
            var interactable = hit.collider.GetComponent<InteractableObject>();
            
            // Also check parent/root GameObject in case collider is on a child object
            if (interactable == null)
            {
                interactable = hit.collider.GetComponentInParent<InteractableObject>();
            }
            
            if (interactable != null && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestObject = interactable;
            }
        }

        if (closestObject != null)
        {
            _heldObject = closestObject;
            _heldObject.OnPickedUp(this);
            UpdateTargetPosition();
            Debug.Log("Picked up: " + _heldObject.name);
        }
    }

    private void DropObject()
    {
        if (_heldObject == null) return;

        _heldObject.OnDropped();
        Debug.Log("Dropped: " + _heldObject.name);
        _heldObject = null;
    }
    
    // Called when player teleports - swap held object with clone if it exists
    public void OnPlayerTeleport()
    {
        if (_heldObject != null)
        {
            _heldObject.OnPlayerTeleport();
        }
    }

    private void Update()
    {
        // Check for interact input
        if (_controls != null && _controls.Player.Interact.WasPerformedThisFrame())
        {
            if (_heldObject == null)
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
        if (_heldObject != null)
        {
            UpdateTargetPosition();
        }
    }

    private void UpdateTargetPosition()
    {
        if (Camera.main != null && _heldObject != null)
        {
            Vector3 targetPos;
            Quaternion targetRot;
            
            // Use holdParent position directly (it's already positioned at scaled holdDistance)
            if (holdParent != null)
            {
                targetPos = holdParent.position;
                targetRot = holdParent.rotation;
            }
            else
            {
                // Fallback: calculate from camera if no holdParent assigned (scaled by player scale)
                Vector3 currentScale = transform.localScale;
                float currentScaleFactor = (currentScale.x + currentScale.y + currentScale.z) / 3f / _basePlayerScale;
                float scaledHoldDistance = holdDistance * currentScaleFactor;
                
                targetPos = Camera.main.transform.position + Camera.main.transform.forward * scaledHoldDistance;
                targetRot = Camera.main.transform.rotation;
            }
            
            // Update target transform on the held object
            _heldObject.SetTargetTransform(targetPos, targetRot);
        }
    }
}
