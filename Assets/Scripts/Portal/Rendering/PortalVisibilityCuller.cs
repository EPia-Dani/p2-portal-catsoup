using UnityEngine;

namespace Portal {
	[DisallowMultipleComponent]
	public class PortalVisibilityCuller : MonoBehaviour {
		[SerializeField] private bool enableCulling = false;
		[SerializeField, Range(0f, 1f)] private float minScreenCoverageFraction = 0.01f;

		private readonly Plane[] _frustumPlanes = new Plane[6];

		public bool EnableCulling {
			get => enableCulling;
			set => enableCulling = value;
		}

		public float MinScreenCoverage {
			get => minScreenCoverageFraction;
			set => minScreenCoverageFraction = Mathf.Clamp01(value);
		}

		public bool ShouldCull(Camera mainCamera, MeshRenderer surfaceRenderer) {
			if (!enableCulling) return false;
			if (!mainCamera || !surfaceRenderer) return true;

			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) {
				return true;
			}

			if (minScreenCoverageFraction > 0f) {
				float coverage = GetScreenSpaceCoverage(mainCamera, surfaceRenderer);
				return coverage < minScreenCoverageFraction;
			}

			return false;
		}

		public float GetScreenSpaceCoverage(Camera mainCamera, MeshRenderer surfaceRenderer) {
			if (!mainCamera || !surfaceRenderer) return 0f;

			var bounds = surfaceRenderer.bounds;
			Vector3 min = new Vector3(float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue);
			int visiblePoints = 0;

			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			Vector3[] corners = {
				center + new Vector3(-extents.x, -extents.y, -extents.z),
				center + new Vector3(-extents.x, -extents.y,  extents.z),
				center + new Vector3(-extents.x,  extents.y, -extents.z),
				center + new Vector3(-extents.x,  extents.y,  extents.z),
				center + new Vector3( extents.x, -extents.y, -extents.z),
				center + new Vector3( extents.x, -extents.y,  extents.z),
				center + new Vector3( extents.x,  extents.y, -extents.z),
				center + new Vector3( extents.x,  extents.y,  extents.z)
			};

			foreach (var corner in corners) {
				Vector3 screenPos = mainCamera.WorldToScreenPoint(corner);
				if (screenPos.z <= 0) continue;

				visiblePoints++;
				if (screenPos.x < min.x) min.x = screenPos.x;
				if (screenPos.y < min.y) min.y = screenPos.y;
				if (screenPos.x > max.x) max.x = screenPos.x;
				if (screenPos.y > max.y) max.y = screenPos.y;
			}

			if (visiblePoints == 0) return 0f;

			float area = (max.x - min.x) * (max.y - min.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}
	}
}
