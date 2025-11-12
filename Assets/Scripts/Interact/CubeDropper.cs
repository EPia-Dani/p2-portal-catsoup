using UnityEngine;
using UnityEngine.Events;

namespace Interact
{
    /// <summary>
    /// A platform/cube that drops down and returns up smoothly using lerp.
    /// Can be triggered manually or set to auto-cycle.
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
        
        [Tooltip("If true, automatically cycles between up and down positions")]
        public bool autoCycle = false;
        
        [Tooltip("Delay in seconds before returning up (only used if autoCycle is true)")]
        public float returnDelay = 1f;
        
        [Header("Animation Curve")]
        [Tooltip("Curve that controls the drop/return animation. X axis (0-1) represents progress, Y axis (0-1) represents the interpolation value.")]
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("Physics")]
        [Tooltip("If true, applies velocity to rigidbody instead of directly moving transform")]
        public bool useRigidbody = false;
        
        [Tooltip("If useRigidbody is true, this multiplier affects the velocity applied")]
        public float velocityMultiplier = 1f;
        
        [Header("Events")]
        [Tooltip("Invoked when cube reaches the bottom position")]
        public UnityEvent onReachedBottom;
        
        [Tooltip("Invoked when cube reaches the top position")]
        public UnityEvent onReachedTop;
        
        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        public bool debugLog = false;
        
        private Transform _targetTransform;
        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private float _currentProgress = 0f;
        private bool _isDropping = false;
        private bool _isMoving = false;
        private Rigidbody _rigidbody;
        private float _returnTimer = 0f;
        
        private void Start()
        {
            // Use assigned target or default to this GameObject
            _targetTransform = targetObject != null ? targetObject : transform;
            
            // Store starting position
            _startPosition = _targetTransform.localPosition;
            _endPosition = _startPosition + Vector3.down * dropDistance;
            
            // Get rigidbody from target object
            _rigidbody = _targetTransform.GetComponent<Rigidbody>();
            
            if (useRigidbody && _rigidbody == null)
            {
                Debug.LogWarning($"[CubeDropper] {gameObject.name}: useRigidbody is true but no Rigidbody component found on target!");
            }
        }
        
        private void Update()
        {
            if (useRigidbody && _rigidbody != null)
            {
                UpdateRigidbodyMovement();
            }
            else
            {
                UpdateTransformMovement();
            }
            
            // Handle auto-cycle return delay
            if (autoCycle && _isDropping && _currentProgress >= 1f)
            {
                _returnTimer += Time.deltaTime;
                if (_returnTimer >= returnDelay)
                {
                    ReturnUp();
                }
            }
        }
        
        /// <summary>
        /// Updates movement using transform (direct position changes)
        /// </summary>
        private void UpdateTransformMovement()
        {
            if (!_isMoving) return;
            
            float targetProgress = _isDropping ? 1f : 0f;
            float progressDelta = moveSpeed * Time.deltaTime / dropDistance;
            
            // Update progress towards target
            if (_currentProgress < targetProgress)
            {
                _currentProgress = Mathf.Min(_currentProgress + progressDelta, targetProgress);
            }
            else if (_currentProgress > targetProgress)
            {
                _currentProgress = Mathf.Max(_currentProgress - progressDelta, targetProgress);
            }
            
            // Check if we've reached the target
            if (Mathf.Approximately(_currentProgress, targetProgress))
            {
                _isMoving = false;
                
                if (_isDropping)
                {
                    onReachedBottom?.Invoke();
                    if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Reached bottom");
                    
                    if (!autoCycle)
                    {
                        // Stop at bottom if not auto-cycling
                        return;
                    }
                }
                else
                {
                    onReachedTop?.Invoke();
                    if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Reached top");
                    
                    if (autoCycle)
                    {
                        // Auto-cycle: drop again after a delay
                        _returnTimer = 0f;
                        Invoke(nameof(Drop), returnDelay);
                    }
                }
            }
            
            // Evaluate curve and lerp position
            float curveValue = movementCurve.Evaluate(_currentProgress);
            _targetTransform.localPosition = Vector3.Lerp(_startPosition, _endPosition, curveValue);
        }
        
        /// <summary>
        /// Updates movement using rigidbody (applies velocity)
        /// </summary>
        private void UpdateRigidbodyMovement()
        {
            if (!_isMoving) return;
            
            Vector3 currentPos = _targetTransform.localPosition;
            Vector3 targetPos = _isDropping ? _endPosition : _startPosition;
            Vector3 direction = (targetPos - currentPos).normalized;
            
            float distanceToTarget = Vector3.Distance(currentPos, targetPos);
            
            // Check if we've reached the target
            if (distanceToTarget < 0.01f)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _isMoving = false;
                
                // Update progress for event triggers
                _currentProgress = _isDropping ? 1f : 0f;
                
                if (_isDropping)
                {
                    onReachedBottom?.Invoke();
                    if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Reached bottom");
                    
                    if (!autoCycle)
                    {
                        return;
                    }
                }
                else
                {
                    onReachedTop?.Invoke();
                    if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Reached top");
                    
                    if (autoCycle)
                    {
                        _returnTimer = 0f;
                        Invoke(nameof(Drop), returnDelay);
                    }
                }
            }
            else
            {
                // Apply velocity towards target
                float speed = moveSpeed * velocityMultiplier;
                _rigidbody.linearVelocity = direction * speed;
                
                // Update progress for curve evaluation (optional, for visual feedback)
                float totalDistance = dropDistance;
                _currentProgress = _isDropping ? 
                    1f - (distanceToTarget / totalDistance) : 
                    distanceToTarget / totalDistance;
            }
        }
        
        /// <summary>
        /// Drops the cube down
        /// </summary>
        public void Drop()
        {
            if (_isMoving && _isDropping)
            {
                return; // Already dropping
            }
            
            _isDropping = true;
            _isMoving = true;
            _returnTimer = 0f;
            
            if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Dropping");
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
            _returnTimer = 0f;
            
            if (debugLog) Debug.Log($"[CubeDropper] {gameObject.name}: Returning up");
        }
        
        /// <summary>
        /// Toggles between drop and return
        /// </summary>
        public void Toggle()
        {
            if (_isDropping)
            {
                ReturnUp();
            }
            else
            {
                Drop();
            }
        }
        
        /// <summary>
        /// Resets to starting position immediately
        /// </summary>
        public void ResetPosition()
        {
            _isMoving = false;
            _isDropping = false;
            _currentProgress = 0f;
            _returnTimer = 0f;
            
            if (useRigidbody && _rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
            }
            
            _targetTransform.localPosition = _startPosition;
        }
        
        private void OnValidate()
        {
            if (dropDistance < 0f)
            {
                dropDistance = 0f;
            }
            if (moveSpeed < 0f)
            {
                moveSpeed = 0f;
            }
            if (returnDelay < 0f)
            {
                returnDelay = 0f;
            }
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Draws gizmos in the editor to visualize drop distance
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Transform gizmoTransform = targetObject != null ? targetObject : transform;
            Vector3 startPos = Application.isPlaying ? _startPosition : gizmoTransform.localPosition;
            Vector3 endPos = startPos + Vector3.down * dropDistance;
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startPos, 0.2f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endPos, 0.2f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPos, endPos);
        }
        #endif
    }
}

