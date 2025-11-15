using UnityEngine;

namespace Interact
{
    /// <summary>
    /// A simple floor button that animates when pressed.
    /// Use UnityEvents (e.g., from ScriptableTrigger) to call SetPressed() and SetUnpressed().
    /// </summary>
    public class Button : MonoBehaviour
    {
        [Header("Button Parts")]
        [Tooltip("The child GameObject that represents the button part that moves.")]
        public GameObject buttonChild;
        
        [Header("Button Settings")]
        [Tooltip("How far down the button moves when pressed (in local Y units).")]
        public float pressDistance = 0.2f;

        [Header("Audio")]
        [Tooltip("Sound played when the button is pressed.")]
        [SerializeField] AudioClip buttonPressedClip;
        
        [Tooltip("Speed at which the button moves down/up (units per second).")]
        public float pressSpeed = 2f;
        
        [Header("Debug")]
        [Tooltip("Current pressed state of the button.")]
        public bool isPressed = false;
        
        // Button child movement
        private Vector3 _buttonChildOriginalPosition;
        private Vector3 _buttonChildPressedPosition;
        
        private void Start()
        {
            // Find button child if not assigned
            if (buttonChild == null)
            {
                // Try to find first child
                if (transform.childCount > 0)
                {
                    buttonChild = transform.GetChild(0).gameObject;
                }
            }
            
            if (buttonChild == null)
            {
                Debug.LogError($"[Button] {gameObject.name}: Button child not assigned! Please assign the child GameObject.");
                enabled = false;
                return;
            }
            
            // Store original position
            _buttonChildOriginalPosition = buttonChild.transform.localPosition;
            _buttonChildPressedPosition = _buttonChildOriginalPosition + Vector3.down * pressDistance;
        }
        
        /// <summary>
        /// Sets the button to pressed state. Call this from UnityEvents (e.g., ScriptableTrigger.onEnter).
        /// </summary>
        public void SetPressed()
        {
            isPressed = true;
            // Play press sound at the button's position
            if (buttonPressedClip != null) AudioSource.PlayClipAtPoint(buttonPressedClip, transform.position);
        }
        
        /// <summary>
        /// Sets the button to unpressed state. Call this from UnityEvents (e.g., ScriptableTrigger.onExit).
        /// </summary>
        public void SetUnpressed()
        {
            isPressed = false;
        }
        
        /// <summary>
        /// Smoothly animates the button child moving down/up.
        /// </summary>
        private void Update()
        {
            if (buttonChild == null) return;
            
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
    }
}
