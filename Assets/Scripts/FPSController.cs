using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform yawTransform;
    [SerializeField] private Transform pitchTransform;

    [SerializeField] private float maxSpeedOnGround = 4f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float timeToApex = 0.45f;
    [SerializeField] private float terminalVelocity = -53f;

	[SerializeField] [Range(0f, 1f)] private float lookSensitivity = 1f;
	[SerializeField] private float groundAcceleration = 40f;
	[SerializeField] private float airAcceleration = 10f;
	[SerializeField] private float groundFriction = 8f;


    
    private Input.PlayerInput _controls;

    // movement state
    private float _gravity;
    private float _jumpSpeed;
    private float _verticalSpeed;
    private bool _isGrounded;
	private Vector2 _moveInput;
	private Vector3 _horizontalVelocity;

    // look state
    private float _currentYaw;
    private float _currentPitch;
    

    private void Awake() {
        if (!characterController) characterController = GetComponent<CharacterController>();

        // gravity/jump precompute
        _gravity = -2f * jumpHeight / (timeToApex * timeToApex);
        _jumpSpeed = 2f * jumpHeight / timeToApex;

        // Get the shared input instance from InputManager
        _controls = InputManager.PlayerInput;

        // sync initial yaw/pitch from current transforms
        if (yawTransform != null) {
            _currentYaw = yawTransform.eulerAngles.y;
        }
        if (pitchTransform != null) {
            float rawX = pitchTransform.localEulerAngles.x;
            _currentPitch = Mathf.DeltaAngle(0f, rawX); // convert to [-180, 180]
        }
    }

 

    private void Update() {
		float dt = Time.deltaTime;

		// input
		ReadInput(out Vector2 moveInput, out Vector2 lookInput, out bool jumpPressed);

		// look
		UpdateLook(lookInput, dt);

		// vertical motion (returns dy to apply this frame)
		float dy = UpdateVerticalMotion(jumpPressed, dt);

		// horizontal motion (updates _horizontalVelocity)
		UpdateHorizontalMotion(moveInput, dt);

		// move and resolve collisions
		MoveAndCollide(dy, dt);
    }

	private void ReadInput(out Vector2 moveInput, out Vector2 lookInput, out bool jumpPressed) {
		moveInput = _controls.Player.Move.ReadValue<Vector2>();
		lookInput = _controls.Player.Look.ReadValue<Vector2>();
		jumpPressed = _controls.Player.Jump.WasPerformedThisFrame();
		_moveInput = moveInput; // retain for debugging if needed
	}

	private void UpdateLook(Vector2 lookInput, float dt) {
		var lookScale = lookSensitivity * dt * 100f;
		_currentYaw += lookInput.x * lookScale;
		_currentPitch -= lookInput.y * lookScale;
		_currentPitch = Mathf.Clamp(_currentPitch, -89f, 89f);
		if (yawTransform != null) yawTransform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
		if (pitchTransform != null) pitchTransform.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
	}

	private float UpdateVerticalMotion(bool jumpPressed, float dt) {
		float acceleration = 0f;
		if (_isGrounded) {
			if (_verticalSpeed < 0f) _verticalSpeed = -2.0f;
		} else {
			acceleration = _gravity;
		}
		float dy = _verticalSpeed * dt + 0.5f * acceleration * dt * dt;
		_verticalSpeed += acceleration * dt;
		if (_verticalSpeed < terminalVelocity) _verticalSpeed = terminalVelocity;
		if (jumpPressed && _isGrounded) { _verticalSpeed = _jumpSpeed; _isGrounded = false; }
		return dy;
	}

	private void UpdateHorizontalMotion(Vector2 moveInput, float dt) {
		Vector3 wishDir = (transform.forward * moveInput.y + transform.right * moveInput.x);
		wishDir.y = 0f;
		if (wishDir.sqrMagnitude > 1e-4f) wishDir.Normalize();
		float wishSpeed = maxSpeedOnGround;
		float accel = _isGrounded ? groundAcceleration : airAcceleration;
		Vector3 targetVel = wishDir * wishSpeed;
		Vector3 horizontal = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z);
		Vector3 delta = targetVel - horizontal;
		float maxDelta = accel * dt;
		if (delta.sqrMagnitude > maxDelta * maxDelta) delta = delta.normalized * maxDelta;
		horizontal += delta;
		if (_isGrounded) {
			float mag = horizontal.magnitude;
			mag = Mathf.Max(0f, mag - groundFriction * dt);
			horizontal = (mag > 0f) ? horizontal.normalized * mag : Vector3.zero;
		}
		_horizontalVelocity = new Vector3(horizontal.x, 0f, horizontal.z);
	}

	private void MoveAndCollide(float dy, float dt) {
		Vector3 move = _horizontalVelocity * dt;
		move.y = dy;
		if (!characterController || !characterController.enabled) return;
		CollisionFlags flags = characterController.Move(move);
		_isGrounded = (flags & CollisionFlags.Below) != 0;
		if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;
	}

	/// <summary>
	/// Called by PortalTraveller to transform velocity when passing through a portal
	/// </summary>
	public void TransformVelocity(Matrix4x4 transformMatrix)
	{
		// Transform horizontal velocity
		_horizontalVelocity = transformMatrix.MultiplyVector(_horizontalVelocity);
		
		// Transform vertical velocity
		Vector3 verticalVel = Vector3.up * _verticalSpeed;
		verticalVel = transformMatrix.MultiplyVector(verticalVel);
		_verticalSpeed = verticalVel.y;
		
		// Update internal look angles to match the new rotation
		if (yawTransform != null)
		{
			_currentYaw = yawTransform.eulerAngles.y;
		}
		else
		{
			_currentYaw = transform.eulerAngles.y;
		}
		
		if (pitchTransform != null)
		{
			float rawX = pitchTransform.localEulerAngles.x;
			_currentPitch = Mathf.DeltaAngle(0f, rawX);
		}
	}
    
}
