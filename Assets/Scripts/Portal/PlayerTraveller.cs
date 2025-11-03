using UnityEngine;
using System.Reflection;

namespace Portal {
	/// <summary>
	/// Player-specific traveller that handles portal transitions with camera logic.
	/// </summary>
	[RequireComponent(typeof(FPSController))]
	[RequireComponent(typeof(CharacterController))]
	public class PlayerTraveller : PortalTraveller {
		private Camera _mainCamera;
		private Transform _pitchTransform;
		private FPSController _fpsController;
		private CharacterController _characterController;
		private PortalTeleporter _currentSourcePortal;
		private PortalTeleporter _currentDestPortal;

		protected override void Awake() {
			base.Awake();
			_fpsController = GetComponent<FPSController>();
			_characterController = GetComponent<CharacterController>();
			_mainCamera = Camera.main;
		}

		/// <summary>
		/// Called when entering a portal trigger.
		/// Stores the current camera pitch transform for portal camera following.
		/// </summary>
		public override void OnPortalEnter(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			base.OnPortalEnter(sourcePortal, destPortal);

			_currentSourcePortal = sourcePortal;
			_currentDestPortal = destPortal;

			if (_fpsController != null) {
				var pitchField = typeof(FPSController).GetField("pitchTransform",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_pitchTransform = pitchField?.GetValue(_fpsController) as Transform;
			}
		}

		private void LateUpdate() {
			// Update camera while inside a portal
			if (_currentSourcePortal != null && _currentDestPortal != null && _mainCamera != null && _pitchTransform != null) {
				PortalRenderer sourceRenderer = _currentSourcePortal.GetComponent<PortalRenderer>();
				PortalRenderer destRenderer = _currentDestPortal.GetComponent<PortalRenderer>();

				if (sourceRenderer != null && destRenderer != null) {
					// Calculate portal camera position based on player's pitchTransform (camera) position
					Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
					Matrix4x4 step = destRenderer.transform.localToWorldMatrix * mirror * sourceRenderer.transform.worldToLocalMatrix;
					Matrix4x4 worldMatrix = step * _pitchTransform.localToWorldMatrix;

					Vector3 portalCameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
					Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
					Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
					Quaternion portalCameraRot = Quaternion.LookRotation(forward, up);

					_mainCamera.transform.position = portalCameraPos;
					_mainCamera.transform.rotation = portalCameraRot;
				}
			}
		}

		/// <summary>
		/// Called each frame while inside a portal trigger.
		/// Detects when the player crosses the portal and initiates teleportation.
		/// </summary>
		public override void OnPortalStay(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			// Check if crossed portal plane
			if (HasCrossedPortalPlane(sourcePortal.transform.position, sourcePortal.transform.forward)) {
				TeleportThroughPortal(sourcePortal, destPortal);
				destPortal.NotifyTravellerCrossed(this);
			}

			_lastPosition = transform.position;
		}

		/// <summary>
		/// Called when exiting a portal trigger.
		/// Restores the main camera to the player's actual camera position.
		/// </summary>
		public override void OnPortalExit(PortalTeleporter sourcePortal) {
			base.OnPortalExit(sourcePortal);

			// Stop following portal camera
			_currentSourcePortal = null;
			_currentDestPortal = null;

			// Restore camera to pitchTransform
			if (_mainCamera != null && _pitchTransform != null) {
				_mainCamera.transform.position = _pitchTransform.position;
				_mainCamera.transform.rotation = _pitchTransform.rotation;
			}

			_pitchTransform = null;
		}

		/// <summary>
		/// Teleports the player through the portal, adjusting position, rotation, and camera.
		/// </summary>
		private void TeleportThroughPortal(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			PortalRenderer sourceRenderer = sourcePortal.GetComponent<PortalRenderer>();
			PortalRenderer destRenderer = destPortal.GetComponent<PortalRenderer>();

			if (sourceRenderer == null || destRenderer == null) return;

			TransformThroughPortal(sourceRenderer, destRenderer, out Vector3 newPosition, out Quaternion newCameraRotation);
			newPosition += destPortal.transform.forward * 0.1f;

			// Player body should face same direction as camera (horizontal only)
			Quaternion playerRotation = Quaternion.Euler(0, newCameraRotation.eulerAngles.y, 0);

			// Move player through character controller
			if (_characterController != null) {
				_characterController.enabled = false;
				transform.SetPositionAndRotation(newPosition, playerRotation);
				_characterController.enabled = true;
			} else {
				transform.SetPositionAndRotation(newPosition, playerRotation);
			}

			// Update camera rotations to match portal camera (separate from player body)
			if (_fpsController != null) {
				var yawField = typeof(FPSController).GetField("yawTransform", BindingFlags.NonPublic | BindingFlags.Instance);
				var pitchField = typeof(FPSController).GetField("pitchTransform", BindingFlags.NonPublic | BindingFlags.Instance);

				Transform yaw = null;
				Transform pitch = null;

				if (yawField?.GetValue(_fpsController) is Transform y) {
					yaw = y;
					// Set yaw to match camera rotation (not player body)
					yaw.rotation = Quaternion.Euler(0, newCameraRotation.eulerAngles.y, 0);
				}

				if (pitchField?.GetValue(_fpsController) is Transform p) {
					pitch = p;
					float pitchAngle = newCameraRotation.eulerAngles.x;
					if (pitchAngle > 180) pitchAngle -= 360;
					pitch.localRotation = Quaternion.Euler(pitchAngle, 0, 0);
				}

				// Update FPSController internal state to prevent override
				var currentYawField = typeof(FPSController).GetField("_currentYaw", BindingFlags.NonPublic | BindingFlags.Instance);
				var currentPitchField = typeof(FPSController).GetField("_currentPitch", BindingFlags.NonPublic | BindingFlags.Instance);

				if (yaw != null && currentYawField != null) {
					currentYawField.SetValue(_fpsController, yaw.eulerAngles.y);
				}
				if (pitch != null && currentPitchField != null) {
					float rawX = pitch.localEulerAngles.x;
					currentPitchField.SetValue(_fpsController, Mathf.DeltaAngle(0f, rawX));
				}
			}

			// Restore camera to pitchTransform immediately after teleport
			if (_mainCamera != null && _pitchTransform != null) {
				_mainCamera.transform.position = _pitchTransform.position;
				_mainCamera.transform.rotation = _pitchTransform.rotation;
			}

			// Stop following portal camera - return to normal camera control
			_pitchTransform = null;
		}
	}
}

