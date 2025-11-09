using UnityEngine;

namespace Portal {
	[DisallowMultipleComponent]
	public class PortalViewChain : MonoBehaviour {
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));

		public int BuildViewChain(Camera mainCamera, PortalRenderer source, PortalRenderer destination, int recursionLimit, Matrix4x4[] buffer) {
			if (!mainCamera || !source || !destination || buffer == null || buffer.Length == 0) return 0;

			int count = recursionLimit > buffer.Length ? buffer.Length : (recursionLimit < 1 ? 1 : recursionLimit);

			// Cache transforms to avoid repeated property access
			Transform sourceTransform = source.transform;
			Transform destTransform = destination.transform;
			Transform mainCamTransform = mainCamera.transform;

			Matrix4x4 step = destTransform.localToWorldMatrix * _mirror * sourceTransform.worldToLocalMatrix;
			Matrix4x4 current = mainCamTransform.localToWorldMatrix;

			PortalRenderer currentPortal = source;
			PortalRenderer nextPortal = destination;

			for (int i = 0; i < count; i++) {
				current = step * current;
				buffer[i] = ApplyScaleAdjustment(current, currentPortal, nextPortal);

				PortalRenderer swap = currentPortal;
				currentPortal = nextPortal;
				nextPortal = swap;
			}

			return count;
		}

		public void ComputeTeleportPose(PortalRenderer fromPortal, PortalRenderer toPortal, Vector3 travellerPosition, Quaternion travellerRotation, float scaleRatio, out Vector3 newPosition, out Quaternion newRotation) {
			newPosition = travellerPosition;
			newRotation = travellerRotation;

			if (!fromPortal || !toPortal) return;

			Matrix4x4 portalTransform = toPortal.transform.localToWorldMatrix * _mirror * fromPortal.transform.worldToLocalMatrix;

			Vector3 offset = travellerPosition - fromPortal.transform.position;
			Vector3 scaledOffset = offset * scaleRatio;
			Vector3 transformedOffset = portalTransform.MultiplyVector(scaledOffset);
			newPosition = toPortal.transform.position + transformedOffset;

			Matrix4x4 travellerMatrix = portalTransform * Matrix4x4.TRS(travellerPosition, travellerRotation, Vector3.one);
			newRotation = travellerMatrix.rotation;
		}

		Matrix4x4 ApplyScaleAdjustment(Matrix4x4 matrix, PortalRenderer currentPortal, PortalRenderer nextPortal) {
			if (!currentPortal || !nextPortal) return matrix;

			float scaleRatio = nextPortal.PortalScale / currentPortal.PortalScale;
			if (Mathf.Abs(scaleRatio - 1f) <= 0.001f) return matrix;

			Transform nextTransform = nextPortal.transform;
			Vector3 nextPos = nextTransform.position;
			Vector3 position = matrix.GetColumn(3);
			Quaternion rotation = matrix.rotation;
			
			Vector3 offset = position - nextPos;
			Vector3 localOffset = nextTransform.InverseTransformDirection(offset);
			Vector3 scaledOffset = localOffset * scaleRatio;
			Vector3 scaledWorldOffset = nextTransform.TransformDirection(scaledOffset);

			return Matrix4x4.TRS(nextPos + scaledWorldOffset, rotation, Vector3.one);
		}
	}
}

