// PortalCloneSystem.cs - Handles visual clones for held objects passing through portals
using UnityEngine;
using System.Collections.Generic;

namespace Portal {
	public class PortalCloneSystem : MonoBehaviour {
		[Header("Clone Settings")]
		[SerializeField] private LayerMask portalLayer = -1;
		
		private GameObject _clone;
		private PortalRenderer _currentPortal;
		private PortalRenderer _currentDestination;
		private bool _isHeld;
		private float _lastSideCheck = 0f; // Track which side we were on last frame
		
		public bool HasClone => _clone != null;
		public GameObject Clone => _clone;

		void Update() {
			// Always check for portal contact and update clone, even when not held
			// This ensures clone persists when object is crossing portal
			CheckForPortalContact();
			
			// Update clone position/rotation if it exists
			if (_clone && _currentPortal && _currentDestination) {
				UpdateCloneTransform();
				
				// Check if object has fully crossed portal (for dropped objects)
				if (!_isHeld) {
					CheckForPortalCrossing();
				}
			}
		}
		
		void CheckForPortalCrossing() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			// Verify we're still near the portal we're tracking
			// If object teleported via PortalTravellerHandler, we might be at a different portal now
			Vector3 offsetFromPortal = transform.position - _currentPortal.transform.position;
			float distanceToPortal = offsetFromPortal.magnitude;
			
			// If object is too far from the portal, it probably teleported normally - destroy clone
			if (distanceToPortal > 2f) {
				Debug.Log($"[PortalCloneSystem] Object too far from tracked portal ({distanceToPortal}m), destroying clone for {gameObject.name}");
				DestroyClone();
				return;
			}
			
			// Check which side of the portal we're on now
			float dot = Vector3.Dot(offsetFromPortal, _currentPortal.transform.forward);
			
			// If we were on the entering side (negative) and now we're on the exiting side (positive),
			// we've crossed the portal - swap with clone
			if (_lastSideCheck < 0 && dot > 0.01f) {
				Debug.Log($"[PortalCloneSystem] Object crossed portal (dot changed from {_lastSideCheck} to {dot}), swapping with clone for {gameObject.name}");
				SwapWithClone();
				return;
			}
			
			// Update last side check
			_lastSideCheck = dot;
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
					// Check which side of the portal we're on
					Vector3 offsetFromPortal = transform.position - _currentPortal.transform.position;
					float dot = Vector3.Dot(offsetFromPortal, _currentPortal.transform.forward);
					
					// Use a small threshold to handle cases where object is exactly at portal plane
					const float threshold = 0.01f;
					
					// If dot > threshold, we're clearly on the "exiting" side (where clone is) - swap
					if (dot > threshold) {
						// We're on the destination side, swap with clone
						Debug.Log($"[PortalCloneSystem] Dropped on destination side (dot={dot}), swapping with clone for {gameObject.name}");
						SwapWithClone();
					} 
					// If dot < -threshold, we're clearly on the "entering" side
					else if (dot < -threshold) {
						// We're still on the source side, destroy clone
						Debug.Log($"[PortalCloneSystem] Dropped on source side (dot={dot}), destroying clone for {gameObject.name}");
						DestroyClone();
						
						// Properly initialize portal tracking so it doesn't immediately teleport
						InitializePortalTracking();
					}
					// If -threshold <= dot <= threshold, object is crossing/intersecting portal
					// Keep the clone alive - it will be handled by portal teleportation system
					else {
						Debug.Log($"[PortalCloneSystem] Dropped while crossing portal (dot={dot}), keeping clone alive for {gameObject.name}");
						// Don't destroy clone - let portal teleportation system handle it
						// Clone will update in Update() and swap will happen when object fully crosses
						
						// Properly initialize portal tracking
						InitializePortalTracking();
					}
				} else {
					// No clone exists, but check if we're touching a portal
					// If we are, a clone might be created in the next Update()
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

		/// <summary>
		/// Called when object teleports via PortalTravellerHandler (normal teleportation, not clone swap)
		/// This prevents clone system from interfering and causing object to disappear
		/// </summary>
		public void OnObjectTeleported(Transform fromPortal, Transform toPortal) {
			// If we have a clone, destroy it since normal teleportation already happened
			// The clone system should not interfere with normal teleportation
			if (_clone != null) {
				Debug.Log($"[PortalCloneSystem] Object teleported via PortalTravellerHandler, destroying clone for {gameObject.name}");
				DestroyClone();
			}
			
			// Reset side check to prevent immediate re-teleportation
			// Find which portal we're now at
			Collider[] nearby = Physics.OverlapSphere(transform.position, 0.5f);
			foreach (var col in nearby) {
				var portal = col.GetComponent<PortalRenderer>();
				if (portal != null && (portal.transform == toPortal || portal.pair?.transform == toPortal)) {
					// Initialize side check for the portal we're now at
					Vector3 offsetFromPortal = transform.position - portal.transform.position;
					_lastSideCheck = Vector3.Dot(offsetFromPortal, portal.transform.forward);
					break;
				}
			}
		}

		void CreateClone(PortalRenderer portal) {
			if (!portal.pair || _clone != null) return; // Only create if we don't have one
			
			_currentPortal = portal;
			_currentDestination = portal.pair;
			
			// Initialize side check
			Vector3 offsetFromPortal = transform.position - portal.transform.position;
			_lastSideCheck = Vector3.Dot(offsetFromPortal, portal.transform.forward);
			
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
			
			// Store portal references before destroying clone
			PortalRenderer oldDestination = _currentDestination;
			
			// Destroy clone - swap complete!
			DestroyClone();
			
			// After swap, check if we're still touching a portal and create new clone if needed
			// This handles cases where object is dropped right at portal boundary
			// We're now on what was the destination side
			if (oldDestination != null) {
				// Check if we're still touching a portal after swap
				Collider[] nearby = Physics.OverlapSphere(transform.position, 0.5f);
				PortalRenderer stillTouching = null;
				foreach (var col in nearby) {
					var portal = col.GetComponent<PortalRenderer>();
					if (portal != null && portal.pair != null) {
						stillTouching = portal;
						break;
					}
				}
				
				// If still touching the portal we just teleported to, create new clone
				// (we're now on the other side, so clone should appear on the original side)
				if (stillTouching != null && stillTouching == oldDestination) {
					CreateClone(oldDestination);
				}
			}
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
			_lastSideCheck = 0f;
		}

		void CopyVisualComponents(GameObject source, GameObject target) {
			// Get ALL renderers including children
			var allRenderers = source.GetComponentsInChildren<Renderer>();
			
			// Build hierarchy map to match source transforms to target transforms
			Dictionary<Transform, Transform> transformMap = new Dictionary<Transform, Transform>();
			transformMap[source.transform] = target.transform;
			
			// Recursively build the full hierarchy
			void BuildHierarchy(Transform sourceParent, Transform targetParent) {
				foreach (Transform child in sourceParent) {
					GameObject childClone = new GameObject(child.name);
					childClone.transform.SetParent(targetParent);
					childClone.transform.localPosition = child.localPosition;
					childClone.transform.localRotation = child.localRotation;
					childClone.transform.localScale = child.localScale;
					transformMap[child] = childClone.transform;
					BuildHierarchy(child, childClone.transform);
				}
			}
			BuildHierarchy(source.transform, target.transform);
			
			// Copy all renderers to their corresponding positions in the clone hierarchy
			foreach (var renderer in allRenderers) {
				if (!renderer) continue;
				Transform targetTransform = transformMap[renderer.transform];
				if (!targetTransform) continue;
				
				if (renderer is MeshRenderer meshRenderer) {
					// Copy MeshFilter if it exists
					var filter = renderer.GetComponent<MeshFilter>();
					if (filter && filter.sharedMesh) {
						var targetFilter = targetTransform.GetComponent<MeshFilter>();
						if (!targetFilter) targetFilter = targetTransform.gameObject.AddComponent<MeshFilter>();
						targetFilter.sharedMesh = filter.sharedMesh;
					}
					
					// Copy MeshRenderer
					var targetRenderer = targetTransform.GetComponent<MeshRenderer>();
					if (!targetRenderer) targetRenderer = targetTransform.gameObject.AddComponent<MeshRenderer>();
					targetRenderer.sharedMaterials = meshRenderer.sharedMaterials;
					targetRenderer.shadowCastingMode = meshRenderer.shadowCastingMode;
					targetRenderer.receiveShadows = meshRenderer.receiveShadows;
					targetRenderer.enabled = meshRenderer.enabled;
				}
				else if (renderer is SkinnedMeshRenderer skinnedRenderer) {
					// Copy SkinnedMeshRenderer
					var targetSkinned = targetTransform.GetComponent<SkinnedMeshRenderer>();
					if (!targetSkinned) targetSkinned = targetTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
					targetSkinned.sharedMesh = skinnedRenderer.sharedMesh;
					targetSkinned.sharedMaterials = skinnedRenderer.sharedMaterials;
					targetSkinned.bones = skinnedRenderer.bones;
					targetSkinned.rootBone = skinnedRenderer.rootBone;
					targetSkinned.shadowCastingMode = skinnedRenderer.shadowCastingMode;
					targetSkinned.receiveShadows = skinnedRenderer.receiveShadows;
					targetSkinned.enabled = skinnedRenderer.enabled;
				}
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

