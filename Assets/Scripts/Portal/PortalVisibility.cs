using UnityEngine;

namespace Portal {
	public static class PortalVisibility {
		private static readonly Plane[] Planes = new Plane[6];
		private static int _cachedFrame = -1;

		private static Bounds _cachedBounds;

		// Cache for temporary camera used in visibility checks
		private static Camera _tempVisibilityCamera;

		public static bool IsVisible(Camera camera, Renderer renderer) {
			if (camera == null || renderer == null) return false;

			// Cache bounds to avoid property access overhead
			_cachedBounds = renderer.bounds;

			if (_cachedFrame == Time.frameCount) return GeometryUtility.TestPlanesAABB(Planes, _cachedBounds);

			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			_cachedFrame = Time.frameCount;

			return GeometryUtility.TestPlanesAABB(Planes, _cachedBounds);
		}

		/// <summary>
		/// Checks if a renderer is visible from a specific camera position and direction.
		/// Used for checking visibility at recursion levels without needing an actual Camera object.
		/// </summary>
		/// <param name="cameraPosition">The camera position in world space</param>
		/// <param name="cameraForward">The camera forward direction (normalized)</param>
		/// <param name="cameraUp">The camera up direction (normalized)</param>
		/// <param name="referenceCamera">Reference camera for field of view and aspect ratio</param>
		/// <param name="renderer">The renderer to check visibility for</param>
		/// <returns>True if the renderer is visible from the specified camera perspective</returns>
		public static bool IsVisibleFromPosition(
			Vector3 cameraPosition,
			Vector3 cameraForward,
			Vector3 cameraUp,
			Camera referenceCamera,
			Renderer renderer) {
			if (referenceCamera == null || renderer == null) return false;

			Bounds rendererBounds = renderer.bounds;

			// Ensure we have a temporary camera for frustum calculations
			EnsureTempCamera();

			// Copy relevant camera properties to temp camera
			_tempVisibilityCamera.fieldOfView = referenceCamera.fieldOfView;
			_tempVisibilityCamera.aspect = referenceCamera.aspect;
			_tempVisibilityCamera.nearClipPlane = referenceCamera.nearClipPlane;
			_tempVisibilityCamera.farClipPlane = referenceCamera.farClipPlane;

			// Set temp camera position and rotation
			_tempVisibilityCamera.transform.SetPositionAndRotation(
				cameraPosition,
				Quaternion.LookRotation(cameraForward, cameraUp)
			);

			// Use Unity's built-in frustum calculation
			GeometryUtility.CalculateFrustumPlanes(_tempVisibilityCamera, Planes);

			return GeometryUtility.TestPlanesAABB(Planes, rendererBounds);
		}

		/// <summary>
		/// Ensures a temporary camera exists for visibility checks.
		/// </summary>
		private static void EnsureTempCamera() {
			if (_tempVisibilityCamera != null) return;

			GameObject tempCameraObj = new GameObject("TempVisibilityCamera_PortalSystem");
			tempCameraObj.hideFlags = HideFlags.HideAndDontSave;
			_tempVisibilityCamera = tempCameraObj.AddComponent<Camera>();
			_tempVisibilityCamera.enabled = false;
		}
	}
}