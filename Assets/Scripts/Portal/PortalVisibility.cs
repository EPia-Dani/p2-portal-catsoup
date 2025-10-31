using UnityEngine;

namespace Portal {
	public static class PortalVisibility
	{
		private static readonly Plane[] Planes = new Plane[6];
		private static int _cachedFrame = -1;

	private static Bounds _cachedBounds;

	public static bool IsVisible(Camera camera, Renderer renderer)
	{
		if (camera == null || renderer == null) return false;

		// Cache bounds to avoid property access overhead
		_cachedBounds = renderer.bounds;

		if (_cachedFrame == Time.frameCount) return GeometryUtility.TestPlanesAABB(Planes, _cachedBounds);
		
		GeometryUtility.CalculateFrustumPlanes(camera, Planes);
		_cachedFrame = Time.frameCount;

		return GeometryUtility.TestPlanesAABB(Planes, _cachedBounds);
	}
	}
}
