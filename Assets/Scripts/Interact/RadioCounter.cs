using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;


public class RadioCounter : MonoBehaviour
{
    [Header("Counter Settings")]
    [Tooltip("Number of radios that need to be destroyed to trigger scene load")]
    public int targetCount = 10;
    
    [Tooltip("Delay before loading next scene after target count is reached (seconds)")]
    public float sceneLoadDelay = 1f;
    
    [Header("Audio")]
    [Tooltip("FMOD sound events for countdown (array of 10 sounds). Index 0 = '9 to go', Index 1 = '8 to go', ..., Index 8 = '1 to go'. Index 9 is unused (use 'All Radios Captured Sound' instead).")]
    public EventReference[] radiosRemainingSounds = new EventReference[10];
    
    [Tooltip("FMOD sound event to play when all radios have been captured (e.g., 'All radios have been captured, please go to the elevator')")]
    public EventReference allRadiosCapturedSound;
    
    [Header("Debug")]
    [Tooltip("Show debug messages when radios are destroyed")]
    public bool showDebugMessages = true;
    
    private int _destroyedCount = 0;
    private bool _hasTriggeredSceneLoad = false;
    
    // Singleton instance
    private static RadioCounter instance;
    public static RadioCounter Instance => instance;
    
    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        
        // Reset count when scene loads
        _destroyedCount = 0;
        _hasTriggeredSceneLoad = false;
    }
    
    /// <summary>
    /// Called when a radio is destroyed. Increments counter and loads scene if target reached.
    /// </summary>
    public void OnRadioDestroyed(Radio radio)
    {
        if (_hasTriggeredSceneLoad)
        {
            return; // Already triggered scene load, ignore additional destructions
        }
        
        _destroyedCount++;
        
        // Calculate remaining radios
        int remainingCount = targetCount - _destroyedCount;
        
        // Play countdown sound based on remaining radios
        // When 1 radio destroyed (9 remain): play "9 to go" at index 0
        // When 2 radios destroyed (8 remain): play "8 to go" at index 1
        // etc.
        if (radiosRemainingSounds != null && radiosRemainingSounds.Length > 0 && remainingCount > 0 && remainingCount < targetCount)
        {
            // Index calculation: destroyedCount - 1 gives us the right index
            // destroyedCount=1 (9 remain) -> index 0 = "9 to go"
            // destroyedCount=2 (8 remain) -> index 1 = "8 to go"
            int soundIndex = _destroyedCount - 1;
            
            if (soundIndex >= 0 && soundIndex < radiosRemainingSounds.Length)
            {
                if (!radiosRemainingSounds[soundIndex].IsNull && radio != null)
                {
                    Vector3 radioPosition = radio.transform.position;
                    RuntimeManager.PlayOneShot(radiosRemainingSounds[soundIndex], radioPosition);
                }
            }
        }
        
        if (showDebugMessages)
        {
            Debug.Log($"[RadioCounter] Radio destroyed! Count: {_destroyedCount}/{targetCount} ({remainingCount} remaining)");
        }
        
        // Check if we've reached the target count
        if (_destroyedCount >= targetCount)
        {
            _hasTriggeredSceneLoad = true;
            
            // Play final "all radios captured" sound
            if (!allRadiosCapturedSound.IsNull && radio != null)
            {
                Vector3 radioPosition = radio.transform.position;
                RuntimeManager.PlayOneShot(allRadiosCapturedSound, radioPosition);
            }
            
            if (showDebugMessages)
            {
                Debug.Log($"[RadioCounter] Target count reached ({_destroyedCount}/{targetCount})! Loading next scene in {sceneLoadDelay} seconds...");
            }
            
            // Load next scene after delay
            Invoke(nameof(LoadNextScene), sceneLoadDelay);
        }
    }
    
    /// <summary>
    /// Loads the next scene in the build index with fade transition.
    /// </summary>
    private void LoadNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;
        
        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"[RadioCounter] No next scene available. Current scene is the last one in build settings.");
            return;
        }
        
        Debug.Log($"[RadioCounter] Loading next scene (index {nextSceneIndex})");
        
        // Use fade system if available
        var fadeManager = ScreenFadeManager.Instance;
        if (fadeManager != null)
        {
            fadeManager.FadeOutAndLoadScene(nextSceneIndex);
        }
        else
        {
            // Fallback: direct load if fade manager doesn't exist
            SceneManager.LoadScene(nextSceneIndex);
        }
    }
    
    /// <summary>
    /// Gets the current count of destroyed radios.
    /// </summary>
    public int GetDestroyedCount()
    {
        return _destroyedCount;
    }
    
    /// <summary>
    /// Gets the target count needed to trigger scene load.
    /// </summary>
    public int GetTargetCount()
    {
        return targetCount;
    }
    
    /// <summary>
    /// Resets the counter (useful for testing or restarting).
    /// </summary>
    public void ResetCounter()
    {
        _destroyedCount = 0;
        _hasTriggeredSceneLoad = false;
        
        if (showDebugMessages)
        {
            Debug.Log("[RadioCounter] Counter reset");
        }
    }
}

