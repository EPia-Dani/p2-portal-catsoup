using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;

namespace Interact
{
    /// <summary>
    /// Singleton that tracks destroyed radios and loads the next scene when 10 radios are destroyed.
    /// Should be placed in the scene as a singleton.
    /// </summary>
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
        
        private int _destroyedCount;
        private bool _hasTriggeredSceneLoad;
        
        // Singleton instance
        private static RadioCounter _instance;
        public static RadioCounter Instance => _instance;
        
        private void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            // Reset count when scene loads
            _destroyedCount = 0;
            _hasTriggeredSceneLoad = false;
        }
        
        public void OnRadioDestroyed(Interact.Radio radio)
        {
            if (_hasTriggeredSceneLoad)
            {
                return; // Already triggered scene load, ignore additional destructions
            }
            
            _destroyedCount++;
            
            // Calculate remaining radios
            int remainingCount = targetCount - _destroyedCount;
            
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
        
        public int GetDestroyedCount()
        {
            return _destroyedCount;
        }
        public int GetTargetCount()
        {
            return targetCount;
        }
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
}
