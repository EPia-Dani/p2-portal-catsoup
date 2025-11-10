using System;
using UnityEngine;

namespace Interact
{
    public class DoorManager : MonoBehaviour, IActionManager
    {
        public Animator animator;

        [Header("Animator parameters (optional)")]
        [Tooltip("Trigger to set when door should open (used by performAction)")]
        public string pressTriggerName = "ButtonPressed";
        [Tooltip("Trigger to set when door should close (used by resetPosition)")]
        public string releaseTriggerName = "ButtonUnpressed";
        [Tooltip("Boolean parameter name to represent open state (set false on reset if present)")]
        public string closeBoolName = "";
        [Tooltip("Animator state or clip name expected for closed/idle pose (used as fallback)")]
        public string closedStateName = "Closed";

        private Vector3 initialPosition;

        // If we disable the animator to force the door closed, remember it so we can re-enable on open
        private bool _animatorDisabledByReset = false;

        private void Start()
        {
            // store full initial local position so we can restore it later
            initialPosition = transform.localPosition;
        }

        // Explicitly open the door (preferred API for ButtonManager)
        public void Open()
        {
            if (animator != null)
            {
                // Re-enable animator if we disabled it on reset
                if (_animatorDisabledByReset)
                {
                    animator.enabled = true;
                    _animatorDisabledByReset = false;
                }

                // Prefer bool if available so animator remains open
                if (!string.IsNullOrEmpty(closeBoolName) && HasAnimatorParameter(animator, closeBoolName, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(closeBoolName, true);
                    if (!string.IsNullOrEmpty(pressTriggerName) && HasAnimatorParameter(animator, pressTriggerName, AnimatorControllerParameterType.Trigger))
                    {
                        animator.SetTrigger(pressTriggerName);
                    }
                    return;
                }

                // Fallback to trigger
                if (!string.IsNullOrEmpty(pressTriggerName) && HasAnimatorParameter(animator, pressTriggerName, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(pressTriggerName);
                }
                else
                {
                    animator.SetTrigger("ButtonPressed");
                }
            }
            else
            {
                Debug.LogWarning("Animator not assigned on DoorManager (Open()).");
            }
        }

        // Explicitly close the door (preferred API for ButtonManager)
        public void Close()
        {
            // Use resetPosition behaviour which attempts triggers, bools, states, or disables animator
            resetPosition();
        }

        public void performAction()
        {
            if (animator != null)
            {
                // If we previously disabled the animator to keep the door closed, re-enable it now
                if (_animatorDisabledByReset)
                {
                    animator.enabled = true;
                    _animatorDisabledByReset = false;
                }

                // Prefer to use a boolean parameter to represent the open state so the animator will remain open
                if (!string.IsNullOrEmpty(closeBoolName) && HasAnimatorParameter(animator, closeBoolName, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(closeBoolName, true);
                    if (!string.IsNullOrEmpty(pressTriggerName) && HasAnimatorParameter(animator, pressTriggerName, AnimatorControllerParameterType.Trigger))
                    {
                        // also set open trigger if it exists to play opening animation
                        animator.SetTrigger(pressTriggerName);
                    }
                    Debug.Log("Door performAction: set open bool parameter.");
                    return;
                }

                if (!string.IsNullOrEmpty(pressTriggerName) && HasAnimatorParameter(animator, pressTriggerName, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(pressTriggerName);
                }
                else
                {
                    animator.SetTrigger("ButtonPressed");
                }

                Debug.Log("Door action performed, playing animation.");
            }
            else
            {
                Debug.LogWarning("Animator not assigned on DoorManager.");
            }
        }

        public void resetPosition()
        {
            // If there's an Animator, try to return it to a closed state first.
            if (animator != null)
            {
                // 1) If a specific release trigger is provided and exists, use it
                if (!string.IsNullOrEmpty(releaseTriggerName) && HasAnimatorParameter(animator, releaseTriggerName, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(pressTriggerName);
                    animator.SetTrigger(releaseTriggerName);
                    animator.Update(0f);
                    Debug.Log("Door reset by setting release trigger.");
                    return;
                }

                // 2) If a close boolean parameter exists, set it false
                if (!string.IsNullOrEmpty(closeBoolName) && HasAnimatorParameter(animator, closeBoolName, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(closeBoolName, false);
                    animator.Update(0f);
                    Debug.Log("Door reset by clearing close bool parameter.");
                    return;
                }

                // 3) Try to play a closed state by name
                if (!string.IsNullOrEmpty(closedStateName) && TryPlayAnimatorState(closedStateName))
                {
                    animator.Update(0f);
                    Debug.Log("Door reset by playing closed state.");
                    return;
                }

                // 4) fallback: temporarily disable the animator so we can set the transform directly
                // Disable the animator and KEEP it disabled so its transitions won't reopen the door.
                animator.enabled = false;
                _animatorDisabledByReset = true;

                // set position manually
                SetLocalZ(initialPosition.z);

                Debug.Log("Door reset to closed position (animator disabled until next open).");
                return;
            }

            // No animator -- just set the local position
            SetLocalZ(initialPosition.z);
            Debug.Log("Door reset to closed position.");
        }

        private void SetLocalZ(float z)
        {
            Vector3 localPos = transform.localPosition;
            localPos.z = z;
            transform.localPosition = localPos;
        }

        // Try to play a state by name (best-effort using animation clip names / states)
        private bool TryPlayAnimatorState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            // Quick check: if the controller has an animation clip with this name, play the state
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == stateName)
                {
                    animator.Play(stateName, 0, 0f);
                    return true;
                }
            }

            // As a secondary attempt, try Play(stateName) anyway in case the state uses the same name
            try
            {
                animator.Play(stateName, 0, 0f);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool HasAnimatorParameter(Animator anim, string name, AnimatorControllerParameterType type)
        {
            if (anim == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in anim.parameters)
            {
                if (p.name == name && p.type == type) return true;
            }
            return false;
        }
    }
}