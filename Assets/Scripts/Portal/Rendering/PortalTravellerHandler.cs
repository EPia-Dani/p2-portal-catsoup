// PortalTravellerHandler.cs - Simple teleportation handler
using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	public class PortalTravellerHandler : MonoBehaviour {
		[SerializeField] private PortalRenderer portalRenderer;
		public Collider wallCollider;
		
		private readonly List<PortalTraveller> _trackedTravellers = new List<PortalTraveller>();
		private PortalViewChain _viewChain;

		void Awake() {
			if (!portalRenderer) portalRenderer = GetComponent<PortalRenderer>();
			_viewChain = GetComponent<PortalViewChain>();
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

			// Calculate teleport position/rotation
			Vector3 newPos = destination.transform.position;
			Quaternion newRot = traveller.transform.rotation;
			if (_viewChain) {
				_viewChain.ComputeTeleportPose(portalRenderer, destination, traveller.transform.position, traveller.transform.rotation, scaleRatio, out newPos, out newRot);
			}

			traveller.Teleport(transform, destination.transform, newPos, newRot, scaleRatio);
			destHandler?.OnTravellerEnterPortal(traveller, justTeleported: true);
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
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
			if (wallCollider) {
				var collider = traveller.GetComponent<Collider>();
				if (collider) {
					Physics.IgnoreCollision(collider, wallCollider, true);
				}
			}
		}

		void OnTriggerEnter(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller) {
				OnTravellerEnterPortal(traveller);
			}
		}

		void OnTriggerExit(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller && _trackedTravellers.Remove(traveller)) {
				if (wallCollider) {
					var collider = traveller.GetComponent<Collider>();
					if (collider) {
						Physics.IgnoreCollision(collider, wallCollider, false);
					}
				}
			}
		}
	}
}
