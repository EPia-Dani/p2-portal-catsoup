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

			// Calculate teleport position/rotation using utility
			PortalTransformUtility.TransformThroughPortal(portalRenderer, destination, 
				traveller.transform.position, traveller.transform.rotation, scaleRatio, 
				out Vector3 newPos, out Quaternion newRot);

			traveller.Teleport(transform, destination.transform, newPos, newRot, scaleRatio);
			destHandler?.OnTravellerEnterPortal(traveller, justTeleported: true);
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			// Never track held objects
			if (PlayerPickup.IsObjectHeld(traveller.gameObject)) {
				SetCollisionIgnore(traveller, true);
				return;
			}

			if (_trackedTravellers.Contains(traveller)) return;

			Vector3 offset = traveller.transform.position - transform.position;

			// Prevent immediate re-teleport
			if (justTeleported) {
				float dot = Vector3.Dot(offset, transform.forward);
				if (dot >= 0) {
					traveller.previousOffsetFromPortal = offset - transform.forward * (dot + 0.1f);
				} else {
					traveller.previousOffsetFromPortal = offset;
				}
			} else {
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
			var traveller = other.GetComponent<PortalTraveller>();
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
