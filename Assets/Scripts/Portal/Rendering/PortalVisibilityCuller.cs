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

			// Cache bounds once
			Bounds bounds = surfaceRenderer.bounds;
			Vector3 boundsCenter = bounds.center;

			// Frustum culling
			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) {
				return true;
			}

			// Distance culling - use sqrMagnitude to avoid sqrt
			if (enableDistanceCulling && maxRenderDistance > 0f) {
				Vector3 camPos = mainCamera.transform.position;
				float distanceSq = (boundsCenter - camPos).sqrMagnitude;
				float maxDistSq = maxRenderDistance * maxRenderDistance;
				if (distanceSq > maxDistSq) {
					return true;
				}
			}

			// Angle culling - check if camera is facing the portal
			if (enableAngleCulling) {
				Transform camTransform = mainCamera.transform;
				Vector3 toPortal = boundsCenter - camTransform.position;
				float distanceToPortalSq = toPortal.sqrMagnitude;
				if (distanceToPortalSq > 0.0001f) {
					Vector3 camForward = camTransform.forward;
					float forwardDot = Vector3.Dot(camForward, toPortal);
					// Check angle: forwardDot^2 / distanceSq < cos^2(angle) => forwardDot^2 < distanceSq * cos^2(angle)
					float cosAngle = Mathf.Cos(Mathf.Deg2Rad * maxViewAngle);
					float minFacingDotSq = distanceToPortalSq * cosAngle * cosAngle;
					if (forwardDot < 0f || forwardDot * forwardDot < minFacingDotSq) {
						return true;
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

			Bounds bounds = surfaceRenderer.bounds;
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			
			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;
			int visiblePoints = 0;

			// Process 8 corners without array allocation
			ProcessCorner(mainCamera, center, extents, -1, -1, -1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents, -1, -1,  1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents, -1,  1, -1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents, -1,  1,  1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents,  1, -1, -1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents,  1, -1,  1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents,  1,  1, -1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);
			ProcessCorner(mainCamera, center, extents,  1,  1,  1, ref minX, ref minY, ref maxX, ref maxY, ref visiblePoints);

			if (visiblePoints == 0) return 0f;

			int screenWidth = mainCamera.pixelWidth;
			int screenHeight = mainCamera.pixelHeight;
			float area = (maxX - minX) * (maxY - minY);
			return area / (screenWidth * screenHeight);
		}

		void ProcessCorner(Camera cam, Vector3 center, Vector3 extents, float sx, float sy, float sz, ref float minX, ref float minY, ref float maxX, ref float maxY, ref int visiblePoints) {
			Vector3 corner = center + new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
			Vector3 screenPos = cam.WorldToScreenPoint(corner);
			if (screenPos.z > 0) {
				visiblePoints++;
				if (screenPos.x < minX) minX = screenPos.x;
				if (screenPos.y < minY) minY = screenPos.y;
				if (screenPos.x > maxX) maxX = screenPos.x;
				if (screenPos.y > maxY) maxY = screenPos.y;
			}
		}
	}
}
