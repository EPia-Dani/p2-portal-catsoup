using System;
using UnityEngine;

namespace Interact
{
    public class DoorManager : MonoBehaviour, IActionManager
    {
        public Animator animator;

        public void performAction()
        {
            animator.SetTrigger("ButtonPressed");
            Debug.Log("Door action performed, playing animation.");
        }

        public void ResetPosition()
        {
            animator.SetTrigger("ButtonUnpressed");
        }
    }
}