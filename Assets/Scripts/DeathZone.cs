using UnityEngine;

/// <summary>
/// Death zone that triggers when the player enters it.
/// Requires a Collider component set as a trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    [Header("Death Zone Settings")]
    [Tooltip("Only trigger death for objects with this tag. Leave empty to trigger for any object.")]
    public string targetTag = "Player";
    
    [Tooltip("Delay before triggering death (in seconds)")]
    public float deathDelay = 0f;
    
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
        
        // Check if this is the player
        FPSController player = other.GetComponent<FPSController>();
        if (player != null)
        {
            if (deathDelay > 0f)
            {
                Invoke(nameof(TriggerDeath), deathDelay);
            }
            else
            {
                TriggerDeath();
            }
        }
    }
    
    private void TriggerDeath()
    {
        // Find PlayerManager and trigger death
        PlayerManager playerManager = FindFirstObjectByType<PlayerManager>();
        if (playerManager != null)
        {
            playerManager.OnPlayerDeath();
        }
        else
        {
            Debug.LogError("DeathZone: PlayerManager not found in scene! Cannot trigger death.");
        }
    }
}






