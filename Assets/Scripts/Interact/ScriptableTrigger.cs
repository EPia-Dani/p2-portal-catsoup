using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Interact
{
    /// <summary>
    /// A simple trigger component that fires UnityEvents when objects enter/exit.
    /// Attach to a GameObject with a Collider set as Trigger.
    /// In the inspector, drag objects and select methods from the dropdown.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ScriptableTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [Tooltip("Which tags should trigger this? Leave empty to trigger on any object.")]
        public string[] triggerTags = new string[] { "Interactable", "Player" };
        
        [Tooltip("If true, only triggers when objects with specific tags enter. If false, triggers on any object.")]
        public bool useTagFilter = true;
        
        [Tooltip("If true, ignores objects that are currently being held by the player")]
        public bool ignoreHeldObjects = true;
        
        [Tooltip("If true, automatically loads the next scene when trigger is activated")]
        public bool loadNextScene = false;
        
        [Tooltip("Delay in seconds before loading the next scene (only used if loadNextScene is enabled)")]
        [SerializeField] float sceneLoadDelay = 0f;
        
        [Header("On Trigger Enter")]
        [Tooltip("Events to invoke when an object enters the trigger")]
        public UnityEvent onEnter;
        
        [Header("On Trigger Exit")]
        [Tooltip("Events to invoke when an object exits the trigger")]
        public UnityEvent onExit;
        
        private Collider _collider;
        
        private void Start()
        {
            _collider = GetComponent<Collider>();
            if (_collider == null)
            {
                Debug.LogError($"[ScriptableTrigger] {gameObject.name}: No Collider component found!");
                enabled = false;
                return;
            }
            
            if (!_collider.isTrigger)
            {
                _collider.isTrigger = true;
            }
        }
        
        private bool ShouldTrigger(Collider other)
        {
            if (other == null) return false;
            
            if (ignoreHeldObjects && PlayerPickup.IsObjectHeld(other.gameObject))
            {
                return false;
            }
            
            if (!useTagFilter) return true;
            
            if (triggerTags != null && triggerTags.Length > 0)
            {
                foreach (string tag in triggerTags)
                {
                    if (other.CompareTag(tag))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (ShouldTrigger(other))
            {
                onEnter?.Invoke();
                
                if (loadNextScene)
                {
                    if (sceneLoadDelay > 0f)
                    {
                        StartCoroutine(LoadNextSceneDelayed());
                    }
                    else
                    {
                        LoadNextScene();
                    }
                }
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (ShouldTrigger(other))
            {
                onExit?.Invoke();
            }
        }
        
        /// <summary>
        /// Coroutine that waits for the delay before loading the next scene
        /// </summary>
        private IEnumerator LoadNextSceneDelayed()
        {
            yield return new WaitForSeconds(sceneLoadDelay);
            LoadNextScene();
        }
        
        /// <summary>
        /// Loads the next scene in the build index.
        /// Can be called from UnityEvents in the inspector.
        /// </summary>
        public void LoadNextScene()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            int nextSceneIndex = currentSceneIndex + 1;
            
            if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning($"[ScriptableTrigger] {gameObject.name}: No next scene available. Current scene is the last one in build settings.");
                return;
            }
            
            Debug.Log($"[ScriptableTrigger] {gameObject.name}: Loading next scene (index {nextSceneIndex})");
            
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
        
        private void OnValidate()
        {
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
            
            if (sceneLoadDelay < 0f)
            {
                sceneLoadDelay = 0f;
            }
        }
    }
}
