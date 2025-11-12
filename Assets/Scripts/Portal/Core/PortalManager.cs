using UnityEngine;

namespace Portal {
	public class PortalManager : MonoBehaviour {
		[Header("Portals")]
		[SerializeField] PortalRenderer bluePortal;
		[SerializeField] PortalRenderer orangePortal;
		[SerializeField] Transform bluePortalMesh;
		[SerializeField] Transform orangePortalMesh;
		[SerializeField] Transform blueCollidersTransform;
		[SerializeField] Transform orangeCollidersTransform;

		[Header("Settings")]
		[SerializeField] int textureSize = 1024;
		[SerializeField] int recursionLimit = 2;

		PortalState _blueState;
		PortalState _orangeState;
		Vector3 _blueBaseScale = Vector3.one;
		Vector3 _orangeBaseScale = Vector3.one;
		Vector3 _blueMeshBaseScale = Vector3.one;
		Vector3 _orangeMeshBaseScale = Vector3.one;
		Vector3 _blueColliderBaseSize = Vector3.one;
		Vector3 _orangeColliderBaseSize = Vector3.one;
		Vector3 _blueCollidersBaseScale = Vector3.one;
		Vector3 _orangeCollidersBaseScale = Vector3.one;

		public PortalRenderer BluePortal => bluePortal;
		public PortalRenderer OrangePortal => orangePortal;

		void Awake() {
			// Link portals together
			if (bluePortal && orangePortal) {
				bluePortal.pair = orangePortal;
				orangePortal.pair = bluePortal;
			}

			// Store base scales and disable portals (they start disabled)
			if (bluePortal) {
				_blueBaseScale = bluePortal.transform.localScale;
				bluePortal.IsReadyToRender = false;
				bluePortal.gameObject.SetActive(false);
				// Store base collider size if it exists (from colliders transform)
				if (blueCollidersTransform) {
					var blueCollider = blueCollidersTransform.GetComponent<BoxCollider>();
					if (blueCollider) {
						_blueColliderBaseSize = blueCollider.size;
					}
				}
			}
			if (orangePortal) {
				_orangeBaseScale = orangePortal.transform.localScale;
				orangePortal.IsReadyToRender = false;
				orangePortal.gameObject.SetActive(false);
				// Store base collider size if it exists (from colliders transform)
				if (orangeCollidersTransform) {
					var orangeCollider = orangeCollidersTransform.GetComponent<BoxCollider>();
					if (orangeCollider) {
						_orangeColliderBaseSize = orangeCollider.size;
					}
				}
			}
			if (bluePortalMesh) {
				_blueMeshBaseScale = bluePortalMesh.localScale;
				bluePortalMesh.gameObject.SetActive(false);
			}
			if (orangePortalMesh) {
				_orangeMeshBaseScale = orangePortalMesh.localScale;
				orangePortalMesh.gameObject.SetActive(false);
			}
			if (blueCollidersTransform) {
				_blueCollidersBaseScale = blueCollidersTransform.localScale;
			}
			if (orangeCollidersTransform) {
				_orangeCollidersBaseScale = orangeCollidersTransform.localScale;
			}

			// Setup animators
			var blueAnimator = bluePortal?.GetComponent<PortalAnimator>() ?? bluePortal?.GetComponentInChildren<PortalAnimator>();
			var orangeAnimator = orangePortal?.GetComponent<PortalAnimator>() ?? orangePortal?.GetComponentInChildren<PortalAnimator>();
			if (blueAnimator && bluePortalMesh) blueAnimator.SetMeshTransform(bluePortalMesh);
			if (orangeAnimator && orangePortalMesh) orangeAnimator.SetMeshTransform(orangePortalMesh);
		}

		void Start() {
			ApplySettings();
		}

		void OnValidate() {
			recursionLimit = Mathf.Max(1, recursionLimit);
			textureSize = Mathf.Clamp(textureSize, 256, 4096);
			if (Application.isPlaying) {
				ApplySettings();
			}
		}

		void LateUpdate() {
			// Update render readiness based on animator state (only if portals are enabled)
			if (bluePortal != null && bluePortal.gameObject.activeInHierarchy) {
				UpdateRenderReadiness(bluePortal, PortalId.Blue);
			}
			if (orangePortal != null && orangePortal.gameObject.activeInHierarchy) {
				UpdateRenderReadiness(orangePortal, PortalId.Orange);
			}
		}

		void UpdateRenderReadiness(PortalRenderer renderer, PortalId id) {
			if (renderer == null || !renderer.gameObject.activeInHierarchy) return;
			var animator = renderer.GetComponent<PortalAnimator>() ?? renderer.GetComponentInChildren<PortalAnimator>();
			if (animator == null) return;
			renderer.IsReadyToRender = animator.IsOpening || animator.IsFullyOpen;
		}

		public void PlacePortal(PortalId id, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float scale = 1f) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			Transform mesh = id == PortalId.Blue ? bluePortalMesh : orangePortalMesh;
			if (renderer == null) return;

			// Enable the portal GameObject first (enables sounds, components, etc.)
			EnablePortal(id);

			// Apply settings to the portal now that it's enabled
			ApplySettingsToPortal(id);

			// Update state
			PortalState state = new PortalState {
				IsPlaced = true,
				Surface = surface,
				Position = position,
				Normal = normal.normalized,
				Right = right.normalized,
				Up = up.normalized,
				Scale = scale
			};

			if (id == PortalId.Blue) {
				_blueState = state;
			} else {
				_orangeState = state;
			}

			// Apply to renderer
			renderer.SetVisible(true);
			renderer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-normal, up));
			renderer.transform.localScale = id == PortalId.Blue ? _blueBaseScale : _orangeBaseScale;
			renderer.SetWallCollider(surface);
			renderer.PortalScale = scale;

			// Scale trigger collider to match portal size
			UpdatePortalTriggerCollider(renderer, scale);

			// Apply to mesh
			if (mesh != null) {
				mesh.gameObject.SetActive(true);
				Vector3 meshBaseScale = id == PortalId.Blue ? _blueMeshBaseScale : _orangeMeshBaseScale;
				mesh.localScale = new Vector3(meshBaseScale.x * scale, meshBaseScale.y, meshBaseScale.z * scale);
			}

			// Scale colliders child transform
			Transform collidersTransform = id == PortalId.Blue ? blueCollidersTransform : orangeCollidersTransform;
			if (collidersTransform != null) {
				Vector3 collidersBaseScale = id == PortalId.Blue ? _blueCollidersBaseScale : _orangeCollidersBaseScale;
				collidersTransform.localScale = collidersBaseScale * scale;
			}

			UpdateVisualStates();
		}

		public void RemovePortal(PortalId id) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			Transform mesh = id == PortalId.Blue ? bluePortalMesh : orangePortalMesh;

			if (id == PortalId.Blue) {
				_blueState = PortalState.Empty;
			} else {
				_orangeState = PortalState.Empty;
			}

			// Only try to access components if portal is enabled
			if (renderer != null && renderer.gameObject.activeInHierarchy) {
				renderer.SetVisible(false);
				renderer.IsReadyToRender = false;
				renderer.PortalScale = 1f;

				var animator = renderer.GetComponent<PortalAnimator>() ?? renderer.GetComponentInChildren<PortalAnimator>();
				if (animator != null) {
					animator.HideImmediate();
				}
			}

			if (mesh != null) {
				mesh.gameObject.SetActive(false);
			}

			// Update visual states before disabling (so it can handle the state change)
			UpdateVisualStates();

			// Disable the portal GameObject (disables sounds, etc.)
			DisablePortal(id);
		}

		public bool TryGetState(PortalId id, out PortalState state) {
			state = id == PortalId.Blue ? _blueState : _orangeState;
			return state.IsPlaced;
		}

		public PortalState GetState(PortalId id) {
			return id == PortalId.Blue ? _blueState : _orangeState;
		}

		/// <summary>
		/// Enables a portal GameObject (enables sounds, components, etc.)
		/// </summary>
		public void EnablePortal(PortalId id) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			if (renderer != null) {
				renderer.gameObject.SetActive(true);
			}
		}

		/// <summary>
		/// Disables a portal GameObject (disables sounds, components, etc.)
		/// </summary>
		public void DisablePortal(PortalId id) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			if (renderer != null) {
				renderer.gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Disables all portals (disables sounds, components, etc.)
		/// </summary>
		public void DisableAllPortals() {
			if (bluePortal != null) {
				bluePortal.gameObject.SetActive(false);
			}
			if (orangePortal != null) {
				orangePortal.gameObject.SetActive(false);
			}
		}

		void ApplySettings() {
			if (bluePortal) bluePortal.ConfigurePortal(textureSize, textureSize, recursionLimit, 1);
			if (orangePortal) orangePortal.ConfigurePortal(textureSize, textureSize, recursionLimit, 1);
		}

		/// <summary>
		/// Applies settings to a specific portal (called after enabling it)
		/// </summary>
		void ApplySettingsToPortal(PortalId id) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			if (renderer != null) {
				renderer.ConfigurePortal(textureSize, textureSize, recursionLimit, 1);
			}
		}

		public void SetRecursionLimit(int value) {
			recursionLimit = Mathf.Max(1, value);
			ApplySettings();
		}

		public void SetFrameSkipInterval(int value) {
			// Frame skipping removed for simplicity - this is a no-op for UI compatibility
		}

		void UpdatePortalTriggerCollider(PortalRenderer renderer, float scale) {
			if (!renderer) return;

			// Get BoxCollider from colliders transform (manually added, scales with portal)
			Transform collidersTransform = (renderer == bluePortal) ? blueCollidersTransform : orangeCollidersTransform;
			if (!collidersTransform) return;

			BoxCollider triggerCollider = collidersTransform.GetComponent<BoxCollider>();
			if (!triggerCollider) return; // Collider will be manually added

			// Get base collider size for this portal
			Vector3 baseSize = (renderer == bluePortal) ? _blueColliderBaseSize : _orangeColliderBaseSize;
			
			// If base size wasn't stored (first time), use current size as base
			if (baseSize == Vector3.one && triggerCollider.size != Vector3.one) {
				baseSize = triggerCollider.size;
				if (renderer == bluePortal) {
					_blueColliderBaseSize = baseSize;
				} else {
					_orangeColliderBaseSize = baseSize;
				}
			}

			// Keep collider smaller than portal - don't scale X/Y with portal scale
			// Just use base size, centered on portal
			float increasedThickness = baseSize.z * 1.5f;
			triggerCollider.size = new Vector3(baseSize.x, baseSize.y, increasedThickness);
			triggerCollider.center = Vector3.zero; // Ensure it's centered
		}

		void UpdateVisualStates() {
			bool bluePlaced = _blueState.IsPlaced;
			bool orangePlaced = _orangeState.IsPlaced;
			bool bothPlaced = bluePlaced && orangePlaced;

			UpdatePortalVisuals(PortalId.Blue, bluePlaced, bothPlaced);
			UpdatePortalVisuals(PortalId.Orange, orangePlaced, bothPlaced);
		}

		void UpdatePortalVisuals(PortalId id, bool placed, bool bothPlaced) {
			PortalRenderer renderer = id == PortalId.Blue ? bluePortal : orangePortal;
			Transform mesh = id == PortalId.Blue ? bluePortalMesh : orangePortalMesh;
			if (renderer == null) return;

			// Ensure portal is enabled before accessing its components
			if (!renderer.gameObject.activeInHierarchy) {
				if (placed) {
					EnablePortal(id);
				} else {
					// Portal is being removed, don't try to access disabled components
					return;
				}
			}

			if (mesh != null) {
				mesh.gameObject.SetActive(placed);
			}

			bool shouldRender = placed && bothPlaced;
			renderer.SetVisible(shouldRender);
			if (!shouldRender) {
				renderer.IsReadyToRender = false;
			}

			var animator = renderer.GetComponent<PortalAnimator>() ?? renderer.GetComponentInChildren<PortalAnimator>();
			if (animator == null) return;

			// Calculate target scale
			float targetScaleX = 0f;
			float targetScaleZ = 0f;
			if (placed && mesh != null) {
				PortalState state = id == PortalId.Blue ? _blueState : _orangeState;
				Vector3 meshBaseScale = id == PortalId.Blue ? _blueMeshBaseScale : _orangeMeshBaseScale;
				targetScaleX = meshBaseScale.x * state.Scale;
				targetScaleZ = meshBaseScale.z * state.Scale;
			}

			animator.HideImmediate();
			if (placed) {
				animator.PlayAppear(targetScaleX, targetScaleZ);
				if (bothPlaced) {
					animator.StartOpening();
				}
			}
		}
	}
}
