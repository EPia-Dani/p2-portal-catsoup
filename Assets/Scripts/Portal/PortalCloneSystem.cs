// PortalCloneSystem.cs - Handles visual clones for held objects passing through portals
using UnityEngine;

namespace Portal {
	public class PortalCloneSystem : MonoBehaviour {
		[Header("Clone Settings")]
		[SerializeField] private LayerMask portalLayer = -1;
		
		private GameObject _clone;
		private PortalRenderer _currentPortal;
		private PortalRenderer _currentDestination;
		private bool _isHeld;
		
		public bool HasClone => _clone != null;
		public GameObject Clone => _clone;

		void Update() {
			if (!_isHeld) return;
			
			// Check if we're touching a portal by checking nearby colliders
			CheckForPortalContact();
			
			// Update clone position/rotation if it exists
			if (_clone && _currentPortal && _currentDestination) {
				UpdateCloneTransform();
			}
		}

		void CheckForPortalContact() {
			// Use overlap sphere to detect nearby portals
			Collider[] nearby = Physics.OverlapSphere(transform.position, 0.5f);
			
			PortalRenderer touchingPortal = null;
			foreach (var col in nearby) {
				var portal = col.GetComponent<PortalRenderer>();
				if (portal != null && portal.pair != null) {
					touchingPortal = portal;
					break;
				}
			}
			
			// Only create ONE clone if touching a portal and we don't have one yet
			if (touchingPortal != null && _clone == null) {
				Debug.Log($"[PortalCloneSystem] Creating clone for {gameObject.name} touching portal {touchingPortal.name}");
				CreateClone(touchingPortal);
			}
			// If touching a different portal, destroy old clone and create new one
			else if (touchingPortal != null && touchingPortal != _currentPortal && _clone != null) {
				Debug.Log($"[PortalCloneSystem] Switching clone from {_currentPortal?.name} to {touchingPortal.name}");
				DestroyClone();
				CreateClone(touchingPortal);
			}
			// Destroy clone if not touching any portal
			else if (touchingPortal == null && _clone != null) {
				Debug.Log($"[PortalCloneSystem] No portal contact, destroying clone for {gameObject.name}");
				DestroyClone();
			}
		}

		public void SetHeld(bool held) {
			_isHeld = held;
			if (!held) {
				// When dropped, restore collisions first
				RestorePortalCollisions();
				
				if (_clone && _currentPortal && _currentDestination) {
					// Only swap with clone if we're actually on the destination side (where clone is)
					// Check which side of the portal we're on
					Vector3 offsetFromPortal = transform.position - _currentPortal.transform.position;
					float dot = Vector3.Dot(offsetFromPortal, _currentPortal.transform.forward);
					
					// If dot > 0, we're on the "exiting" side (where clone is) - swap
					// If dot <= 0, we're on the "entering" side - don't swap, just destroy clone
					if (dot > 0) {
						// We're on the destination side, swap with clone
						SwapWithClone();
					} else {
						// We're still on the source side, just destroy the clone
						Debug.Log($"[PortalCloneSystem] Dropped on source side, destroying clone for {gameObject.name}");
						DestroyClone();
						
						// Properly initialize portal tracking so it doesn't immediately teleport
						InitializePortalTracking();
					}
				} else {
					DestroyClone();
					
					// If no clone but we might be in a portal, initialize tracking
					InitializePortalTracking();
				}
			}
		}
		
		void InitializePortalTracking() {
			// Check if we're inside any portal trigger and properly initialize tracking
			Collider[] nearby = Physics.OverlapSphere(transform.position, 0.5f);
			foreach (var col in nearby) {
				var portal = col.GetComponent<PortalRenderer>();
				if (portal != null) {
					var handler = portal.GetComponent<PortalTravellerHandler>();
					if (handler != null) {
						var traveller = GetComponent<PortalTraveller>();
						if (traveller != null) {
							// Initialize previousOffsetFromPortal to current position
							// This prevents immediate teleportation when dropped
							Vector3 offset = transform.position - portal.transform.position;
							traveller.previousOffsetFromPortal = offset;
							
							// Add to tracking if not already tracked
							handler.OnTravellerEnterPortal(traveller, justTeleported: false);
						}
					}
				}
			}
		}

		public void OnPlayerTeleport() {
			// When player teleports, swap with clone if it exists
			if (_clone) {
				SwapWithClone();
			}
		}

		void CreateClone(PortalRenderer portal) {
			if (!portal.pair || _clone != null) return; // Only create if we don't have one
			
			_currentPortal = portal;
			_currentDestination = portal.pair;
			
			// Ensure collision is ignored with both portal walls
			SetupPortalCollisions();
			
			// Create ONE clone
			_clone = new GameObject($"{gameObject.name}_Clone");
			_clone.transform.SetParent(null);
			
			// Copy visual components only (no physics, no scripts)
			CopyVisualComponents(gameObject, _clone);
			
			// Position clone on other side
			UpdateCloneTransform();
			
			// Make clone non-interactive
			DisableCloneComponents(_clone);
		}

		void SetupPortalCollisions() {
			if (!_currentPortal || !_currentDestination) return;
			
			var traveller = GetComponent<PortalTraveller>();
			if (!traveller) {
				Debug.LogWarning($"[PortalCloneSystem] No PortalTraveller on {gameObject.name}!");
				return;
			}
			
			// Ignore collision with source portal wall
			var sourceHandler = _currentPortal.GetComponent<PortalTravellerHandler>();
			if (sourceHandler) {
				Debug.Log($"[PortalCloneSystem] Ignoring collision with source portal wall for {gameObject.name}");
				sourceHandler.SetCollisionIgnore(traveller, true);
			}
			
			// Ignore collision with destination portal wall
			var destHandler = _currentDestination.GetComponent<PortalTravellerHandler>();
			if (destHandler) {
				Debug.Log($"[PortalCloneSystem] Ignoring collision with destination portal wall for {gameObject.name}");
				destHandler.SetCollisionIgnore(traveller, true);
			}
		}

		void UpdateCloneTransform() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			float scaleRatio = _currentDestination.PortalScale / _currentPortal.PortalScale;
			
			// Use PortalTransformUtility to position clone correctly
			// This maintains the same relative position to the portal, just on the other side
			PortalTransformUtility.TransformThroughPortal(_currentPortal, _currentDestination,
				transform.position, transform.rotation, scaleRatio,
				out Vector3 newPos, out Quaternion newRot);
			
			_clone.transform.SetPositionAndRotation(newPos, newRot);
			
			// Apply scale - clone should match the scale the real object would have on the other side
			Vector3 baseScale = transform.localScale;
			if (Mathf.Abs(scaleRatio - 1f) > 0.001f) {
				_clone.transform.localScale = baseScale * scaleRatio;
			} else {
				_clone.transform.localScale = baseScale;
			}
		}

		void SwapWithClone() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			// Store clone's transform (this is where the real object should be)
			Vector3 clonePos = _clone.transform.position;
			Quaternion cloneRot = _clone.transform.rotation;
			Vector3 cloneScale = _clone.transform.localScale;
			
			// Get scale ratio for PortalTraveller
			float scaleRatio = _currentDestination.PortalScale / _currentPortal.PortalScale;
			
			// Capture Rigidbody velocity BEFORE teleporting (if it exists)
			Rigidbody rb = GetComponent<Rigidbody>();
			Vector3 velocityBeforeTeleport = Vector3.zero;
			Vector3 angularVelocityBeforeTeleport = Vector3.zero;
			if (rb) {
				velocityBeforeTeleport = rb.linearVelocity;
				angularVelocityBeforeTeleport = rb.angularVelocity;
			}
			
			// Move real object to clone's position using PortalTraveller.Teleport
			// This ensures proper scaling and portal tracking
			var traveller = GetComponent<PortalTraveller>();
			if (traveller) {
				traveller.Teleport(_currentPortal.transform, _currentDestination.transform, clonePos, cloneRot, scaleRatio);
				
				// Transform velocity through portal (same as player does)
				if (rb && velocityBeforeTeleport.sqrMagnitude > 0.001f) {
					// Scale velocity by portal size difference
					Vector3 transformedVelocity = velocityBeforeTeleport * scaleRatio;
					
					// Rotate velocity through portal (same transformation as player)
					Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
					Quaternion relativeRotation = _currentDestination.transform.rotation * flipLocal * Quaternion.Inverse(_currentPortal.transform.rotation);
					transformedVelocity = relativeRotation * transformedVelocity;
					
					// Apply transformed velocity
					rb.linearVelocity = transformedVelocity;
					
					// Transform angular velocity too
					if (angularVelocityBeforeTeleport.sqrMagnitude > 0.001f) {
						Vector3 transformedAngularVelocity = relativeRotation * angularVelocityBeforeTeleport;
						rb.angularVelocity = transformedAngularVelocity;
					}
				}
				
				// Notify PortalTravellerHandler that we just teleported
				var handler = _currentDestination.GetComponent<PortalTravellerHandler>();
				if (handler) {
					handler.OnTravellerEnterPortal(traveller, justTeleported: true);
				}
			} else {
				// Fallback if no PortalTraveller
				transform.SetPositionAndRotation(clonePos, cloneRot);
				transform.localScale = cloneScale;
			}
			
			// Destroy clone - swap complete!
			DestroyClone();
		}

		void RestorePortalCollisions() {
			// Restore collisions with all portal walls when object is dropped
			var traveller = GetComponent<PortalTraveller>();
			if (!traveller) return;
			
			// Find all portal handlers and restore collisions
			var allHandlers = FindObjectsOfType<PortalTravellerHandler>();
			foreach (var handler in allHandlers) {
				handler.SetCollisionIgnore(traveller, false);
			}
			
			Debug.Log($"[PortalCloneSystem] Restored collisions for {gameObject.name}");
		}

		void DestroyClone() {
			if (_clone) {
				Destroy(_clone);
				_clone = null;
			}
			
			_currentPortal = null;
			_currentDestination = null;
		}

		void CopyVisualComponents(GameObject source, GameObject target) {
			// Copy MeshRenderer
			var sourceRenderer = source.GetComponent<MeshRenderer>();
			if (sourceRenderer) {
				var targetRenderer = target.AddComponent<MeshRenderer>();
				targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
				targetRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
				targetRenderer.receiveShadows = sourceRenderer.receiveShadows;
			}
			
			// Copy MeshFilter
			var sourceFilter = source.GetComponent<MeshFilter>();
			if (sourceFilter && sourceFilter.sharedMesh) {
				var targetFilter = target.AddComponent<MeshFilter>();
				targetFilter.sharedMesh = sourceFilter.sharedMesh;
			}
			
			// Copy all child visual components
			foreach (Transform child in source.transform) {
				GameObject childClone = new GameObject(child.name);
				childClone.transform.SetParent(target.transform);
				childClone.transform.localPosition = child.localPosition;
				childClone.transform.localRotation = child.localRotation;
				childClone.transform.localScale = child.localScale;
				CopyVisualComponents(child.gameObject, childClone);
			}
		}

		void DisableCloneComponents(GameObject clone) {
			// Remove all non-visual components
			var components = clone.GetComponents<Component>();
			foreach (var comp in components) {
				if (comp is Transform || comp is MeshRenderer || comp is MeshFilter) continue;
				Destroy(comp);
			}
			
			// Disable colliders
			var colliders = clone.GetComponentsInChildren<Collider>();
			foreach (var col in colliders) {
				col.enabled = false;
			}
			
			// Remove rigidbodies
			var rigidbodies = clone.GetComponentsInChildren<Rigidbody>();
			foreach (var rb in rigidbodies) {
				Destroy(rb);
			}
			
			// Recursively disable for children
			foreach (Transform child in clone.transform) {
				DisableCloneComponents(child.gameObject);
			}
		}

		void OnDestroy() {
			DestroyClone();
		}
	}
}

