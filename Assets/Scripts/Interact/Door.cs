using UnityEngine;

namespace Interact {
	/// <summary>
	/// Simple door that opens by moving left/right parts outward.
	/// </summary>
	public class Door : MonoBehaviour {
		[Header("Door Parts")]
		[Tooltip("Left part of the door (moves left when opening)")]
		[SerializeField] Transform doorLeft;
		
		[Tooltip("Right part of the door (moves right when opening)")]
		[SerializeField] Transform doorRight;

        [Header("Audio")]
        [Tooltip("Sound played when the door opens.")]
        [SerializeField] AudioClip openClip;
        [Tooltip("Sound played when the door closes.")]
        [SerializeField] AudioClip closeClip;

		[Header("Door Settings")]
		[Tooltip("Distance each door part moves when opening (in local space)")]
		[SerializeField] float openDistance = 1f;
		
		[Tooltip("Speed at which doors open/close (units per second)")]
		[SerializeField] float moveSpeed = 2f;
		
		[Header("Animation Curve")]
		[Tooltip("Curve that controls the door opening/closing animation. X axis (0-1) represents progress, Y axis (0-1) represents the interpolation value.")]
		[SerializeField] AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		private Vector3 _leftOriginalPosition;
		private Vector3 _rightOriginalPosition;
		private Vector3 _leftOpenPosition;
		private Vector3 _rightOpenPosition;
		private bool _isOpen = false;
		private float _currentProgress = 0f;

		void Start() {
			if (doorLeft == null || doorRight == null) {
				Debug.LogError($"[Door] {gameObject.name}: Door left or right transform not assigned!");
				enabled = false;
				return;
			}

			// Store original positions
			_leftOriginalPosition = doorLeft.localPosition;
			_rightOriginalPosition = doorRight.localPosition;

			// Calculate open positions (Z axis: left = positive Z, right = negative Z)
			_leftOpenPosition = _leftOriginalPosition + Vector3.forward * openDistance;
			_rightOpenPosition = _rightOriginalPosition + Vector3.back * openDistance;
		}

		/// <summary>
		/// Opens the door by moving left part left and right part right
		/// </summary>
		public void Open() {
			_isOpen = true;
			if (openClip != null) AudioSource.PlayClipAtPoint(openClip, transform.position);
		}

		/// <summary>
		/// Closes the door by moving both parts back to original positions
		/// </summary>
		public void Close() {
			_isOpen = false;
			if (closeClip != null) AudioSource.PlayClipAtPoint(closeClip, transform.position);
		}

		void Update() {
			if (doorLeft == null || doorRight == null) return;

			// Update progress towards target (0 = closed, 1 = open)
			float targetProgress = _isOpen ? 1f : 0f;
			float progressDelta = moveSpeed * Time.deltaTime;
			
			if (_currentProgress < targetProgress) {
				_currentProgress = Mathf.Min(_currentProgress + progressDelta, targetProgress);
			} else if (_currentProgress > targetProgress) {
				_currentProgress = Mathf.Max(_currentProgress - progressDelta, targetProgress);
			}

			// Evaluate the curve at current progress
			float curveValue = openCurve.Evaluate(_currentProgress);
			
			// Lerp between original and open positions using the curve value
			doorLeft.localPosition = Vector3.Lerp(_leftOriginalPosition, _leftOpenPosition, curveValue);
			doorRight.localPosition = Vector3.Lerp(_rightOriginalPosition, _rightOpenPosition, curveValue);
		}

		void OnValidate() {
			if (openDistance < 0f) {
				openDistance = 0f;
			}
			if (moveSpeed < 0f) {
				moveSpeed = 0f;
			}
		}
	}
}
