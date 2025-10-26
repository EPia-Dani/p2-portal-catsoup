using UnityEngine;
using UnityEngine.InputSystem;

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


    // Using the generated InputActions wrapper (strongly typed) from Assets/Settings/Input/PlayerInput.cs
    // We keep a reference to it and read/poll actions directly.
    private global::Input.PlayerInput _controls;

    // movement state
    private float _gravity;
    private float _jumpSpeed;
    private float _verticalSpeed;
    private bool _isGrounded;
    private Vector2 _moveInput;

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
        // inputs
        _moveInput = (_controls != null) ? _controls.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        Vector2 lookInput = (_controls != null) ? _controls.Player.Look.ReadValue<Vector2>() : Vector2.zero;
        bool jumpPressed = (_controls != null) && _controls.Player.Jump.WasPerformedThisFrame();

        // look
        var lookScale = lookSensitivity * Time.deltaTime * 100f;
        _currentYaw += lookInput.x * lookScale;
        _currentPitch -= lookInput.y * lookScale;
        _currentPitch = Mathf.Clamp(_currentPitch, -89f, 89f); //hardcoded as we want to allow maximum pitch without confusing the orientation of the camera
        if (yawTransform != null) yawTransform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        if (pitchTransform != null) pitchTransform.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);

        // gravity and jump
        float dt = Time.deltaTime;
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

        // horizontal move
        Vector3 move = (transform.forward * _moveInput.y + transform.right * _moveInput.x) * (maxSpeedOnGround * Time.deltaTime);
        move.y = dy;

        if (characterController == null || !characterController.enabled) return;
        CollisionFlags flags = characterController.Move(move);
        _isGrounded = (flags & CollisionFlags.Below) != 0;
        if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f) _verticalSpeed = 0f;
    }

    
}
