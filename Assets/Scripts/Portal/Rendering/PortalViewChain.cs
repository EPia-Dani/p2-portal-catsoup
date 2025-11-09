using UnityEngine;

namespace Portal {
	[DisallowMultipleComponent]
	public class PortalViewChain : MonoBehaviour {
		// Wrapper for backward compatibility - now uses PortalTransformUtility
		public int BuildViewChain(Camera mainCamera, PortalRenderer source, PortalRenderer destination, int recursionLimit, Matrix4x4[] buffer) {
			return PortalTransformUtility.BuildViewMatrices(mainCamera, source, destination, recursionLimit, buffer);
		}

		public void ComputeTeleportPose(PortalRenderer fromPortal, PortalRenderer toPortal, Vector3 travellerPosition, Quaternion travellerRotation, float scaleRatio, out Vector3 newPosition, out Quaternion newRotation) {
			PortalTransformUtility.TransformThroughPortal(fromPortal, toPortal, travellerPosition, travellerRotation, scaleRatio, out newPosition, out newRotation);
		}
	}
}

