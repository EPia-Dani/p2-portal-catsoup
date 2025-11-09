using System;
using Interact;
using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    public Animator animator;
    public bool isPressed;
    public SpawnerManager spawner;
    
    private void OnTriggerEnter(Collider interactable)
    {
        if (isPressed) return;
        if(!interactable.CompareTag("Interactable")) return;
        isPressed = true;
        
        if(animator != null)
        {
            animator.SetTrigger("ButtonPressed");
            Debug.Log("Button pressed, playing animation.");
            spawner.performAction();
        }
        else
        { 
            Debug.LogWarning("Animator not assigned on ButtonManager.");
        }
        
    }
    private void OnTriggerExit(Collider interactable)
    {
        if(!interactable.CompareTag("Interactable")) return;
        isPressed = false;
    }
}