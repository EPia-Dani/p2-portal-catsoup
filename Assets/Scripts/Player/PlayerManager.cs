using UnityEngine;
using UnityEngine.SceneManagement;


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
        
        // Try to auto-find death screen if not set
        if (deathScreen == null)
        {
            deathScreen = FindFirstObjectByType<DeathScreenManager>();
            if (deathScreen == null)
            {
                Debug.LogWarning("PlayerManager: No DeathScreenManager found. Will fallback to instant respawn on death.");
            }
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

        // Disable player control while dead
        if (fpsController != null)
        {
            fpsController.SetDisabled(true);
        }
        
        // Show death screen if available; otherwise fallback to instant respawn
        if (deathScreen != null)
        {
            deathScreen.ShowDeathScreen();
        }
        else
        {
            Debug.LogWarning("PlayerManager: DeathScreenManager not set. Respawning immediately.");
            RespawnPlayer();
        }
    }
    
    public void RespawnPlayer()
    {
        if (player == null || fpsController == null || respawnPoint == null)
        {
            Debug.LogWarning("PlayerManager: Cannot respawn - missing references.");
            return;
        }
        
        // Get respawn position from anchor
        Vector3 targetPos = respawnPoint.position;
        Quaternion targetRot = respawnPoint.rotation;
        
        // Reset player health before respawn
        fpsController.ResetHealth();
        
        // Teleport player - that's it, nothing else
        fpsController.TeleportToPosition(targetPos, targetRot);
        
        // Re-enable control after respawn
        fpsController.SetDisabled(false);

        // Ensure cursor is locked/hidden for gameplay after respawn
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        isDead = false;
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
