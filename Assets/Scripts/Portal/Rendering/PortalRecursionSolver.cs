// PortalRecursionSolver.cs
// Utility class for portal recursion matrix calculations

using System;
using UnityEngine;

namespace Portal.Rendering {
	public static class PortalRecursionSolver {
		private static readonly Matrix4x4 MirrorMatrix = Matrix4x4.Scale(new Vector3(-1, 1, -1));

		/// <summary>
		/// Builds the transformation matrix that maps from source portal space to destination portal space
		/// </summary>
		public static Matrix4x4 BuildPortalTransform(Transform source, Transform destination) {
			return destination.localToWorldMatrix * MirrorMatrix * source.worldToLocalMatrix;
		}

		/// <summary>
		/// Builds recursion matrices for portal rendering
		/// </summary>
		public static void BuildRecursionMatrices(
			Matrix4x4 portalTransform,
			Matrix4x4 cameraWorldMatrix,
			Matrix4x4[] output,
			int recursionLimit) {
			
			Matrix4x4 current = cameraWorldMatrix;
			int count = Mathf.Min(output.Length, recursionLimit);

			for (int i = 0; i < count; i++) {
				current = portalTransform * current;
				output[i] = current;
			}
		}

		/// <summary>
		/// Calculates the optimal recursion level based on portal orientation
		/// </summary>
		public static int CalculateMaxRecursionLevel(Transform source, Transform destination, int maxLimit) {
			if (!destination) return maxLimit - 1;

			// Reduce recursion for vertical portals
			bool sourceVertical = Mathf.Abs(Vector3.Dot(source.forward, Vector3.up)) > 0.9f;
			bool destVertical = Mathf.Abs(Vector3.Dot(destination.forward, Vector3.up)) > 0.9f;
			if (sourceVertical || destVertical) {
				return Mathf.Min(2, maxLimit - 1);
			}

			// Adjust based on portal angle
			float dot = Mathf.Clamp(Vector3.Dot(source.forward, destination.forward), -1f, 1f);
			float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

			if (angle < 45f) return 0; // Nearly parallel - no recursion needed
			if (angle < 135f) return Mathf.Min(1, maxLimit - 1); // Moderate angle
			return maxLimit - 1; // Opposite portals - full recursion
		}
	}
}

