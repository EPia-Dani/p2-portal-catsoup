using System;
using System.Collections.Generic;
using UnityEngine;

namespace Interact
{
    /// <summary>
    /// Defines all button event names and manages button events.
    /// </summary>
    public static class ButtonEvents
    {
        /// <summary>
        /// Event names for button interactions.
        /// </summary>
        public static class EventNames
        {
            public const string ButtonPressed = "ButtonPressed";
            public const string ButtonUnpressed = "ButtonUnpressed";
        }
        
        // Dictionary to store event handlers by event name
        private static Dictionary<string, Action<Button>> _eventHandlers = new Dictionary<string, Action<Button>>();
        
        /// <summary>
        /// Subscribe to a button event by name.
        /// </summary>
        /// <param name="eventName">The name of the event to subscribe to</param>
        /// <param name="handler">The method to call when the event is triggered</param>
        public static void Subscribe(string eventName, Action<Button> handler)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogWarning("[ButtonEvents] Cannot subscribe to null or empty event name.");
                return;
            }
            
            if (handler == null)
            {
                Debug.LogWarning("[ButtonEvents] Cannot subscribe with null handler.");
                return;
            }
            
            if (!_eventHandlers.ContainsKey(eventName))
            {
                _eventHandlers[eventName] = null;
            }
            
            _eventHandlers[eventName] += handler;
        }
        
        /// <summary>
        /// Unsubscribe from a button event by name.
        /// </summary>
        /// <param name="eventName">The name of the event to unsubscribe from</param>
        /// <param name="handler">The method to remove from the event</param>
        public static void Unsubscribe(string eventName, Action<Button> handler)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }
            
            if (!_eventHandlers.ContainsKey(eventName))
            {
                return;
            }
            
            _eventHandlers[eventName] -= handler;
        }
        
        /// <summary>
        /// Invoke an event by name.
        /// </summary>
        /// <param name="eventName">The name of the event to invoke</param>
        /// <param name="button">The button that triggered the event</param>
        public static void Invoke(string eventName, Button button)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }
            
            if (!_eventHandlers.ContainsKey(eventName))
            {
                return;
            }
            
            _eventHandlers[eventName]?.Invoke(button);
        }
        
        /// <summary>
        /// Clear all event subscriptions (useful for cleanup).
        /// </summary>
        public static void ClearAll()
        {
            _eventHandlers.Clear();
        }
    }
}
