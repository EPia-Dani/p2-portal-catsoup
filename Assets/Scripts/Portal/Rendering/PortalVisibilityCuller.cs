using UnityEngine;

namespace Portal {
	[DisallowMultipleComponent]
	public class PortalVisibilityCuller : MonoBehaviour {
		[SerializeField] private bool enableCulling = false;
		[SerializeField, Range(0f, 1f)] private float minScreenCoverageFraction = 0.01f;
		[SerializeField] private bool enableAngleCulling = false;
		[SerializeField, Range(0f, 180f)] private float maxViewAngle = 90f;
		[SerializeField] private bool enableDistanceCulling = false;
		[SerializeField] private float maxRenderDistance = 100f;

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

			// Frustum culling
			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) {
				return true;
			}

			// Distance culling
			if (enableDistanceCulling && maxRenderDistance > 0f) {
				float distance = Vector3.Distance(mainCamera.transform.position, surfaceRenderer.bounds.center);
				if (distance > maxRenderDistance) {
					return true;
				}
			}

			// Angle culling - check if camera is facing the portal
			if (enableCulling && enableAngleCulling) {
				Vector3 toPortal = surfaceRenderer.bounds.center - mainCamera.transform.position;
				float distanceToPortal = toPortal.magnitude;
				if (distanceToPortal > 0.01f) {
					Vector3 dirToPortal = toPortal / distanceToPortal;
					float facingDot = Vector3.Dot(mainCamera.transform.forward, dirToPortal);
					float minFacingDot = Mathf.Cos(Mathf.Deg2Rad * maxViewAngle);
					if (facingDot < minFacingDot) {
						return true; // Camera is not facing the portal enough
					}
				}
			}

			// Screen coverage culling
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
