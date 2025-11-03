using UnityEngine;

namespace Portal {
	/// <summary>
	/// Generic object traveller for non-player entities passing through portals.
	/// Handles simple position and rotation transformation.
	/// </summary>
	public class ObjectTraveller : PortalTraveller {
		/// <summary>
		/// Called each frame while inside a portal trigger.
		/// Detects when the object crosses the portal and initiates teleportation.
		/// </summary>
		public override void OnPortalStay(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			if (HasCrossedPortalPlane(sourcePortal.transform.position, sourcePortal.transform.forward)) {
				TeleportThroughPortal(sourcePortal, destPortal);
				destPortal.NotifyTravellerCrossed(this);
			}

			_lastPosition = transform.position;
		}

		/// <summary>
		/// Teleports the object through the portal, adjusting position and rotation.
		/// </summary>
		private void TeleportThroughPortal(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			PortalRenderer sourceRenderer = sourcePortal.GetComponent<PortalRenderer>();
			PortalRenderer destRenderer = destPortal.GetComponent<PortalRenderer>();

			if (sourceRenderer == null || destRenderer == null) return;

			TransformThroughPortal(sourceRenderer, destRenderer, out Vector3 newPosition, out Quaternion newRotation);
			newPosition += destPortal.transform.forward * 0.1f;

			// Move object
			transform.SetPositionAndRotation(newPosition, newRotation);
		}
	}
}

