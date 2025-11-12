using UnityEngine;
using Interact;

namespace Portal {
	/// <summary>
	/// Handles the full intro animation sequence including portal activation and door opening.
	/// </summary>
	public class IntroAnimation : MonoBehaviour {
		[Header("Portal Manager")]
		[SerializeField] PortalManager portalManager;

		[System.Serializable]
		public class PortalPreset {
			[Tooltip("The transform that defines the portal's position and rotation")]
			public Transform portalTransform;
			
			[Tooltip("The collider of the wall surface this portal is placed on")]
			public Collider surfaceCollider;
			
			[Tooltip("Scale of the portal (1.0 = default size)")]
			public float scale = 1f;
			
			[Tooltip("Whether this portal should be activated")]
			public bool activate = true;

			public bool IsValid() {
				return portalTransform != null && surfaceCollider != null;
			}
		}

		[Header("Blue Portal Preset")]
		[SerializeField] PortalPreset bluePortalPreset = new PortalPreset();

		[Header("Orange Portal Preset")]
		[SerializeField] PortalPreset orangePortalPreset = new PortalPreset();

		[Header("Door Settings")]
		[Tooltip("The Door component that handles door opening")]
		[SerializeField] Door door;

		[Header("Activation Settings")]
		[Tooltip("Automatically activate portals on Start")]
		[SerializeField] bool activateOnStart = true;

		[Tooltip("Remove existing portals before placing preset ones")]
		[SerializeField] bool removeExistingPortals = true;

		[Header("Time-Based Activation")]
		[Tooltip("Enable time-based delay before portals appear")]
		[SerializeField] bool useTimeDelay = false;

		[Tooltip("Delay in seconds before portals are activated (only used if Use Time Delay is enabled)")]
		[SerializeField] float activationDelay = 0f;

		void Start() {
			if (portalManager == null) {
				portalManager = FindObjectOfType<PortalManager>();
			}

			if (activateOnStart) {
				if (useTimeDelay && activationDelay > 0f) {
					StartCoroutine(DelayedActivation());
				} else {
					ActivatePresetPortals();
				}
			}
		}

		System.Collections.IEnumerator DelayedActivation() {
			yield return new WaitForSeconds(activationDelay);
			ActivatePresetPortals();
		}

		/// <summary>
		/// Activates both preset portals if they are configured
		/// </summary>
		public void ActivatePresetPortals() {
			if (portalManager == null) {
				Debug.LogError("[IntroAnimation] PortalManager not found! Cannot activate preset portals.");
				return;
			}

			if (removeExistingPortals) {
				portalManager.RemovePortal(PortalId.Blue);
				portalManager.RemovePortal(PortalId.Orange);
			}

			if (bluePortalPreset.activate && bluePortalPreset.IsValid()) {
				PlacePresetPortal(PortalId.Blue, bluePortalPreset);
			}

			if (orangePortalPreset.activate && orangePortalPreset.IsValid()) {
				PlacePresetPortal(PortalId.Orange, orangePortalPreset);
			}

			// Open the door when portals are activated
			OpenDoor();
		}

		/// <summary>
		/// Opens the door
		/// </summary>
		public void OpenDoor() {
			if (door != null) {
				door.Open();
				Debug.Log("[IntroAnimation] Opening door");
			} else {
				Debug.LogWarning("[IntroAnimation] Door component not assigned!");
			}
		}

		/// <summary>
		/// Closes the door
		/// </summary>
		public void CloseDoor() {
			if (door != null) {
				door.Close();
			}
		}

		void PlacePresetPortal(PortalId id, PortalPreset preset) {
			if (preset.portalTransform == null) {
				Debug.LogError($"[IntroAnimation] Portal transform is null for {id} portal!");
				return;
			}

			if (preset.surfaceCollider == null) {
				Debug.LogError($"[IntroAnimation] Surface collider is null for {id} portal!");
				return;
			}

			// Calculate portal orientation vectors from transform
			// Portal forward points INTO the wall (opposite of normal)
			Vector3 portalForward = preset.portalTransform.forward;
			Vector3 normal = -portalForward; // Normal points OUT of the wall
			Vector3 up = preset.portalTransform.up;
			Vector3 right = preset.portalTransform.right;

			Vector3 position = preset.portalTransform.position;

			// Place the portal using PortalManager
			portalManager.PlacePortal(
				id,
				position,
				normal,
				right,
				up,
				preset.surfaceCollider,
				preset.scale
			);

			Debug.Log($"[IntroAnimation] Placed {id} portal at {position} with scale {preset.scale}");
		}

		void OnValidate() {
			// Validate preset configurations in editor
			if (bluePortalPreset.portalTransform != null && bluePortalPreset.surfaceCollider == null) {
				Debug.LogWarning("[IntroAnimation] Blue portal preset has transform but no surface collider assigned!");
			}
			if (orangePortalPreset.portalTransform != null && orangePortalPreset.surfaceCollider == null) {
				Debug.LogWarning("[IntroAnimation] Orange portal preset has transform but no surface collider assigned!");
			}
			
			// Ensure delay is not negative
			if (activationDelay < 0f) {
				activationDelay = 0f;
			}
		}

		#if UNITY_EDITOR
		/// <summary>
		/// Draws gizmos in the editor to visualize preset portal positions
		/// </summary>
		void OnDrawGizmos() {
			if (bluePortalPreset.portalTransform != null && bluePortalPreset.activate) {
				Gizmos.color = Color.cyan;
				DrawPortalGizmo(bluePortalPreset.portalTransform, bluePortalPreset.scale);
			}

			if (orangePortalPreset.portalTransform != null && orangePortalPreset.activate) {
				Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
				DrawPortalGizmo(orangePortalPreset.portalTransform, orangePortalPreset.scale);
			}
		}

		void DrawPortalGizmo(Transform portalTransform, float scale) {
			Vector3 pos = portalTransform.position;
			Vector3 forward = portalTransform.forward;
			Vector3 up = portalTransform.up;
			Vector3 right = portalTransform.right;

			// Draw portal plane (simplified as a rectangle)
			float width = 0.9f * scale;
			float height = 1.6f * scale;

			Vector3 topLeft = pos + up * height * 0.5f - right * width * 0.5f;
			Vector3 topRight = pos + up * height * 0.5f + right * width * 0.5f;
			Vector3 bottomLeft = pos - up * height * 0.5f - right * width * 0.5f;
			Vector3 bottomRight = pos - up * height * 0.5f + right * width * 0.5f;

			// Draw rectangle outline
			Gizmos.DrawLine(topLeft, topRight);
			Gizmos.DrawLine(topRight, bottomRight);
			Gizmos.DrawLine(bottomRight, bottomLeft);
			Gizmos.DrawLine(bottomLeft, topLeft);

			// Draw normal (pointing out of wall)
			Gizmos.color = Color.yellow;
			Gizmos.DrawRay(pos, -forward * 0.2f);
		}
		#endif
	}
}
