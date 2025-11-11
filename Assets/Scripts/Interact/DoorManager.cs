using System;
using UnityEngine;

namespace Interact
{
    public class DoorManager : MonoBehaviour, IActionManager
    {
        [Header("Door Settings")]
        public Animator animator;
        
        [Header("Event Subscription")]
        [Tooltip("Event name to listen for button press. Use ButtonEvents.EventNames constants.")]
        public string pressedEventName = ButtonEvents.EventNames.ButtonPressed;
        
        [Tooltip("Event name to listen for button unpress. Use ButtonEvents.EventNames constants.")]
        public string unpressedEventName = ButtonEvents.EventNames.ButtonUnpressed;
        
        private void OnEnable()
        {
            // Subscribe to button events
            ButtonEvents.Subscribe(pressedEventName, OnButtonPressed);
            ButtonEvents.Subscribe(unpressedEventName, OnButtonUnpressed);
        }
        
        private void OnDisable()
        {
            // Unsubscribe from button events
            ButtonEvents.Unsubscribe(pressedEventName, OnButtonPressed);
            ButtonEvents.Unsubscribe(unpressedEventName, OnButtonUnpressed);
        }
        
        /// <summary>
        /// Called when a button press event is received.
        /// </summary>
        private void OnButtonPressed(Button button)
        {
            performAction();
        }
        
        /// <summary>
        /// Called when a button unpress event is received.
        /// </summary>
        private void OnButtonUnpressed(Button button)
        {
            ResetPosition();
        }

        public void performAction()
        {
            if (animator != null)
            {
                animator.SetTrigger("ButtonPressed");
                Debug.Log("Door action performed, playing animation.");
            }
        }

        public void ResetPosition()
        {
            if (animator != null)
            {
                animator.SetTrigger("ButtonUnpressed");
            }
        }
    }
}