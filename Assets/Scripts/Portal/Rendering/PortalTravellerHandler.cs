// PortalTravellerHandler.cs - Simple teleportation handler
using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	public class PortalTravellerHandler : MonoBehaviour {
		[SerializeField] private PortalRenderer portalRenderer;
		public Collider wallCollider;
		
		private readonly List<PortalTraveller> _trackedTravellers = new List<PortalTraveller>();
		private PortalManager _portalManager;

		void Awake() {
			if (!portalRenderer) portalRenderer = GetComponent<PortalRenderer>();
			_portalManager = FindObjectOfType<PortalManager>();
		}

		void LateUpdate() {
			if (!portalRenderer?.pair) return;
			
			// Check if both portals are placed before allowing teleportation
			if (!AreBothPortalsPlaced()) return;

			PortalRenderer pair = portalRenderer.pair;
			float scaleRatio = pair.PortalScale / portalRenderer.PortalScale;

			for (int i = _trackedTravellers.Count - 1; i >= 0; i--) {
				var traveller = _trackedTravellers[i];
				if (!traveller) {
					_trackedTravellers.RemoveAt(i);
					continue;
				}

				// Completely ignore held objects - remove them from tracking
				if (PlayerPickup.IsObjectHeld(traveller.gameObject)) {
					Debug.LogWarning($"[PortalTravellerHandler] REMOVING held object {traveller.name} from tracking - it shouldn't be here!");
					_trackedTravellers.RemoveAt(i);
					continue;
				}

				Vector3 offset = traveller.transform.position - transform.position;
				float sidePrev = Mathf.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
				float sideNow = Mathf.Sign(Vector3.Dot(offset, transform.forward));

				if (sideNow > 0 && sidePrev < 0) {
					TeleportTraveller(traveller, pair, scaleRatio);
					_trackedTravellers.RemoveAt(i);
				} else {
					traveller.previousOffsetFromPortal = offset;
				}
			}
		}

		public void TeleportTraveller(PortalTraveller traveller, PortalRenderer destination, float scaleRatio) {
			if (!traveller || !destination) return;
			
			// Safety check: ensure both portals are placed before teleporting
			if (!AreBothPortalsPlaced()) return;

			Collider travellerCollider = traveller.GetComponent<Collider>();

			// Disable collision with destination wall BEFORE teleport (so object can pass through it)
			var destHandler = destination.GetComponent<PortalTravellerHandler>();
			if (destHandler?.wallCollider && travellerCollider) {
				Physics.IgnoreCollision(travellerCollider, destHandler.wallCollider, true);
			}

			// Capture velocity BEFORE teleporting (for Rigidbody objects)
			Rigidbody rb = traveller.GetComponent<Rigidbody>();
			Vector3 velocityBeforeTeleport = Vector3.zero;
			Vector3 angularVelocityBeforeTeleport = Vector3.zero;
			if (rb) {
				velocityBeforeTeleport = rb.linearVelocity;
				angularVelocityBeforeTeleport = rb.angularVelocity;
			}

			// Calculate teleport position/rotation using utility
			PortalTransformUtility.TransformThroughPortal(portalRenderer, destination, 
				traveller.transform.position, traveller.transform.rotation, scaleRatio, 
				out Vector3 newPos, out Quaternion newRot);

			traveller.Teleport(transform, destination.transform, newPos, newRot, scaleRatio);

			// CRITICAL: Re-enable collision with SOURCE portal wall IMMEDIATELY AFTER teleport
			// This allows the object to collide with the floor/wall it came from
			if (wallCollider && travellerCollider) {
				Physics.IgnoreCollision(travellerCollider, wallCollider, false);
			}

			// Force physics to update immediately so collision changes take effect
			Physics.SyncTransforms();

			_portalManager?.NotifyTravellerTeleported(portalRenderer, destination, newPos);

			// Apply velocity transformation ONLY if the traveler doesn't handle it itself
			// InteractableObject and FPSController override Teleport and handle velocity transformation internally
			bool handlesVelocityInternally = traveller is InteractableObject || traveller is FPSController;
			
			if (!handlesVelocityInternally && rb) {
				Vector3 transformedVelocity = Vector3.zero;
				
				if (velocityBeforeTeleport.sqrMagnitude > 0.001f) {
					// Scale velocity by portal size difference
					transformedVelocity = velocityBeforeTeleport * scaleRatio;
					
					// Rotate velocity through portal (same transformation as player)
					Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
					Quaternion relativeRotation = destination.transform.rotation * flipLocal * Quaternion.Inverse(transform.rotation);
					transformedVelocity = relativeRotation * transformedVelocity;
					
					// Transform angular velocity too
					if (angularVelocityBeforeTeleport.sqrMagnitude > 0.001f) {
						Vector3 transformedAngularVelocity = relativeRotation * angularVelocityBeforeTeleport;
						rb.angularVelocity = transformedAngularVelocity;
					}
				}
				
				// Apply minimum exit velocity using base class method (handles non-vertical to vertical transitions)
				transformedVelocity = traveller.ApplyMinimumExitVelocity(transform, destination.transform, transformedVelocity);
				
				// Apply transformed velocity
				rb.linearVelocity = transformedVelocity;
			}

			destHandler?.OnTravellerEnterPortal(traveller, justTeleported: true);
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			// Never track held objects
			if (PlayerPickup.IsObjectHeld(traveller.gameObject)) {
				// Only disable collision if both portals are placed
				if (AreBothPortalsPlaced()) {
					SetCollisionIgnore(traveller, true);
				}
				return;
			}

			// If already tracked, don't add again (prevents duplicate tracking)
			if (_trackedTravellers.Contains(traveller)) return;

			Vector3 offset = traveller.transform.position - transform.position;

			// Prevent immediate re-teleport - set previousOffsetFromPortal to be on the "entering" side
			if (justTeleported) {
				// Object just teleported here, ensure it's marked as being on the "entering" side
				// so it won't teleport again until it crosses to the "exiting" side
				float dot = Vector3.Dot(offset, transform.forward);
				if (dot >= 0) {
					// Object is on exiting side, push previousOffset to entering side
					traveller.previousOffsetFromPortal = offset - transform.forward * (dot + 0.1f);
				} else {
					// Object is already on entering side, set previousOffset to match
					traveller.previousOffsetFromPortal = offset;
				}
			} else {
				// Object naturally entered portal, initialize tracking
				traveller.previousOffsetFromPortal = offset;
			}

			_trackedTravellers.Add(traveller);

			// Ignore collision with wall only if both portals are placed
			if (AreBothPortalsPlaced()) {
				SetCollisionIgnore(traveller, true);
			}
		}

		public void SetCollisionIgnore(PortalTraveller traveller, bool ignore) {
			// If trying to ignore collision (disable it), only do so if both portals are placed
			// Always allow restoring collision (ignore = false) regardless of portal state
			if (ignore && !AreBothPortalsPlaced()) {
				return;
			}
			
			if (wallCollider && traveller) {
				var collider = traveller.GetComponent<Collider>();
				if (collider) {
					Physics.IgnoreCollision(collider, wallCollider, ignore);
				}
			}
		}

		void OnTriggerEnter(Collider other) {
			// Check if object has PortalTraveller component
			var traveller = other.GetComponent<PortalTraveller>();
			if (!traveller && other.CompareTag("Interactable")) {
				// InteractableObject now inherits from PortalTraveller, so GetComponent should find it
				// Fallback: automatically add PortalTraveller to interactable objects so they can teleport
				traveller = other.gameObject.AddComponent<PortalTraveller>();
			}
			
			if (traveller) {
				bool isHeld = PlayerPickup.IsObjectHeld(traveller.gameObject);
				
				// For held objects, only set up collision ignore (no tracking/teleportation)
				// Only disable collision if both portals are placed
				if (isHeld) {
					if (AreBothPortalsPlaced()) {
						SetCollisionIgnore(traveller, true);
						// Also ignore collision with destination portal wall
						if (portalRenderer?.pair) {
							var destHandler = portalRenderer.pair.GetComponent<PortalTravellerHandler>();
							if (destHandler) {
								destHandler.SetCollisionIgnore(traveller, true);
							}
						}
					}
				} else {
					OnTravellerEnterPortal(traveller);
				}
			}
		}

		void OnTriggerExit(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller) {
				bool isHeld = PlayerPickup.IsObjectHeld(traveller.gameObject);
				
				// For held objects, DO NOT restore collision - keep it ignored while held
				// Collision will be restored when object is dropped (handled by PortalCloneSystem)
				if (isHeld) {
					return;
				}
				
				// Remove from tracking if it was tracked
				bool wasTracked = _trackedTravellers.Remove(traveller);

				// Only restore collision for non-held objects
				if (wasTracked) {
					SetCollisionIgnore(traveller, false);
				}
			}
		}

		/// <summary>
		/// Checks if both portals (blue and orange) are placed.
		/// Returns false if PortalManager is not found or if either portal is not placed.
		/// </summary>
		bool AreBothPortalsPlaced() {
			if (_portalManager == null) {
				_portalManager = FindObjectOfType<PortalManager>();
				if (_portalManager == null) return false;
			}

			bool bluePlaced = _portalManager.TryGetState(PortalId.Blue, out _);
			bool orangePlaced = _portalManager.TryGetState(PortalId.Orange, out _);
			
			return bluePlaced && orangePlaced;
		}
	}
}
