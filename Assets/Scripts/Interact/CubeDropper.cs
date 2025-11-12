using UnityEngine;

namespace Interact
{
    /// <summary>
    /// A platform/cube that drops down when Drop() is called.
    /// </summary>
    public class CubeDropper : MonoBehaviour
    {
        [Header("Target Object")]
        [Tooltip("The GameObject that will move (e.g., a child cube). If not assigned, this GameObject will move.")]
        public Transform targetObject;
        
        [Header("Movement Settings")]
        [Tooltip("How far down the cube drops (in local Y units)")]
        public float dropDistance = 5f;
        
        [Tooltip("Speed of movement (units per second)")]
        public float moveSpeed = 2f;
        
        private Transform _targetTransform;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private float _currentProgress = 0f;
        private bool _isDropping = false;
        private bool _isMoving = false;
        
        private void Start()
        {
            // Use assigned target or default to this GameObject
            _targetTransform = targetObject != null ? targetObject : transform;
            
            // Store starting position
            _startPosition = _targetTransform.localPosition;
            // Convert world down to local space to respect rotation
            Vector3 localDown = _targetTransform.InverseTransformDirection(Vector3.down);
            _endPosition = _startPosition + localDown * dropDistance;
        }
        
        private void Update()
        {
            if (!_isMoving) return;
            
            if (_targetTransform == null)
            {
                Debug.LogError($"[CubeDropper] {gameObject.name}: _targetTransform is null!");
                _isMoving = false;
                return;
            }
            
            float targetProgress = _isDropping ? 1f : 0f;
            
            // Calculate movement direction
            float direction = targetProgress > _currentProgress ? 1f : -1f;
            
            if (dropDistance <= 0f)
            {
                Debug.LogError($"[CubeDropper] {gameObject.name}: dropDistance is {dropDistance}! Cannot move.");
                _isMoving = false;
                return;
            }
            
            float progressDelta = moveSpeed * Time.deltaTime / dropDistance;
            
            // Update progress towards target
            _currentProgress += progressDelta * direction;
            
            // Clamp to target
            if (direction > 0f)
            {
                _currentProgress = Mathf.Min(_currentProgress, targetProgress);
            }
            else
            {
                _currentProgress = Mathf.Max(_currentProgress, targetProgress);
            }
            
            // Update position
            Vector3 newPosition = Vector3.Lerp(_startPosition, _endPosition, _currentProgress);
            _targetTransform.localPosition = newPosition;
            
            // Debug every few frames
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"[CubeDropper] Moving: progress={_currentProgress:F3}, pos={newPosition}, start={_startPosition}, end={_endPosition}");
            }
            
            // Check if we've reached the target
            if (Mathf.Approximately(_currentProgress, targetProgress))
            {
                _isMoving = false;
                _currentProgress = targetProgress; // Ensure exact value
                Debug.Log($"[CubeDropper] {gameObject.name}: Reached target position");
            }
        }
        
        /// <summary>
        /// Drops the cube down
        /// </summary>
        public void Drop()
        {
            // Early return check at the very beginning to prevent duplicate calls
            if (_isMoving && _isDropping)
            {
                Debug.Log($"[CubeDropper] Already dropping, returning early");
                return; // Already dropping
            }
            
            Debug.Log($"[CubeDropper] Drop() METHOD CALLED on {gameObject.name}!");
            _isDropping = true;
            _isMoving = true;
        }
        
        /// <summary>
        /// Returns the cube back up
        /// </summary>
        public void ReturnUp()
        {
            if (_isMoving && !_isDropping)
            {
                return; // Already returning
            }
            
            _isDropping = false;
            _isMoving = true;
        }
        
        /// <summary>
        /// Resets to starting position immediately
        /// </summary>
        public void ResetPosition()
        {
            _isMoving = false;
            _isDropping = false;
            _currentProgress = 0f;
            _targetTransform.localPosition = _startPosition;
        }
       
        
       
    }
}

