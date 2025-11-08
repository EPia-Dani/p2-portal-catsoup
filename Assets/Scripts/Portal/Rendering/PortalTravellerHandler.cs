// PortalTravellerHandler.cs
// Handles traveler teleportation logic separately from rendering

using System.Collections.Generic;
using Portal.Rendering;
using UnityEngine;

namespace Portal {
	public class PortalTravellerHandler : MonoBehaviour {
		[SerializeField] private PortalRenderer portalRenderer;
		public Collider wallCollider;
		
		private readonly List<PortalTraveller> _trackedTravellers = new List<PortalTraveller>();
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1, 1, -1));

		public List<PortalTraveller> TrackedTravellers => _trackedTravellers;

		void Awake() {
			if (!portalRenderer) portalRenderer = GetComponent<PortalRenderer>();
		}

		void LateUpdate() {
			if (!portalRenderer || !portalRenderer.pair) return;
			HandleTravellers();
		}

		void HandleTravellers() {
			PortalRenderer pair = portalRenderer.pair;
			if (!pair) return;

			// Calculate scale ratio for size-based teleportation
			float scaleRatio = pair.PortalScale / portalRenderer.PortalScale;

			for (int i = 0; i < _trackedTravellers.Count; i++) {
				var t = _trackedTravellers[i];
				if (!t) {
					_trackedTravellers.RemoveAt(i--);
					continue;
				}

			// Scale offset from portal center based on portal size ratio
			Vector3 offset = t.transform.position - portalRenderer.transform.position;
			Vector3 scaledOffset = offset * scaleRatio;
			
			// Transform scaled offset through portal
			Matrix4x4 portalTransform = pair.transform.localToWorldMatrix * _mirror * portalRenderer.transform.worldToLocalMatrix;
			Vector3 transformedOffset = portalTransform.MultiplyVector(scaledOffset);
			Vector3 newPos = pair.transform.position + transformedOffset;
			
			// Get rotation from full transformation
			Matrix4x4 toDest = pair.transform.localToWorldMatrix * _mirror * 
			                   portalRenderer.transform.worldToLocalMatrix * t.transform.localToWorldMatrix;
			Quaternion newRot = toDest.rotation;

			float sidePrev = Mathf.Sign(Vector3.Dot(t.previousOffsetFromPortal, portalRenderer.transform.forward));
			float sideNow = Mathf.Sign(Vector3.Dot(offset, portalRenderer.transform.forward));

				if (sideNow > 0 && sidePrev < 0) {
					// Crossed from front to back - teleport
					Collider playerCollider = t.GetComponent<Collider>();
					
					// Handle collision with walls
					if (wallCollider && playerCollider) {
						Physics.IgnoreCollision(playerCollider, wallCollider, false);
					}
					
					var pairHandler = pair.GetComponent<PortalTravellerHandler>();
					if (pairHandler && pairHandler.wallCollider && playerCollider) {
						Physics.IgnoreCollision(playerCollider, pairHandler.wallCollider, true);
					}

					// Teleport with scale ratio
					t.Teleport(portalRenderer.transform, pair.transform, newPos, newRot, scaleRatio);
					
					_trackedTravellers.RemoveAt(i--);
					pairHandler?.OnTravellerEnterPortal(t, justTeleported: true);
					continue;
				}

				t.previousOffsetFromPortal = offset;
			}
		}

		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			if (_trackedTravellers.Contains(traveller)) return;

			Vector3 offsetFromPortal = traveller.transform.position - portalRenderer.transform.position;

			if (justTeleported) {
				float currentDot = Vector3.Dot(offsetFromPortal, portalRenderer.transform.forward);
				if (currentDot >= 0) {
					traveller.previousOffsetFromPortal = offsetFromPortal - portalRenderer.transform.forward * (currentDot + 0.1f);
				} else {
					traveller.previousOffsetFromPortal = offsetFromPortal;
				}
			} else {
				traveller.previousOffsetFromPortal = offsetFromPortal;
			}

			_trackedTravellers.Add(traveller);

			if (wallCollider) {
				Collider playerCollider = traveller.GetComponent<Collider>();
				if (playerCollider) {
					Physics.IgnoreCollision(playerCollider, wallCollider, true);
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
					Collider playerCollider = traveller.GetComponent<Collider>();
					if (playerCollider) {
						Physics.IgnoreCollision(playerCollider, wallCollider, false);
					}
				}
			}
		}
	}
}
