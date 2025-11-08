// PortalVisibility.cs
// Utility class for portal visibility and culling calculations

using UnityEngine;

namespace Portal.Rendering {
	public static class PortalVisibility {
		// Cache arrays to avoid allocations
		private static readonly Plane[] FrustumPlanes = new Plane[6];
		private static readonly Vector3[] CoveragePoints = new Vector3[8];
		private static Vector2 _screenMin;
		private static Vector2 _screenMax;

		/// <summary>
		/// Checks if portal is visible to the main camera using frustum culling
		/// </summary>
		public static bool IsVisibleToCamera(Camera camera, MeshRenderer surfaceRenderer) {
			if (!camera || !surfaceRenderer) return false;

			GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(FrustumPlanes, surfaceRenderer.bounds)) {
				return false;
			}

			// Check if camera is looking at portal
			Vector3 toCamera = (camera.transform.position - surfaceRenderer.transform.position).normalized;
			return Vector3.Dot(surfaceRenderer.transform.forward, toCamera) < 0.1f;
		}

		/// <summary>
		/// Calculates screen space coverage of the portal (0-1)
		/// </summary>
		public static float GetScreenSpaceCoverage(Camera camera, MeshRenderer surfaceRenderer) {
			if (!camera || !surfaceRenderer) return 0f;

			Bounds bounds = surfaceRenderer.bounds;
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;

			// Calculate 8 corner points of bounding box
			CoveragePoints[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
			CoveragePoints[1] = center + new Vector3(-extents.x, -extents.y,  extents.z);
			CoveragePoints[2] = center + new Vector3(-extents.x,  extents.y, -extents.z);
			CoveragePoints[3] = center + new Vector3(-extents.x,  extents.y,  extents.z);
			CoveragePoints[4] = center + new Vector3( extents.x, -extents.y, -extents.z);
			CoveragePoints[5] = center + new Vector3( extents.x, -extents.y,  extents.z);
			CoveragePoints[6] = center + new Vector3( extents.x,  extents.y, -extents.z);
			CoveragePoints[7] = center + new Vector3( extents.x,  extents.y,  extents.z);

			// Find screen-space bounds
			_screenMin.x = float.MaxValue;
			_screenMin.y = float.MaxValue;
			_screenMax.x = float.MinValue;
			_screenMax.y = float.MinValue;

			int visiblePoints = 0;
			for (int i = 0; i < 8; i++) {
				Vector3 screenPoint = camera.WorldToScreenPoint(CoveragePoints[i]);
				if (screenPoint.z <= 0) continue; // Behind camera

				visiblePoints++;
				if (screenPoint.x < _screenMin.x) _screenMin.x = screenPoint.x;
				if (screenPoint.y < _screenMin.y) _screenMin.y = screenPoint.y;
				if (screenPoint.x > _screenMax.x) _screenMax.x = screenPoint.x;
				if (screenPoint.y > _screenMax.y) _screenMax.y = screenPoint.y;
			}

			if (visiblePoints == 0) return 0f;

			float area = (_screenMax.x - _screenMin.x) * (_screenMax.y - _screenMin.y);
			return area / (camera.pixelWidth * camera.pixelHeight);
		}
	}
}

