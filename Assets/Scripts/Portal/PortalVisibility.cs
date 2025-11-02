using UnityEngine;

namespace Portal {
	public static class PortalVisibility {
		private static readonly Plane[] Planes = new Plane[6];
		private static Camera _tempCamera;

		public static bool IsVisible(Camera camera, Renderer renderer) {
			if (camera == null || renderer == null) return false;
			
			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
		}

		public static bool IsVisibleFromPosition(
			Vector3 cameraPosition,
			Vector3 cameraForward,
			Vector3 cameraUp,
			Camera referenceCamera,
			Renderer renderer) {
			if (referenceCamera == null || renderer == null) return false;

			if (_tempCamera == null) {
				GameObject tempObj = new GameObject("TempVisibilityCamera") {
					hideFlags = HideFlags.HideAndDontSave
				};
				_tempCamera = tempObj.AddComponent<Camera>();
				_tempCamera.enabled = false;
			}

			_tempCamera.fieldOfView = referenceCamera.fieldOfView;
			_tempCamera.aspect = referenceCamera.aspect;
			_tempCamera.nearClipPlane = referenceCamera.nearClipPlane;
			_tempCamera.farClipPlane = referenceCamera.farClipPlane;
			_tempCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(cameraForward, cameraUp));

			GeometryUtility.CalculateFrustumPlanes(_tempCamera, Planes);
			return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
		}
	}
}