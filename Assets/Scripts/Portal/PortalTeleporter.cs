using UnityEngine;

namespace Portal {
	[RequireComponent(typeof(Collider))]
	public class PortalTeleporter : MonoBehaviour {
		[SerializeField] private PortalTeleporter linkedPortal;
		[SerializeField] private Collider wallCollider;

		private Camera _mainCamera;
		private Transform _pitchTransform;
		private PortalRenderer _sourceRenderer;
		private PortalRenderer _destRenderer;
		private Vector3 _lastPlayerPosition;

		private void Awake() {
			if (GetComponent<Collider>() is Collider c && !c.isTrigger) {
				c.isTrigger = true;
			}
			_mainCamera = Camera.main;
			_sourceRenderer = GetComponent<PortalRenderer>();
		}

		private void OnTriggerEnter(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller == null || linkedPortal == null) return;

			if (wallCollider != null) traveller.IgnoreCollisionWith(wallCollider, true);
			
			_destRenderer = linkedPortal.GetComponent<PortalRenderer>();
			
			FPSController fpsController = traveller.GetComponent<FPSController>();
			if (fpsController != null) {
				var pitchField = typeof(FPSController).GetField("pitchTransform", 
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				_pitchTransform = pitchField?.GetValue(fpsController) as Transform;
			}

			_lastPlayerPosition = traveller.transform.position;
		}

		private void LateUpdate() {
			if (_mainCamera == null || _pitchTransform == null || _sourceRenderer == null || _destRenderer == null) return;

			// Calculate portal camera position based on player's pitchTransform (camera) position
			Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
			Matrix4x4 step = _destRenderer.transform.localToWorldMatrix * mirror * _sourceRenderer.transform.worldToLocalMatrix;
			Matrix4x4 worldMatrix = step * _pitchTransform.localToWorldMatrix;

			Vector3 portalCameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			Quaternion portalCameraRot = Quaternion.LookRotation(forward, up);

			_mainCamera.transform.position = portalCameraPos;
			_mainCamera.transform.rotation = portalCameraRot;
		}

		private void OnTriggerStay(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller == null || linkedPortal == null) return;

			Vector3 currentPosition = traveller.transform.position;

			if (_lastPlayerPosition == Vector3.zero) {
				_lastPlayerPosition = currentPosition;
				return;
			}

			// Check if crossed portal plane
			Vector3 normal = transform.forward;
			float prevDot = Vector3.Dot(_lastPlayerPosition - transform.position, normal);
			float currDot = Vector3.Dot(currentPosition - transform.position, normal);

			if (prevDot <= 0.01f && currDot > 0.01f) {
				TeleportPlayer(traveller);
				linkedPortal._lastPlayerPosition = Vector3.zero;
				
				// Restore camera to pitchTransform immediately after teleport
				FPSController fpsController = traveller.GetComponent<FPSController>();
				if (fpsController != null && _mainCamera != null) {
					var pitchField = typeof(FPSController).GetField("pitchTransform", 
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					Transform pitch = pitchField?.GetValue(fpsController) as Transform;
					if (pitch != null) {
						_mainCamera.transform.position = pitch.position;
						_mainCamera.transform.rotation = pitch.rotation;
					}
				}
				
				// Stop following portal camera - return to normal camera control
				_pitchTransform = null;
				_destRenderer = null;
			}

			_lastPlayerPosition = currentPosition;
		}

		private void TeleportPlayer(PortalTraveller traveller) {
			if (_sourceRenderer == null || _destRenderer == null) return;

			Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
			Matrix4x4 step = _destRenderer.transform.localToWorldMatrix * mirror * _sourceRenderer.transform.worldToLocalMatrix;
			Matrix4x4 worldMatrix = step * traveller.transform.localToWorldMatrix;

			Vector3 newPosition = worldMatrix.MultiplyPoint(Vector3.zero) + linkedPortal.transform.forward * 0.1f;
			
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			Quaternion newCameraRotation = Quaternion.LookRotation(forward, up);
			
			// Player body should face same direction as camera (horizontal only)
			Quaternion playerRotation = Quaternion.Euler(0, newCameraRotation.eulerAngles.y, 0);

			CharacterController cc = traveller.GetComponent<CharacterController>();
			if (cc != null) {
				cc.enabled = false;
				traveller.transform.SetPositionAndRotation(newPosition, playerRotation);
				cc.enabled = true;
			} else {
				traveller.transform.SetPositionAndRotation(newPosition, playerRotation);
			}

			// Update camera rotations to match portal camera (separate from player body)
			FPSController fpsController = traveller.GetComponent<FPSController>();
			if (fpsController != null) {
				var yawField = typeof(FPSController).GetField("yawTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var pitchField = typeof(FPSController).GetField("pitchTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				Transform yaw = null;
				Transform pitch = null;

				if (yawField?.GetValue(fpsController) is Transform y) {
					yaw = y;
					// Set yaw to match camera rotation (not player body)
					yaw.rotation = Quaternion.Euler(0, newCameraRotation.eulerAngles.y, 0);
				}

				if (pitchField?.GetValue(fpsController) is Transform p) {
					pitch = p;
					float pitchAngle = newCameraRotation.eulerAngles.x;
					if (pitchAngle > 180) pitchAngle -= 360;
					pitch.localRotation = Quaternion.Euler(pitchAngle, 0, 0);
				}
				
				// Update FPSController internal state to prevent override
				var currentYawField = typeof(FPSController).GetField("_currentYaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				var currentPitchField = typeof(FPSController).GetField("_currentPitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				
				if (yaw != null && currentYawField != null) {
					currentYawField.SetValue(fpsController, yaw.eulerAngles.y);
				}
				if (pitch != null && currentPitchField != null) {
					float rawX = pitch.localEulerAngles.x;
					currentPitchField.SetValue(fpsController, Mathf.DeltaAngle(0f, rawX));
				}
			}
		}

		private void OnTriggerExit(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller != null && wallCollider != null) {
				traveller.IgnoreCollisionWith(wallCollider, false);
			}

			// Restore camera to pitchTransform
			if (_mainCamera != null && _pitchTransform != null) {
				_mainCamera.transform.position = _pitchTransform.position;
				_mainCamera.transform.rotation = _pitchTransform.rotation;
			}

			_pitchTransform = null;
			_destRenderer = null;
			_lastPlayerPosition = Vector3.zero;
		}

		public void SetWallCollider(Collider collider) {
			wallCollider = collider;
		}
	}
}
