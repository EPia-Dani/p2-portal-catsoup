using UnityEngine;

namespace Portal {
	public static class PortalVisibility {
		private static readonly Plane[] Planes = new Plane[6];
		private static Camera _tempCamera;

		public static bool IsVisible(Camera camera, Renderer renderer) {
			if (camera == null || renderer == null) return false;
			
			// Backface culling: check if renderer is behind camera
			Vector3 toRenderer = renderer.bounds.center - camera.transform.position;
			if (Vector3.Dot(camera.transform.forward, toRenderer) < 0) {
				
				return false;
			}
			
			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
		}

		
	}
}