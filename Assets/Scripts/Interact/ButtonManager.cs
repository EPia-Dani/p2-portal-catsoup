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
        

        private void OnTriggerEnter(Collider interactable)
        {
            if (interactable == null) return;
            if (interactable.CompareTag("Interactable") || interactable.CompareTag("Player"))
            {
                isPressed = true; 
                if (animator != null)
                {
                    animator.SetTrigger("ButtonPressed");
                    spawner?.performAction();
                    leftDoor?.performAction();
                    rightDoor?.performAction();   
                }
            }
        }

        private void OnTriggerExit(Collider interactable)
        {
            if (interactable == null) return;
            isPressed = false;
            if (animator != null)
            {
                animator.SetTrigger("ButtonUnpressed");
                leftDoor.ResetPosition();
                rightDoor.ResetPosition();
            }
            
                
        }
    }
}
