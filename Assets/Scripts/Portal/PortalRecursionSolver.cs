using UnityEngine;

namespace Portal {
	public static class PortalRecursionSolver
	{
		public static Matrix4x4 BuildStepMatrix(Transform source, Transform destination, Matrix4x4 mirrorMatrix)
		{
			return destination.localToWorldMatrix * mirrorMatrix * source.worldToLocalMatrix;
		}

		public static void Fill(Matrix4x4[] buffer, Matrix4x4 stepMatrix, Matrix4x4 startMatrix)
		{
			if (buffer == null) return;

			var current = startMatrix;
			for (int i = 0; i < buffer.Length; i++)
			{
				current = stepMatrix * current;
				buffer[i] = current;
			}
		}
	}
}
