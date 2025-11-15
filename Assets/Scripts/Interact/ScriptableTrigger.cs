using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Interact
{
    [RequireComponent(typeof(Collider))]
    public class ScriptableTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [Tooltip("Which tags should trigger this? Leave empty to trigger on any object.")]
        public string[] triggerTags;
        
        [Tooltip("If true, only triggers when objects with specific tags enter. If false, triggers on any object.")]
        public bool useTagFilter = true;
        
        [Tooltip("If true, ignores objects that are currently being held by the player")]
        public bool ignoreHeldObjects = true;
        
        [Tooltip("If true, automatically loads the next scene when trigger is activated")]
        public bool loadNextScene = false;
        
        [Tooltip("Delay in seconds before loading the next scene (only used if loadNextScene is enabled)")]
        [SerializeField] float sceneLoadDelay;
        
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
                foreach (string candidateTag in triggerTags)
                {
                    if (other.CompareTag(candidateTag))
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
        private IEnumerator LoadNextSceneDelayed()
        {
            yield return new WaitForSeconds(sceneLoadDelay);
            LoadNextScene();
        }
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
            
            // Ensure sensible defaults for serialized fields
            if (triggerTags == null || triggerTags.Length == 0)
            {
                triggerTags = new[] { "Interactable", "Player" };
            }
            
            if (sceneLoadDelay < 0f)
            {
                sceneLoadDelay = 0f;
            }

            // Ensure UnityEvent fields are non-null so invoking them is safe in editor/runtime
            if (onEnter == null)
                onEnter = new UnityEvent();
            if (onExit == null)
                onExit = new UnityEvent();
        }
    }
}
