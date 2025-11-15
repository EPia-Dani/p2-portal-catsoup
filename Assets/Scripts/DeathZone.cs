using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("Only trigger death for objects with this tag. Leave empty to trigger for any object.")]
    public string targetTag = "Player";
    
    [Tooltip("Delay before triggering death (in seconds)")]
    public float deathDelay;
    
    private void Start()
    {
        // Ensure collider is set as trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"DeathZone on {gameObject.name}: Collider is not set as trigger. Setting it now.");
            col.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if we should trigger for this object
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }
        
        // Try to kill the player via FPSController if present on the collider or its parents
        FPSController fps = other.GetComponentInParent<FPSController>();
        if (fps != null)
        {
            if (deathDelay > 0f)
            {
                StartCoroutine(DelayedKillFPS(fps, deathDelay));
            }
            else
            {
                fps.KillFromExternal();
            }
            return;
        }
        
        // Fallback: use PlayerManager if it exists
        if (deathDelay > 0f)
        {
            Invoke(nameof(TriggerDeath), deathDelay);
        }
        else
        {
            TriggerDeath();
        }
    }

    private IEnumerator DelayedKillFPS(FPSController fps, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fps != null)
            fps.KillFromExternal();
    }
    
    private void TriggerDeath()
    {
        // First try PlayerManager singleton
        PlayerManager pm = PlayerManager.Instance;
        if (pm != null)
        {
            pm.OnPlayerDeath();
            return;
        }

        // Next try to find an FPSController in the scene
        FPSController fps = FindFirstObjectByType<FPSController>();
        if (fps != null)
        {
            fps.KillFromExternal();
            return;
        }

        Debug.LogError("DeathZone: No PlayerManager or FPSController found in scene! Cannot trigger death.");
    }
}
