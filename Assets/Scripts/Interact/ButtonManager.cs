using System;
using Interact;
using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    public IActionManager actionObject;
    public Animator animator;
    private bool isPressed, wasPressed;
    
    private void OnTriggerEnter(Collider interactable)
    {
        if (isPressed) return;
        if(!interactable.CompareTag("Interactable")) return;
        
        isPressed = true;
        if(animator != null)
        {
            animator.SetTrigger("ButtonPressed");
            Debug.Log("Button pressed, playing animation.");
        }
        else
        { 
            Debug.LogWarning("Animator not assigned on ButtonManager.");
        }
        actionObject?.performAction();
        wasPressed = true;
    }
    private void OnTriggerExit(Collider interactable)
    {
        if(!interactable.CompareTag("Interactable")) return;
        isPressed = false;
    }
}