using UnityEngine;

namespace Interact
{
    /// <summary>
    /// Radio interactable object that can be picked up and destroyed.
    /// Extends InteractableObject to inherit all pickup/drop/portal functionality.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Radio : InteractableObject
    {
        [Header("Radio Settings")]
        [Tooltip("Sound effect to play when radio is picked up (optional)")]
        public AudioClip pickupSound;
        
        [Tooltip("Sound effect to play when radio is dropped (optional)")]
        public AudioClip dropSound;
        
        [Tooltip("Sound effect to play when radio is destroyed (optional)")]
        public AudioClip destroySound;
        
        private AudioSource _audioSource;

        protected override void Awake()
        {
            // CRITICAL: Call base Awake() to initialize Rigidbody, Collider, and other InteractableObject components
            base.Awake();
            
            // Setup audio source if any sounds are provided
            if (pickupSound != null || dropSound != null || destroySound != null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 1f; // 3D sound
                }
            }
        }

        /// <summary>
        /// Called when radio is picked up - plays pickup sound
        /// </summary>
        public new void OnPickedUp(PlayerPickup holder)
        {
            base.OnPickedUp(holder);
            
            // Play pickup sound
            if (_audioSource != null && pickupSound != null)
            {
                _audioSource.PlayOneShot(pickupSound);
            }
        }

        /// <summary>
        /// Called when radio is dropped - plays drop sound
        /// </summary>
        public new void OnDropped()
        {
            base.OnDropped();
            
            // Play drop sound
            if (_audioSource != null && dropSound != null)
            {
                _audioSource.PlayOneShot(dropSound);
            }
        }

        /// <summary>
        /// Called when radio is about to be destroyed - notifies RadioCounter
        /// Called before destroying the radio
        /// </summary>
        public void OnDestroyed()
        {
            // Play destroy sound
            if (_audioSource != null && destroySound != null)
            {
                _audioSource.PlayOneShot(destroySound);
            }
            
            // Notify RadioCounter that this radio was destroyed
            RadioCounter.Instance?.OnRadioDestroyed((Interact.Radio)this);
        }
    }
}
