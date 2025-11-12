using UnityEngine;

namespace Interact {
	/// <summary>
	/// Simple elevator that moves up and down by translating a single door/platform vertically.
	/// </summary>
	public class Elevator : MonoBehaviour {
		[Header("Elevator Parts")]
		[Tooltip("The door/platform that moves up and down")]
		[SerializeField] Transform elevatorDoor;

	[Header("Elevator Settings")]
	[Tooltip("Distance the elevator moves down when opening (in local space, negative Y direction)")]
	[SerializeField] float moveDistance = 1f;
	
	[Tooltip("Speed at which elevator moves up/down (units per second)")]
	[SerializeField] float moveSpeed = 2f;
	
	[Header("Animation Curve")]
	[Tooltip("Curve that controls the elevator movement animation. X axis (0-1) represents progress, Y axis (0-1) represents the interpolation value.")]
	[SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	private Vector3 _originalPosition;
	private Vector3 _downPosition;
	private bool _isOpen = false;
	private float _currentProgress = 0f;

	void Start() {
		if (elevatorDoor == null) {
			Debug.LogError($"[Elevator] {gameObject.name}: Elevator door transform not assigned!");
			enabled = false;
			return;
		}

		// Store original position
		_originalPosition = elevatorDoor.localPosition;

		// Calculate down position (Y axis: down = negative Y)
		_downPosition = _originalPosition + Vector3.down * moveDistance;
	}

	/// <summary>
	/// Moves the elevator down (opens)
	/// </summary>
	public void Open() {
		_isOpen = true;
	}

	/// <summary>
	/// Moves the elevator up to original position (closes)
	/// </summary>
	public void Close() {
		_isOpen = false;
	}

	void Update() {
		if (elevatorDoor == null) return;

		// Update progress towards target (0 = up/closed, 1 = down/open)
		float targetProgress = _isOpen ? 1f : 0f;
		float progressDelta = moveSpeed * Time.deltaTime;
		
		if (_currentProgress < targetProgress) {
			_currentProgress = Mathf.Min(_currentProgress + progressDelta, targetProgress);
		} else if (_currentProgress > targetProgress) {
			_currentProgress = Mathf.Max(_currentProgress - progressDelta, targetProgress);
		}

		// Evaluate the curve at current progress
		float curveValue = moveCurve.Evaluate(_currentProgress);
		
		// Lerp between original and down positions using the curve value
		elevatorDoor.localPosition = Vector3.Lerp(_originalPosition, _downPosition, curveValue);
	}

		void OnValidate() {
			if (moveDistance < 0f) {
				moveDistance = 0f;
			}
			if (moveSpeed < 0f) {
				moveSpeed = 0f;
			}
		}
	}
}

