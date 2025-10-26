using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour {
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
	[SerializeField] private LayerMask groundMask = ~0;
	[SerializeField] private float groundCheckDistance = 0.15f;


    // Using the generated InputActions wrapper (strongly typed) from Assets/Settings/Input/PlayerInput.cs
    // We keep a reference to it and read/poll actions directly.
    private global::Input.PlayerInput _controls;

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

        // instantiate generated input wrapper (do not enable here; enable/disable with the component lifecycle)
        _controls = new global::Input.PlayerInput();

        // sync initial yaw/pitch from current transforms
        if (yawTransform != null) {
            _currentYaw = yawTransform.eulerAngles.y;
        }
        if (pitchTransform != null) {
            float rawX = pitchTransform.localEulerAngles.x;
            _currentPitch = Mathf.DeltaAngle(0f, rawX); // convert to [-180, 180]
        }
    }

    private void OnEnable() {
        // enable input actions
        if (_controls != null) {
            _controls.Enable();
        }
    }

    private void OnDisable() {
        if (_controls != null) {
            _controls.Disable();
        }
    }

    private void OnDestroy() {
        if (_controls != null) {
            _controls.Dispose();
            _controls = null;
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
		moveInput = (_controls != null) ? _controls.Player.Move.ReadValue<Vector2>() : Vector2.zero;
		lookInput = (_controls != null) ? _controls.Player.Look.ReadValue<Vector2>() : Vector2.zero;
		jumpPressed = (_controls != null) && _controls.Player.Jump.WasPerformedThisFrame();
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
		if (characterController == null || !characterController.enabled) return;
		CollisionFlags flags = characterController.Move(move);
		_isGrounded = (flags & CollisionFlags.Below) != 0 || CheckGrounded();
		if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;
	}

	private bool CheckGrounded() {
		float radius = characterController != null ? characterController.radius : 0.3f;
		Vector3 centerWorld = transform.position + Vector3.up * radius;
		float checkDist = groundCheckDistance + (characterController != null ? characterController.skinWidth : 0.02f);
		return Physics.SphereCast(centerWorld, Mathf.Max(0.01f, radius * 0.9f), Vector3.down, out _, checkDist, groundMask, QueryTriggerInteraction.Ignore);
	}

    
}
