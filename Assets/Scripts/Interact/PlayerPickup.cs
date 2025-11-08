using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupRange = 5f;         // Max distance for picking up
    public Transform holdParent;           // Empty child of camera for object to float in front

    [Header("Input")]
    public InputActionReference interactAction; // Assign your Interact action here

    [Header("Debug")]
    public bool debugRay = true;

    private GameObject heldObject;
    private Rigidbody heldObjectRb;
    private Collider heldObjectCollider;

    private void OnEnable()
    {
        interactAction.action.performed += OnInteract;
        interactAction.action.Enable();
    }

    private void OnDisable()
    {
        interactAction.action.performed -= OnInteract;
        interactAction.action.Disable();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (heldObject == null)
            TryPickup();
        else
            DropObject();
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

            if (heldObjectRb != null)
            {
                heldObjectRb.linearVelocity = Vector3.zero;
                heldObjectRb.angularVelocity = Vector3.zero;
                heldObjectRb.useGravity = false;
                heldObjectRb.isKinematic = true;
            }
            
            // Disable collider to prevent interference with player/camera
            if (heldObjectCollider != null)
            {
                heldObjectCollider.enabled = false;
            }

            // Parent to holdParent so it moves naturally with camera
            heldObject.transform.SetParent(holdParent, worldPositionStays: false);
            heldObject.transform.localPosition = Vector3.zero;
            heldObject.transform.localRotation = Quaternion.identity;

            Debug.Log("Picked up: " + heldObject.name);
        }
    }


    private void DropObject()
    {
        if (heldObject == null) return;

        // Unparent the object FIRST (keep world position)
        heldObject.transform.SetParent(null, worldPositionStays: true);

        // Re-enable collider so it can be detected by raycast again
        if (heldObjectCollider != null)
            heldObjectCollider.enabled = true;

        // Re-enable physics
        if (heldObjectRb != null)
        {
            heldObjectRb.isKinematic = false;
            heldObjectRb.useGravity = true;
            heldObjectRb.linearDamping = 1f;
            
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
    }

}
