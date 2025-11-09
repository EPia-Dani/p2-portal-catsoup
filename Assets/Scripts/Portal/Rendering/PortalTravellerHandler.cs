// PortalTravellerHandler.cs - Simple teleportation handler
using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	public class PortalTravellerHandler : MonoBehaviour {
		[SerializeField] private PortalRenderer portalRenderer;
		public Collider wallCollider;
		
		private readonly List<PortalTraveller> _trackedTravellers = new List<PortalTraveller>();

		void Awake() {
			if (!portalRenderer) portalRenderer = GetComponent<PortalRenderer>();
		}

		void LateUpdate() {
			if (!portalRenderer?.pair) return;

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

		void TeleportTraveller(PortalTraveller traveller, PortalRenderer destination, float scaleRatio) {
			if (!traveller || !destination) return;

			Collider travellerCollider = traveller.GetComponent<Collider>();

			// Disable collision with source wall
			if (wallCollider && travellerCollider) {
				Physics.IgnoreCollision(travellerCollider, wallCollider, false);
			}

			// Enable collision ignore with destination wall
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

			// Apply velocity transformation (same as player)
			// Determine if the source portal is 'non-vertical' (e.g. on the floor/ceiling)
			float portalUpDot = Mathf.Abs(Vector3.Dot(transform.forward.normalized, Vector3.up));
			const float nonVerticalThreshold = 0.5f;
			bool isHorizontalPortal = portalUpDot > nonVerticalThreshold;
			
			if (rb) {
				if (velocityBeforeTeleport.sqrMagnitude > 0.001f) {
					// Scale velocity by portal size difference
					Vector3 transformedVelocity = velocityBeforeTeleport * scaleRatio;
					
					// Rotate velocity through portal (same transformation as player)
					Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
					Quaternion relativeRotation = destination.transform.rotation * flipLocal * Quaternion.Inverse(transform.rotation);
					transformedVelocity = relativeRotation * transformedVelocity;
					
					// Apply transformed velocity
					rb.linearVelocity = transformedVelocity;
					
					// Transform angular velocity too
					if (angularVelocityBeforeTeleport.sqrMagnitude > 0.001f) {
						Vector3 transformedAngularVelocity = relativeRotation * angularVelocityBeforeTeleport;
						rb.angularVelocity = transformedAngularVelocity;
					}
				} else if (isHorizontalPortal) {
					// For horizontal portals, even if object has no velocity, give it a small push
					// in the direction of the portal's forward to ensure it appears on the other side
					Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
					Quaternion relativeRotation = destination.transform.rotation * flipLocal * Quaternion.Inverse(transform.rotation);
					Vector3 exitDirection = relativeRotation * transform.forward;
					
					// Give a small forward push (horizontal only)
					Vector3 horizontalPush = new Vector3(exitDirection.x, 0, exitDirection.z).normalized * 2f;
					rb.linearVelocity = horizontalPush;
				}
			}

			destHandler?.OnTravellerEnterPortal(traveller, justTeleported: true);
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			// Never track held objects
			if (PlayerPickup.IsObjectHeld(traveller.gameObject)) {
				SetCollisionIgnore(traveller, true);
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

			// Ignore collision with wall
			SetCollisionIgnore(traveller, true);
		}

		public void SetCollisionIgnore(PortalTraveller traveller, bool ignore) {
			if (wallCollider && traveller) {
				var collider = traveller.GetComponent<Collider>();
				if (collider) {
					Physics.IgnoreCollision(collider, wallCollider, ignore);
				}
			}
		}

		void OnTriggerEnter(Collider other) {
			// Check if object has PortalTraveller component, if not, try to add it for interactable objects
			var traveller = other.GetComponent<PortalTraveller>();
			if (!traveller && other.CompareTag("Interactable")) {
				// Check if object has InteractableObject (which should already have PortalTraveller)
				var interactable = other.GetComponent<InteractableObject>();
				if (interactable != null && interactable.PortalTraveller != null) {
					traveller = interactable.PortalTraveller;
				} else {
					// Fallback: automatically add PortalTraveller to interactable objects so they can teleport
					traveller = other.gameObject.AddComponent<PortalTraveller>();
					Debug.Log($"[PortalTravellerHandler] Auto-added PortalTraveller to {other.name}");
				}
			}
			
			if (traveller) {
				bool isHeld = PlayerPickup.IsObjectHeld(traveller.gameObject);
				Debug.Log($"[PortalTravellerHandler] OnTriggerEnter: {traveller.name} (held: {isHeld})");
				
				// For held objects, only set up collision ignore (no tracking/teleportation)
				if (isHeld) {
					Debug.Log($"[PortalTravellerHandler] Setting collision ignore for held object {traveller.name}");
					SetCollisionIgnore(traveller, true);
					// Also ignore collision with destination portal wall
					if (portalRenderer?.pair) {
						var destHandler = portalRenderer.pair.GetComponent<PortalTravellerHandler>();
						if (destHandler) {
							destHandler.SetCollisionIgnore(traveller, true);
							Debug.Log($"[PortalTravellerHandler] Also ignoring collision with destination portal for {traveller.name}");
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
				Debug.Log($"[PortalTravellerHandler] OnTriggerExit: {traveller.name} (held: {isHeld})");
				
				// For held objects, DO NOT restore collision - keep it ignored while held
				// Collision will be restored when object is dropped (handled by PortalCloneSystem)
				if (isHeld) {
					Debug.Log($"[PortalTravellerHandler] Ignoring OnTriggerExit for held object {traveller.name} - keeping collision ignored");
					return;
				}
				
				// Remove from tracking if it was tracked
				bool wasTracked = _trackedTravellers.Remove(traveller);

				// Only restore collision for non-held objects
				if (wasTracked) {
					Debug.Log($"[PortalTravellerHandler] Restoring collision for {traveller.name}");
					SetCollisionIgnore(traveller, false);
				}
			}
		}
	}
}
