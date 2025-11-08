// PortalTravellerHandler.cs
// Handles traveler teleportation logic separately from rendering

using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	public class PortalTravellerHandler : MonoBehaviour {
		[SerializeField] private PortalRenderer portalRenderer;
		public Collider wallCollider;
		
		private readonly List<PortalTraveller> _trackedTravellers = new List<PortalTraveller>();
		private PortalViewChain _viewChain;

		public List<PortalTraveller> TrackedTravellers => _trackedTravellers;

		void Awake() {
			if (!portalRenderer) portalRenderer = GetComponent<PortalRenderer>();
			_viewChain = GetComponent<PortalViewChain>();
		}

		void LateUpdate() {
			if (!portalRenderer || !portalRenderer.pair) return;
			HandleTravellers();
		}

		void HandleTravellers() {
			PortalRenderer pair = portalRenderer.pair;
			if (!pair) return;

			float scaleRatio = pair.PortalScale / portalRenderer.PortalScale;

			for (int i = 0; i < _trackedTravellers.Count; i++) {
				var traveller = _trackedTravellers[i];
				if (!traveller) {
					_trackedTravellers.RemoveAt(i--);
					continue;
				}

				Vector3 offset = traveller.transform.position - transform.position;
				float sidePrev = Mathf.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
				float sideNow = Mathf.Sign(Vector3.Dot(offset, transform.forward));

				if (sideNow > 0 && sidePrev < 0) {
					TeleportTraveller(traveller, pair, scaleRatio);
					_trackedTravellers.RemoveAt(i--);
					continue;
				}

				traveller.previousOffsetFromPortal = offset;
			}
		}

		void TeleportTraveller(PortalTraveller traveller, PortalRenderer destination, float scaleRatio) {
			if (!traveller || !destination) return;

			Collider travellerCollider = traveller.GetComponent<Collider>();

			if (wallCollider && travellerCollider) {
				Physics.IgnoreCollision(travellerCollider, wallCollider, false);
			}

			var destinationHandler = destination.GetComponent<PortalTravellerHandler>();
			if (destinationHandler && destinationHandler.wallCollider && travellerCollider) {
				Physics.IgnoreCollision(travellerCollider, destinationHandler.wallCollider, true);
			}

			Vector3 newPos;
			Quaternion newRot;
			if (_viewChain) {
				_viewChain.ComputeTeleportPose(portalRenderer, destination, traveller.transform.position, traveller.transform.rotation, scaleRatio, out newPos, out newRot);
			} else {
				newPos = destination.transform.position;
				newRot = traveller.transform.rotation;
			}

			traveller.Teleport(transform, destination.transform, newPos, newRot, scaleRatio);
			destinationHandler?.OnTravellerEnterPortal(traveller, justTeleported: true);
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			if (_trackedTravellers.Contains(traveller)) return;

			Vector3 offsetFromPortal = traveller.transform.position - transform.position;

			if (justTeleported) {
				float currentDot = Vector3.Dot(offsetFromPortal, transform.forward);
				if (currentDot >= 0) {
					traveller.previousOffsetFromPortal = offsetFromPortal - transform.forward * (currentDot + 0.1f);
				} else {
					traveller.previousOffsetFromPortal = offsetFromPortal;
				}
			} else {
				traveller.previousOffsetFromPortal = offsetFromPortal;
			}

			_trackedTravellers.Add(traveller);

			if (wallCollider) {
				Collider travellerCollider = traveller.GetComponent<Collider>();
				if (travellerCollider) {
					Physics.IgnoreCollision(travellerCollider, wallCollider, true);
				}
			}
		}

		public void SetWallCollider(Collider col) {
			wallCollider = col;
		}

		void OnTriggerEnter(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller) {
				OnTravellerEnterPortal(traveller);
			}
		}

		void OnTriggerExit(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller && _trackedTravellers.Contains(traveller)) {
				_trackedTravellers.Remove(traveller);

				if (wallCollider) {
					Collider travellerCollider = traveller.GetComponent<Collider>();
					if (travellerCollider) {
						Physics.IgnoreCollision(travellerCollider, wallCollider, false);
					}
				}
			}
		}
	}
}
