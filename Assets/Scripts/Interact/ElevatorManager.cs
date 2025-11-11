using UnityEngine;

public class ElevatorManager : MonoBehaviour
{
    public Animator animator;

    public void OnTriggerEnter(Collider interactable)
    {
        if (interactable == null) return;
        if (!interactable.CompareTag("Player"))return;
        
        if (animator != null)
        {
            animator.SetTrigger("DoorCrossed");
            Debug.Log("Elevator action performed, playing animation.");
        }
        else
        {
            Debug.LogWarning("Animator not assigned on ElevatorManager.");
        }
    }
}
