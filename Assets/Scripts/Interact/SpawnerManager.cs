using UnityEngine;

namespace Interact
{
    public class SpawnerManager : MonoBehaviour
    {
        public Animator animator;
        public void performAction()
        {
                if(animator != null)
                {
                    animator.SetTrigger("Spawner");
                    Debug.Log("Spawner action performed, playing animation.");
                }
                else
                { 
                    Debug.LogWarning("Animator not assigned on ButtonManager.");
                }
                
                Vector3 locaclPos = transform.localPosition;
                locaclPos.x = 0.0f;
                transform.localPosition = locaclPos;
                Debug.Log("Spawner action completed, resetting position.");
        }
    }
}
