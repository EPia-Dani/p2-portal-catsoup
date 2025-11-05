using UnityEngine;

public class GameBootstrap : MonoBehaviour {
	[SerializeField] private CursorLockMode lockMode = CursorLockMode.Locked;
	[SerializeField] private bool cursorVisible = false;

	private void Awake() {
		// Initialize InputManager singleton (must happen before other components)
		InputManager.Initialize();
		CursorUtility.Apply(lockMode, cursorVisible);

		

	}

	private void OnDestroy() {
		InputManager.Shutdown();
	}
}

/// <summary>
/// Singleton manager for PlayerInput to ensure only one instance exists
/// </summary>
public static class InputManager {
	private static Input.PlayerInput _playerInput;

	public static Input.PlayerInput PlayerInput {
		get {
			// Lazy initialize if not already done
			if (_playerInput == null) {
				_playerInput = new Input.PlayerInput();
				_playerInput.Enable();
			}
			return _playerInput;
		}
	}

	public static void Initialize() {
		// Explicitly initialize (will be called by GameBootstrap, but also happens lazily)
		if (_playerInput == null) {
			_playerInput = new Input.PlayerInput();
			_playerInput.Enable();
		}
	}

	public static void Shutdown() {
		if (_playerInput != null) {
			_playerInput.Disable();
			_playerInput.Dispose();
			_playerInput = null;
		}
	}
}


