using System;
using System.Collections.Generic;
using UnityEngine;

namespace Interact
{
    public class ButtonManager : MonoBehaviour
    {
        public Animator animator;
        public bool isPressed;
        public SpawnerManager spawner;
        public DoorManager leftDoor;
        public DoorManager rightDoor;

        [Tooltip("Tag used to identify objects that can press the button")]
        public string interactableTag = "Interactable";

        [Tooltip("Enable to print detailed debug logs for collider enter/exit events")]
        public bool debugLogs = false;

        [Tooltip("Delay (seconds) after the last exiting collider before the button truly releases. Helps debounce transient exit events.")]
        [SerializeField]
        private float releaseDelay = 0.2f;

        // remember which GameObject (root) actually pressed the button
        private GameObject currentInteractorObject;
        // exact colliders from that interactor currently overlapping this trigger
        private HashSet<Collider> currentInteractorColliders = new HashSet<Collider>();

        // cached animator hashes to avoid repeated string lookups
        private int _pressHash;
        private int _unpressHash;

        // release coroutine handle
        private Coroutine _releaseCoroutine;

        private void Awake()
        {
            _pressHash = Animator.StringToHash("ButtonPressed");
            _unpressHash = Animator.StringToHash("ButtonUnpressed");
        }

        private void OnTriggerEnter(Collider interactable)
        {
            if (interactable == null) return;
            if (!interactable.CompareTag(interactableTag)) return;

            // Prefer the collider's attachedRigidbody root if available (more accurate than transform.root)
            GameObject root = interactable.attachedRigidbody != null ? interactable.attachedRigidbody.gameObject : interactable.transform.root.gameObject;

            if (currentInteractorObject == null)
            {
                // first collider from a pressing object
                currentInteractorObject = root;
                currentInteractorColliders.Clear();
                currentInteractorColliders.Add(interactable);

                // any pending release should be cancelled when something re-enters
                CancelPendingRelease();

                if (!isPressed)
                {
                    isPressed = true;
                    if (animator != null) animator.SetTrigger(_pressHash);
                    if (debugLogs) Debug.Log($"Button pressed by '{currentInteractorObject.name}'.");

                    spawner?.performAction();
                    // Use explicit door API to avoid races with animator state
                    leftDoor?.Open();
                    rightDoor?.Open();
                }
            }
            else if (root == currentInteractorObject)
            {
                // additional collider from same interactor entered
                if (currentInteractorColliders.Add(interactable))
                {
                    // cancel pending release if additional collider re-enters
                    CancelPendingRelease();

                    if (debugLogs) Debug.Log($"Added collider '{interactable.name}' from '{root.name}', count={currentInteractorColliders.Count}.");
                }
            }
            else
            {
                // Another object touched the button while it's pressed by currentInteractorObject - ignore
                if (debugLogs) Debug.Log($"Ignored collider '{interactable.name}' from '{root.name}' while pressed by '{currentInteractorObject.name}'.");
            }
        }

        private void OnTriggerExit(Collider interactable)
        {
            if (interactable == null) return;
            if (currentInteractorObject == null) return; // nothing to release

            GameObject root = interactable.attachedRigidbody != null ? interactable.attachedRigidbody.gameObject : interactable.transform.root.gameObject;

            // If the exiting collider is not from the object that pressed the button, ignore
            if (root != currentInteractorObject) return;

            // Remove this specific collider from the tracked set
            bool removed = currentInteractorColliders.Remove(interactable);
            if (debugLogs) Debug.Log($"Collider '{interactable.name}' exited from '{root.name}', removed={removed}, remaining={currentInteractorColliders.Count}.");

            if (currentInteractorColliders.Count == 0)
            {
                // fully stopped colliding — start debounced release, actual clearing happens in DoRelease()
                if (debugLogs) Debug.Log("All colliders exited; starting debounced release.");
                StartPendingRelease();
            }
        }

        private void StartPendingRelease()
        {
            // cancel existing coroutine if any
            CancelPendingRelease();
            _releaseCoroutine = StartCoroutine(DelayedReleaseCoroutine());
        }

        private void CancelPendingRelease()
        {
            if (_releaseCoroutine != null)
            {
                StopCoroutine(_releaseCoroutine);
                _releaseCoroutine = null;
                if (debugLogs) Debug.Log("Pending release cancelled.");
            }
        }

        private System.Collections.IEnumerator DelayedReleaseCoroutine()
        {
            if (releaseDelay > 0f) yield return new WaitForSeconds(releaseDelay);
            // If any collider re-entered meanwhile, abort
            if (currentInteractorColliders.Count > 0)
            {
                if (debugLogs) Debug.Log("Release aborted: collider re-entered during delay.");
                _releaseCoroutine = null;
                yield break;
            }

            _releaseCoroutine = null;
            DoRelease();
        }

        // Centralized release logic (runs after debounce or force release)
        private void DoRelease()
        {
            // Clear pressed state and recorded interactor now that release is confirmed
            currentInteractorObject = null;
            if (!isPressed && currentInteractorColliders.Count == 0) {
                // nothing to do
            }
            isPressed = false;

            if (animator != null)
            {
                animator.ResetTrigger(_pressHash);
                animator.SetTrigger(_unpressHash);
            }
 
            ResetPosition();
 
            // --- Door reset diagnostics & fallbacks ---
            if (leftDoor != null)
            {
                try { leftDoor.Close(); if (debugLogs) Debug.Log("Called leftDoor.Close()."); }
                catch (Exception e) { Debug.LogError($"Exception while calling leftDoor.Close(): {e}"); }
            }
            else
            {
                DoorManager found = GetComponentInParent<DoorManager>() ?? GetComponent<DoorManager>();
                if (found == null) found = FindObjectOfType<DoorManager>();
                if (found != null)
                {
                    leftDoor = found;
                    try { leftDoor.Close(); Debug.Log("Auto-assigned and called leftDoor.Close()."); }
                    catch (Exception e) { Debug.LogError($"Exception while auto-calling leftDoor.Close(): {e}"); }
                }
                else { Debug.LogWarning("leftDoor is null and no DoorManager was found automatically."); }
            }
 
            if (rightDoor != null)
            {
                try { rightDoor.Close(); if (debugLogs) Debug.Log("Called rightDoor.Close()."); }
                catch (Exception e) { Debug.LogError($"Exception while calling rightDoor.Close(): {e}"); }
            }
            else
            {
                DoorManager foundR = GetComponentInParent<DoorManager>() ?? GetComponent<DoorManager>();
                if (foundR == null) foundR = FindObjectOfType<DoorManager>();
                if (foundR != null)
                {
                    rightDoor = foundR;
                    try { rightDoor.Close(); Debug.Log("Auto-assigned and called rightDoor.Close()."); }
                    catch (Exception e) { Debug.LogError($"Exception while auto-calling rightDoor.Close(): {e}"); }
                }
                else { Debug.LogWarning("rightDoor is null and no DoorManager was found automatically."); }
            }
 
            if (debugLogs) Debug.Log("Button fully released (after debounce).");
        }

        private void OnDisable()
        {
            // If the object is disabled while pressed, force a clean release
            if (isPressed)
            {
                ForceRelease();
                if (debugLogs) Debug.Log("Button reset on disable.");
            }
        }

        private void ForceRelease()
        {
            currentInteractorObject = null;
            currentInteractorColliders.Clear();
            CancelPendingRelease();

            if (!isPressed) return;
            isPressed = false;

            // Force immediate release behavior
            DoRelease();
        }
 
        private void ResetPosition()
        {
            Vector3 localPos = transform.localPosition;
            localPos.y = 0.0f;
            transform.localPosition = localPos;
            if (debugLogs) Debug.Log("Button released, resetting position.");
        }
    }
}
