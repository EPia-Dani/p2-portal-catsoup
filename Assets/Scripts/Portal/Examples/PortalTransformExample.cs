// PortalTransformExample.cs - Example showing how easy it is to transform objects through portals
// This demonstrates the reusable PortalTransformUtility

using UnityEngine;

namespace Portal {
	/// <summary>
	/// Example: Transform any object through portals using the utility.
	/// Just call PortalTransformUtility.TransformThroughPortal() - that's it!
	/// </summary>
	public class PortalTransformExample : MonoBehaviour {
		[SerializeField] PortalRenderer sourcePortal;
		[SerializeField] PortalRenderer destinationPortal;

		// Example: Transform this object through portals
		public void TeleportObject() {
			if (!sourcePortal || !destinationPortal) return;

			float scaleRatio = destinationPortal.PortalScale / sourcePortal.PortalScale;
			
			// Super simple - just one line!
			PortalTransformUtility.TransformThroughPortal(sourcePortal, destinationPortal, transform, scaleRatio);
		}

		// Example: Get new position/rotation without applying (for preview, etc.)
		public void PreviewTeleport(out Vector3 newPos, out Quaternion newRot) {
			if (!sourcePortal || !destinationPortal) {
				newPos = transform.position;
				newRot = transform.rotation;
				return;
			}

			float scaleRatio = destinationPortal.PortalScale / sourcePortal.PortalScale;
			
			// Get the transformed values
			PortalTransformUtility.TransformThroughPortal(sourcePortal, destinationPortal,
				transform.position, transform.rotation, scaleRatio,
				out newPos, out newRot);
		}
	}
}

