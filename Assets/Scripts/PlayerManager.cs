using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
        
        // Subscribe to scene loaded events
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from scene loaded events
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset player reference when loading a new scene (new player instance will be created)
        player = null;
        fpsController = null;
        
        // Handle FinalLevel scene setup
        if (scene.name == "FinalLevel")
        {
            // Use coroutine to ensure scene is fully initialized
            StartCoroutine(SetupFinalLevelDelayed());
        }
    }
    
    private IEnumerator SetupFinalLevelDelayed()
    {
        // Wait one frame to ensure all GameObjects are initialized
        yield return null;
        
        SetupFinalLevel();
    }
    
    private void SetupFinalLevel()
    {
        Debug.Log("PlayerManager: Setting up FinalLevel scene - linking checkpoint anchor");
        
        // Find and link checkpoint anchor
        GameObject checkpointAnchor = GameObject.Find("CheckpointAnchor");
        if (checkpointAnchor == null)
        {
            // Try alternative names
            checkpointAnchor = GameObject.Find("Checkpoint");
            if (checkpointAnchor == null)
            {
                checkpointAnchor = GameObject.Find("RespawnAnchor");
            }
        }
        
        if (checkpointAnchor != null)
        {
            SetRespawnPoint(checkpointAnchor.transform);
            Debug.Log($"PlayerManager: Checkpoint anchor '{checkpointAnchor.name}' linked to respawn point");
        }
        else
        {
            Debug.LogWarning("PlayerManager: No checkpoint anchor found in FinalLevel scene! Expected GameObject named 'CheckpointAnchor', 'Checkpoint', or 'RespawnAnchor'");
        }
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
    /// Called when the player dies. Triggers respawn with black fade transition.
    /// </summary>
    public void OnPlayerDeath()
    {
        if (isDead)
        {
            return; // Already dead, prevent multiple death triggers
        }
        
        isDead = true;
        
        // Use black fade system to respawn directly
        if (ScreenFadeManager.Instance != null)
        {
            ScreenFadeManager.Instance.FadeOutAndRespawn(() =>
            {
                // Respawn player during black screen
                RespawnPlayer();
            });
        }
        else
        {
            // Fallback: respawn immediately if fade manager doesn't exist
            RespawnPlayer();
        }
    }
    
    /// <summary>
    /// Respawns the player at the respawn anchor.
    /// </summary>
    public void RespawnPlayer()
    {
        if (player == null || fpsController == null || respawnPoint == null)
        {
            return;
        }
        
        // Get respawn position from anchor
        Vector3 targetPos = respawnPoint.position;
        Quaternion targetRot = respawnPoint.rotation;
        
        // Teleport player - that's it, nothing else
        fpsController.TeleportToPosition(targetPos, targetRot);
        
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

