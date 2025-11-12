using System.Collections.Generic;
using UnityEngine;

namespace Interact
{
    /// <summary>
    /// A physical floor button that activates when objects (not the player) are placed on top of it.
    /// Uses a child GameObject with a collider for detection and animates it falling down.
    /// </summary>
    public class Button : MonoBehaviour
    {
        [Header("Button Parts")]
        [Tooltip("The child GameObject that represents the button part that moves (should have a collider).")]
        public GameObject buttonChild;
        
        [Header("Button Settings")]
        [Tooltip("How far down the button moves when pressed (in local Y units).")]
        public float pressDistance = 0.2f;
        
        [Tooltip("Speed at which the button moves down/up (units per second).")]
        public float pressSpeed = 2f;
        
        [Header("Door Settings")]
        [Tooltip("The Door component(s) to open/close when button is pressed. Can assign multiple doors.")]
        [SerializeField] Door[] doors;
        
        [Header("Debug")]
        [Tooltip("Current pressed state of the button.")]
        public bool isPressed = false;
        
        // Track objects currently on the button
        private HashSet<Collider> _objectsOnButton = new HashSet<Collider>();
        
        // Button child movement
        private Vector3 _buttonChildOriginalPosition;
        private Vector3 _buttonChildPressedPosition;
        private ButtonTrigger _buttonTrigger;
        private Collider _buttonChildCollider;
        
        private void Start()
        {
            // Find button child if not assigned
            if (buttonChild == null)
            {
                // Try to find a child with a collider
                Collider[] childColliders = GetComponentsInChildren<Collider>();
                foreach (Collider col in childColliders)
                {
                    if (col.gameObject != gameObject && col.isTrigger)
                    {
                        buttonChild = col.gameObject;
                        break;
                    }
                }
            }
            
            if (buttonChild == null)
            {
                Debug.LogError($"[Button] {gameObject.name}: Button child not assigned and could not be found! Please assign the child GameObject with the collider.");
                enabled = false;
                return;
            }
            
            // Store original position
            _buttonChildOriginalPosition = buttonChild.transform.localPosition;
            _buttonChildPressedPosition = _buttonChildOriginalPosition + Vector3.down * pressDistance;
            
            // Ensure child has a collider set as trigger
            _buttonChildCollider = buttonChild.GetComponent<Collider>();
            if (_buttonChildCollider == null)
            {
                Debug.LogError($"[Button] {gameObject.name}: Button child '{buttonChild.name}' must have a Collider component!");
                enabled = false;
                return;
            }
            
            if (!_buttonChildCollider.isTrigger)
            {
                Debug.LogWarning($"[Button] {gameObject.name}: Button child '{buttonChild.name}' collider should be set as Trigger for button to work properly.");
            }
            
            // Ignore collisions between button child and all interactable objects
            // This prevents physics interference when button moves
            SetupCollisionIgnores();
            
            // Add or get ButtonTrigger component on child to handle collisions
            _buttonTrigger = buttonChild.GetComponent<ButtonTrigger>();
            if (_buttonTrigger == null)
            {
                _buttonTrigger = buttonChild.AddComponent<ButtonTrigger>();
            }
            _buttonTrigger.Initialize(this);
        }
        
        /// <summary>
        /// Sets up collision ignores between button child and interactable objects.
        /// </summary>
        private void SetupCollisionIgnores()
        {
            // Find all interactable objects in the scene and ignore collisions with button child
            InteractableObject[] interactables = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            foreach (InteractableObject interactable in interactables)
            {
                Collider objCollider = interactable.GetComponent<Collider>();
                if (objCollider != null && _buttonChildCollider != null)
                {
                    Physics.IgnoreCollision(_buttonChildCollider, objCollider, true);
                }
            }
        }
        
        /// <summary>
        /// Called by ButtonTrigger when an object enters the button.
        /// </summary>
        public void OnObjectEnter(Collider other)
        {
            if (other == null) return;
            
            // Don't track held objects
            if (PlayerPickup.IsObjectHeld(other.gameObject))
            {
                return;
            }
            
            // Ensure collision is ignored with button child (in case object was created after Start)
            if (_buttonChildCollider != null)
            {
                Physics.IgnoreCollision(_buttonChildCollider, other, true);
            }
            
            // Add to tracked objects
            _objectsOnButton.Add(other);
            
            // Update button state
            UpdateButtonState();
        }
        
        /// <summary>
        /// Called by ButtonTrigger when an object exits the button.
        /// </summary>
        public void OnObjectExit(Collider other)
        {
            if (other == null) return;
            
            // Remove from tracked objects if it was being tracked
            if (_objectsOnButton.Remove(other))
            {
                // Update button state
                UpdateButtonState();
            }
        }
        
        /// <summary>
        /// Updates the button state based on whether objects are on it.
        /// </summary>
        private void UpdateButtonState()
        {
            bool shouldBePressed = _objectsOnButton.Count > 0;
            
            // Only update if state changed
            if (shouldBePressed == isPressed)
            {
                return;
            }
            
            isPressed = shouldBePressed;
            
            if (isPressed)
            {
                OnButtonPressed();
            }
            else
            {
                OnButtonUnpressed();
            }
        }
        
        /// <summary>
        /// Called when button becomes pressed (object placed on it).
        /// </summary>
        private void OnButtonPressed()
        {
            // Open all assigned doors
            if (doors != null)
            {
                foreach (Door door in doors)
                {
                    if (door != null)
                    {
                        door.Open();
                    }
                }
            }
        }
        
        /// <summary>
        /// Called when button becomes unpressed (no objects on it).
        /// </summary>
        private void OnButtonUnpressed()
        {
            // Close all assigned doors
            if (doors != null)
            {
                foreach (Door door in doors)
                {
                    if (door != null)
                    {
                        door.Close();
                    }
                }
            }
        }
        
        /// <summary>
        /// Smoothly animates the button child moving down/up.
        /// </summary>
        private void Update()
        {
            if (buttonChild == null) return;
            
            // Remove null references and held objects from tracking
            _objectsOnButton.RemoveWhere(collider => 
                collider == null || 
                collider.gameObject == null || 
                PlayerPickup.IsObjectHeld(collider.gameObject)
            );
            
            // Update state if we removed objects
            bool shouldBePressed = _objectsOnButton.Count > 0;
            if (shouldBePressed != isPressed)
            {
                UpdateButtonState();
            }
            
            // Animate button child movement
            Vector3 targetPosition = isPressed ? _buttonChildPressedPosition : _buttonChildOriginalPosition;
            Vector3 currentPosition = buttonChild.transform.localPosition;
            
            // Smoothly move towards target position
            float distance = Vector3.Distance(currentPosition, targetPosition);
            if (distance > 0.001f)
            {
                float moveDistance = pressSpeed * Time.deltaTime;
                buttonChild.transform.localPosition = Vector3.MoveTowards(currentPosition, targetPosition, moveDistance);
            }
        }
        
        private void OnDestroy()
        {
            // Clean up ButtonTrigger if it exists
            if (_buttonTrigger != null)
            {
                _buttonTrigger.Cleanup();
            }
        }
    }
    
    /// <summary>
    /// Helper component attached to the button child to handle collision detection.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ButtonTrigger : MonoBehaviour
    {
        private Button _parentButton;
        
        public void Initialize(Button parentButton)
        {
            _parentButton = parentButton;
        }
        
        public void Cleanup()
        {
            _parentButton = null;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (_parentButton == null) return;
            if (other == null) return;
            
            // Only react to interactable objects, NOT the player
            if (other.CompareTag("Player"))
            {
                return; // Ignore player
            }
            
            // Don't react to held objects
            if (PlayerPickup.IsObjectHeld(other.gameObject))
            {
                return;
            }
            
            // Check if it's an interactable object (by tag or component)
            bool isInteractable = other.CompareTag("Interactable") || 
                                  other.GetComponent<InteractableObject>() != null;
            
            if (!isInteractable)
            {
                return; // Not an interactable object
            }
            
            // Notify parent button
            _parentButton.OnObjectEnter(other);
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (_parentButton == null) return;
            if (other == null) return;
            
            // Notify parent button
            _parentButton.OnObjectExit(other);
        }
    }
}
