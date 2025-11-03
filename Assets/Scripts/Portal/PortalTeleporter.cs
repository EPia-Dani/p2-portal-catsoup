using UnityEngine;

namespace Portal {
	[RequireComponent(typeof(Collider))]
	public class PortalTeleporter : MonoBehaviour {
		[SerializeField] private PortalTeleporter linkedPortal;
		[SerializeField] private Collider wallCollider;

		private void Awake() {
			if (GetComponent<Collider>() is Collider c && !c.isTrigger) {
				c.isTrigger = true;
			}
		}

		private void OnTriggerEnter(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller == null || linkedPortal == null) return;

			// Ignore collisions with wall
			if (wallCollider != null) traveller.IgnoreCollisionWith(wallCollider, true);

			// Notify traveller of portal entry
			traveller.OnPortalEnter(this, linkedPortal);
		}

		private void OnTriggerStay(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller == null || linkedPortal == null) return;

			// Delegate portal traversal logic to the traveller
			traveller.OnPortalStay(this, linkedPortal);
		}

		private void OnTriggerExit(Collider other) {
			PortalTraveller traveller = other.GetComponent<PortalTraveller>() ?? other.GetComponentInParent<PortalTraveller>();
			if (traveller == null) return;

			// Re-enable collisions with wall
			if (wallCollider != null) traveller.IgnoreCollisionWith(wallCollider, false);

			// Notify traveller of portal exit
			traveller.OnPortalExit(this);
		}

		/// <summary>
		/// Called by traveller when it has crossed the portal.
		/// Resets the traveller's internal state in the linked portal.
		/// </summary>
		public void NotifyTravellerCrossed(PortalTraveller traveller) {
			if (linkedPortal != null) {
				traveller._lastPosition = Vector3.zero;
			}
		}

		public void SetWallCollider(Collider collider) {
			wallCollider = collider;
		}
	}
}
