using UnityEngine;

namespace Portal {
	public static class PortalVisibility
	{
		private static readonly Plane[] Planes = new Plane[6];
		private static int _cachedFrame = -1;

		public static bool IsVisible(Camera camera, Renderer renderer)
		{
			if (!camera || !renderer) return false;

			if (_cachedFrame == Time.frameCount) return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
			
			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			_cachedFrame = Time.frameCount;

			return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
		}
	}
}
