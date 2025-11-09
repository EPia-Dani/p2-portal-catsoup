// PortalTransformUtility.cs - Reusable portal transformation logic
// 
// Usage Examples:
// 
// 1. Transform a transform through portals:
//    PortalTransformUtility.TransformThroughPortal(sourcePortal, destPortal, myTransform, scaleRatio);
//
// 2. Get new position/rotation without applying:
//    PortalTransformUtility.TransformThroughPortal(sourcePortal, destPortal, 
//        currentPos, currentRot, scaleRatio, out Vector3 newPos, out Quaternion newRot);
//
// 3. Build view matrices for portal rendering:
//    int count = PortalTransformUtility.BuildViewMatrices(camera, sourcePortal, destPortal, limit, matrixBuffer);

using UnityEngine;

namespace Portal {
	public static class PortalTransformUtility {
		private static readonly Matrix4x4 MIRROR = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));

		/// <summary>
		/// Transforms a position and rotation through a portal pair.
		/// Handles scale differences between portals automatically.
		/// </summary>
		/// <param name="fromPortal">Source portal</param>
		/// <param name="toPortal">Destination portal</param>
		/// <param name="position">Current position</param>
		/// <param name="rotation">Current rotation</param>
		/// <param name="scaleRatio">Scale ratio (toPortal.PortalScale / fromPortal.PortalScale)</param>
		/// <param name="newPosition">Output new position</param>
		/// <param name="newRotation">Output new rotation</param>
		public static void TransformThroughPortal(PortalRenderer fromPortal, PortalRenderer toPortal, 
			Vector3 position, Quaternion rotation, float scaleRatio, 
			out Vector3 newPosition, out Quaternion newRotation) {
			
			newPosition = position;
			newRotation = rotation;

			if (!fromPortal || !toPortal) return;

			// Calculate portal transformation matrix
			Matrix4x4 portalTransform = toPortal.transform.localToWorldMatrix * MIRROR * fromPortal.transform.worldToLocalMatrix;

			// Transform position
			Vector3 offset = position - fromPortal.transform.position;
			Vector3 scaledOffset = offset * scaleRatio;
			Vector3 transformedOffset = portalTransform.MultiplyVector(scaledOffset);
			newPosition = toPortal.transform.position + transformedOffset;

			// Transform rotation
			Matrix4x4 objectMatrix = portalTransform * Matrix4x4.TRS(position, rotation, Vector3.one);
			newRotation = objectMatrix.rotation;
		}

		/// <summary>
		/// Transforms a transform through a portal pair.
		/// </summary>
		public static void TransformThroughPortal(PortalRenderer fromPortal, PortalRenderer toPortal, 
			Transform transform, float scaleRatio) {
			
			if (!fromPortal || !toPortal || !transform) return;

			TransformThroughPortal(fromPortal, toPortal, transform.position, transform.rotation, scaleRatio, 
				out Vector3 newPos, out Quaternion newRot);
			
			transform.SetPositionAndRotation(newPos, newRot);
		}

		/// <summary>
		/// Builds view matrices for portal recursion rendering.
		/// </summary>
		public static int BuildViewMatrices(Camera camera, PortalRenderer source, PortalRenderer destination, 
			int recursionLimit, Matrix4x4[] buffer) {
			
			if (!camera || !source || !destination || buffer == null || buffer.Length == 0) return 0;

			int count = Mathf.Clamp(recursionLimit, 1, buffer.Length);
			Matrix4x4 step = destination.transform.localToWorldMatrix * MIRROR * source.transform.worldToLocalMatrix;
			Matrix4x4 current = camera.transform.localToWorldMatrix;

			PortalRenderer currentPortal = source;
			PortalRenderer nextPortal = destination;

			for (int i = 0; i < count; i++) {
				current = step * current;
				buffer[i] = ApplyScaleAdjustment(current, currentPortal, nextPortal);

				PortalRenderer temp = currentPortal;
				currentPortal = nextPortal;
				nextPortal = temp;
			}

			return count;
		}

		/// <summary>
		/// Applies scale adjustment to a matrix when portals have different scales.
		/// </summary>
		private static Matrix4x4 ApplyScaleAdjustment(Matrix4x4 matrix, PortalRenderer currentPortal, PortalRenderer nextPortal) {
			if (!currentPortal || !nextPortal) return matrix;

			float scaleRatio = nextPortal.PortalScale / currentPortal.PortalScale;
			if (Mathf.Abs(scaleRatio - 1f) <= 0.001f) {
				return matrix;
			}

			Vector3 position = matrix.GetColumn(3);
			Quaternion rotation = matrix.rotation;
			Vector3 offset = position - nextPortal.transform.position;
			Vector3 localOffset = nextPortal.transform.InverseTransformDirection(offset);
			Vector3 scaledOffset = localOffset * scaleRatio;
			Vector3 scaledWorldOffset = nextPortal.transform.TransformDirection(scaledOffset);

			return Matrix4x4.TRS(nextPortal.transform.position + scaledWorldOffset, rotation, Vector3.one);
		}
	}
}

