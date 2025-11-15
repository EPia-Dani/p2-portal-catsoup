using UnityEngine;
using System.Collections;

namespace Interact
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("If true, the checkpoint will only trigger for objects tagged 'Player'. Otherwise it will try to find an FPSController on the colliding object.")]
        // Default to false so checkpoints work even if the player GameObject isn't tagged.
        public bool requirePlayerTag = false;

        [Tooltip("Optional: disable the checkpoint after it's been activated once.")]
        public bool singleUse = true;

        [Header("Debug")]
        public bool showDebug = true;

        private Collider _collider;
        private bool _activated;
        // If the player was already inside this checkpoint at scene start, we should not
        // treat that as an activation. We wait for the player to exit and re-enter.
        private bool _playerStartedInside;

        private void Start()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null && !_collider.isTrigger)
                _collider.isTrigger = true;

            // Detect if player spawns inside this checkpoint to avoid auto-activating it
            var playerController = FindObjectOfType<FPSController>();
            if (playerController != null && _collider != null)
            {
                // Use bounds containment as a simple check
                if (_collider.bounds.Contains(playerController.transform.position))
                {
                    _playerStartedInside = true;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_activated && singleUse) return;

            if (other == null) return;

            // debug: report who entered
            if (showDebug)
            {
                Debug.Log($"Checkpoint '{name}': OnTriggerEnter by '{other.name}' (tag='{other.tag}')", this);
            }

            // If the player started inside the trigger, ignore the first Enter for that player.
            if (_playerStartedInside)
            {
                // If this enter is caused by the player, flip the flag and ignore activation.
                var fps = other.GetComponentInParent<FPSController>();
                if (fps != null)
                {
                    _playerStartedInside = false;
                    if (showDebug) Debug.Log($"Checkpoint '{name}': player started inside; ignoring initial enter.", this);
                    return;
                }
            }

            // Allow either tag-based or component-based detection
            bool isPlayer = !requirePlayerTag && other.GetComponentInParent<FPSController>() != null;
            if (requirePlayerTag)
            {
                isPlayer = other.CompareTag("Player") || other.GetComponentInParent<FPSController>() != null;
            }

            if (!isPlayer)
            {
                if (showDebug) Debug.Log($"Checkpoint '{name}': OnTriggerEnter by '{other.name}' is not player; ignoring.", this);
                return;
            }

            // Try to set respawn point on PlayerManager; be tolerant of load-order by trying to find a manager if Instance is null
            if (PlayerManager.Instance != null)
            {
                if (showDebug) Debug.Log($"Checkpoint '{name}': Activating checkpoint for player and setting respawn point to this transform.", this);
                PlayerManager.Instance.SetRespawnPoint(transform, true);
                if (showDebug) Debug.Log($"Checkpoint: Activated checkpoint '{gameObject.name}' and enabled respawn at this point.");
                _activated = true;
                if (singleUse && _collider != null)
                    _collider.enabled = false;
            }
            else
            {
                // Attempt to find an existing PlayerManager in the scene and call SetRespawnPoint on it.
                var pm = FindObjectOfType<PlayerManager>();
                if (pm != null)
                {
                    if (showDebug) Debug.Log($"Checkpoint '{name}': Found PlayerManager instance in scene (no singleton). Setting respawn point.", this);
                    pm.SetRespawnPoint(transform, true);
                    _activated = true;
                    if (singleUse && _collider != null)
                        _collider.enabled = false;
                }
                else
                {
                    // If no PlayerManager exists yet (scene load ordering), schedule a retry next frame.
                    if (showDebug) Debug.LogWarning($"Checkpoint '{name}': PlayerManager not found; will retry setting respawn point next frame.", this);
                    StartCoroutine(DeferredSetRespawnPoint());
                }
            }
        }

        private IEnumerator DeferredSetRespawnPoint()
        {
            // Wait one frame to allow managers to initialize
            yield return null;

            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.SetRespawnPoint(transform, true);
                if (showDebug) Debug.Log($"Checkpoint '{name}': Deferred activation succeeded (PlayerManager singleton now available).", this);
                _activated = true;
                if (singleUse && _collider != null)
                    _collider.enabled = false;
                yield break;
            }

            var pm = FindObjectOfType<PlayerManager>();
            if (pm != null)
            {
                pm.SetRespawnPoint(transform, true);
                if (showDebug) Debug.Log($"Checkpoint '{name}': Deferred activation succeeded (found PlayerManager in scene).", this);
                _activated = true;
                if (singleUse && _collider != null)
                    _collider.enabled = false;
                yield break;
            }

            if (showDebug) Debug.LogWarning($"Checkpoint '{name}': Deferred activation failed - PlayerManager still not found.", this);
        }
    }
}
