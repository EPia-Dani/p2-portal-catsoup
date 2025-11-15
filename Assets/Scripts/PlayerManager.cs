using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerManager : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("Reference to the player GameObject. If null, will search for FPSController.")]
    public GameObject player;
    
    [Header("Respawn Settings")]
    [Tooltip("Starting position for respawn. If null, uses player's initial position.")]
    public Transform respawnPoint;
    
    [Tooltip("If true, the player will respawn at the assigned respawnPoint. If false, the scene will restart on death.")]
    public bool respawnAtPoint;
    
    private FPSController fpsController;
    
    private bool isDead;
    // Track times so we only allow checkpoint respawn if the checkpoint was activated after the player's last spawn
    private float _lastSpawnTime = float.NegativeInfinity;
    private float _checkpointActivatedTime = float.NegativeInfinity;
    
    // Singleton instance
    private static PlayerManager _instance;
    public static PlayerManager Instance => _instance;
    
    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Enforce default behavior: do NOT respawn at checkpoints unless the checkpoint has been activated by the player.
        // This overrides any accidental inspector-serialized value so dying before crossing a checkpoint restarts the scene.
        respawnAtPoint = false;
        _lastSpawnTime = float.NegativeInfinity;
        _checkpointActivatedTime = float.NegativeInfinity;
        
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

        // Ensure checkpoint respawn is disabled on scene load: only activating a checkpoint
        // during gameplay (crossing its trigger) should enable respawnAtPoint.
        respawnAtPoint = false;
        // Keep respawnPoint if SetupFinalLevel wants to link it; SetupFinalLevel will re-link when appropriate.
        isDead = false;
        
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
            // Link checkpoint anchor but DO NOT enable checkpoint respawn until player actually crosses it
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
            // Disable PlayerManager to avoid running without a valid FPSController
            enabled = false;
        }
        else
        {
            // Store initial rotation/position if needed later (not used by default behavior)
            // Record initial spawn time for this PlayerManager instance
            _lastSpawnTime = Time.time;
        }
    }
    
    public void OnPlayerDeath()
    {
        if (isDead)
        {
            return; 
        }
        
        isDead = true;
        // Only allow checkpoint respawn if the checkpoint was activated after the player's last spawn
        // If a checkpoint has been activated and respawnAtPoint is true, use it. The prior time-gated
        // check could prevent valid activations in some timing scenarios (player stepping on a checkpoint
        // didn't reliably set the activation time after _lastSpawnTime). Rely on respawnAtPoint and a
        // non-null respawnPoint instead.
        if (respawnAtPoint && respawnPoint != null && fpsController != null)
         {
             if (ScreenFadeManager.Instance != null)
             {
                 ScreenFadeManager.Instance.FadeOutAndRespawn(() => { RespawnPlayer(); });
             }
             else
             {
                 RespawnPlayer();
             }
         }
         else
         {
            // No checkpoint set - restart the scene so everything resets to the level start
            if (ScreenFadeManager.Instance != null)
            {
                int currentIndex = SceneManager.GetActiveScene().buildIndex;
                ScreenFadeManager.Instance.FadeOutAndLoadScene(currentIndex);
            }
            else
            {
                // Fallback: immediate scene reload
                RestartScene();
            }
         }
    }
    public void RespawnPlayer()
    {
        if (player == null || fpsController == null || respawnPoint == null)
        {
            return;
        }
        
        // Get respawn position from anchor
        Vector3 targetPos = respawnPoint.position;
        Quaternion targetRot = respawnPoint.rotation;
        
        // Reset all scene objects EXCEPT audio systems (FMOD, AudioListeners, AudioSources marked DontDestroyOnLoad)
        ResetSceneState();
        
        // Teleport player to checkpoint
        fpsController.TeleportToPosition(targetPos, targetRot);
        
        // Reset player health
        if (fpsController != null)
        {
            // Use reflection to reset health without making fields public
            var healthField = typeof(FPSController).GetField("_currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxHealthField = typeof(FPSController).GetField("maxHealth", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (healthField != null && maxHealthField != null)
            {
                int maxHP = (int)maxHealthField.GetValue(fpsController);
                healthField.SetValue(fpsController, maxHP);
            }
            
            // Re-enable player control
            fpsController.SetDisabled(false);
        }
        
        // Update last spawn time so subsequent deaths only use checkpoints activated after this moment
        _lastSpawnTime = Time.time;
        isDead = false;
    }
    
    /// <summary>
    /// Resets all scene objects (turrets, radios, cubes, etc.) to their initial state.
    /// Preserves audio systems (FMOD, AudioListener, persistent AudioSources).
    /// </summary>
    private void ResetSceneState()
    {
        // Reset all turrets to idle state
        var turrets = FindObjectsByType<Enemy.Turret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var turret in turrets)
        {
            if (turret == null) continue;
            // Re-enable and reset turrets
            turret.gameObject.SetActive(true);
            turret.enabled = true;
            turret.ResetToIdle();
        }
        
        // Reset all interactable objects (radios, cubes, etc.)
        var interactables = FindObjectsByType<InteractableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in interactables)
        {
            if (obj == null) continue;
            // Reset physics - stop all movement
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        // Destroy all active projectiles
        var projectiles = FindObjectsByType<Enemy.Projectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var proj in projectiles)
        {
            if (proj != null)
            {
                Destroy(proj.gameObject);
            }
        }
        
        // Reset radio counter if present (but keep collected radios count - only reset if you want full reset)
        var radioCounter = FindFirstObjectByType<Interact.RadioCounter>();
        if (radioCounter != null)
        {
            // Don't reset radio count - player keeps their progress
            // If you want to reset radios too, add that logic here
        }
        
        // Reset buttons to unpressed state
        var buttons = FindObjectsByType<Interact.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null) continue;
            // Buttons will reset themselves on scene reload, no action needed here for checkpoint respawn
        }
        
        Debug.Log("PlayerManager: Scene state reset (turrets, projectiles, physics) while preserving audio systems.");
    }
    public void RestartScene()
    {
        Debug.Log("PlayerManager: Restarting scene...");
        GameSceneManager.ReloadCurrentScene();
    }
    public void SetRespawnPoint(Transform newRespawnPoint, bool enable = false)
    {
        respawnPoint = newRespawnPoint;
        // Only enable checkpoint respawn if explicitly requested
        if (enable)
        {
            respawnAtPoint = (respawnPoint != null);
            if (respawnAtPoint)
            {
                _checkpointActivatedTime = Time.time;
            }
         }

         Debug.Log($"PlayerManager: Respawn point set to '{respawnPoint?.name}'. Checkpoint respawn enabled: {respawnAtPoint}");
    }
    public bool IsDead => isDead;
}
