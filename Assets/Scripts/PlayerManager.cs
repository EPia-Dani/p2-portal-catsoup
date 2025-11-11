using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages player state, death, and respawn functionality.
/// Should be placed in the scene as a singleton.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("Reference to the player GameObject. If null, will search for FPSController.")]
    public GameObject player;
    
    [Header("Respawn Settings")]
    [Tooltip("Starting position for respawn. If null, uses player's initial position.")]
    public Transform respawnPoint;
    
    [Tooltip("Should the player respawn at the respawn point or restart the scene?")]
    public bool respawnAtPoint = true;

    [Header("Player Reference")]
    [Tooltip("Reference to the player GameObject. If null, will search for FPSController.")]
    public DeathScreenManager deathScreen;
    
    private FPSController fpsController;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isDead = false;
    
    // Singleton instance
    private static PlayerManager instance;
    public static PlayerManager Instance => instance;
    
    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    
    private void Start()
    {
        // Find player if not assigned
        if (player == null)
        {
            FPSController controller = FindFirstObjectByType<FPSController>();
            if (controller != null)
            {
                player = controller.gameObject;
            }
            else
            {
                Debug.LogError("PlayerManager: No player found! Please assign player reference.");
                return;
            }
        }
        
        fpsController = player.GetComponent<FPSController>();
        if (fpsController == null)
        {
            Debug.LogError("PlayerManager: Player GameObject does not have FPSController component!");
            return;
        }
        
        // Store initial position and rotation
        initialPosition = player.transform.position;
        initialRotation = player.transform.rotation;
        
        // Set respawn point if not assigned
        if (respawnPoint == null)
        {
            GameObject respawnObj = new GameObject("RespawnPoint");
            respawnObj.transform.position = initialPosition;
            respawnObj.transform.rotation = initialRotation;
            respawnPoint = respawnObj.transform;
        }
    }
    
    /// <summary>
    /// Called when the player dies. Triggers death screen and disables player control.
    /// </summary>
    public void OnPlayerDeath()
    {
        if (isDead)
        {
            return; // Already dead, prevent multiple death triggers
        }
        
        isDead = true;
        Debug.Log("PlayerManager: Player has died!");
        
       
        
        deathScreen.ShowDeathScreen();
        
    }
    
    /// <summary>
    /// Respawns the player at the respawn point with fade transition.
    /// </summary>
    public void RespawnPlayer()
    {
        if (!isDead)
        {
            return; // Not dead, can't respawn
        }
        
        Debug.Log("PlayerManager: Respawning player...");
        
        // Use fade system if available
        var fadeManager = ScreenFadeManager.Instance;
        if (fadeManager != null)
        {
            fadeManager.FadeOutAndRespawn(() => {
                // Reset player position and rotation
                if (respawnAtPoint && respawnPoint != null)
                {
                    player.transform.position = respawnPoint.position;
                    player.transform.rotation = respawnPoint.rotation;
                }
                
                // Reset velocity if rigidbody exists
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                
                isDead = false;
            });
        }
        else
        {
            // Fallback: direct respawn if fade manager doesn't exist
            if (respawnAtPoint && respawnPoint != null)
            {
                player.transform.position = respawnPoint.position;
                player.transform.rotation = respawnPoint.rotation;
            }
            
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            isDead = false;
        }
    }
    
    /// <summary>
    /// Restarts the current scene with fade transition.
    /// </summary>
    public void RestartScene()
    {
        Debug.Log("PlayerManager: Restarting scene...");
        GameSceneManager.ReloadCurrentScene();
    }
  
   
    
    /// <summary>
    /// Sets a new respawn point.
    /// </summary>
    public void SetRespawnPoint(Transform newRespawnPoint)
    {
        respawnPoint = newRespawnPoint;
    }
    
    /// <summary>
    /// Checks if the player is currently dead.
    /// </summary>
    public bool IsDead => isDead;
}

