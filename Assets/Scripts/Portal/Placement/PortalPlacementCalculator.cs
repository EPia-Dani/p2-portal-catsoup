using UnityEngine;

namespace Portal {
	public class PortalPlacementCalculator {
		static readonly Vector3 ViewCenter = new Vector3(0.5f, 0.5f, 0f);

		readonly Camera _camera;
		readonly LayerMask _shootMask;
		readonly float _shootDistance;
		readonly float _clampSkin;
		readonly PortalSizeController _sizeController;

		public PortalPlacementCalculator(Camera camera, LayerMask shootMask, float shootDistance, float clampSkin, PortalSizeController sizeController) {
			_camera = camera;
			_shootMask = shootMask;
			_shootDistance = shootDistance;
			_clampSkin = clampSkin;
			_sizeController = sizeController;
		}

		public bool TryCalculate(out PortalPlacement placement) {
			placement = default;
			if (_camera == null || _sizeController == null) return false;

			Ray ray = _camera.ViewportPointToRay(ViewCenter);
			if (!Physics.Raycast(ray, out RaycastHit hit, _shootDistance, _shootMask, QueryTriggerInteraction.Ignore)) {
				return false;
			}

			Collider surface = hit.collider;
			if (surface == null || !surface.enabled) {
				return false;
			}

			Vector3 normal = hit.normal;
			Vector3 up = PortalSurfaceMath.ResolveUpVector(normal, _camera.transform);
			Vector3 right = Vector3.Cross(normal, up).normalized;
			Bounds bounds = surface.bounds;
			Vector3 surfaceCenter = PortalSurfaceMath.ProjectPointToSurfaceCenter(bounds, hit.point, normal);
			Vector2 clampRange = PortalSurfaceMath.GetClampRange(bounds, right, up, _sizeController.CurrentHalfSize, _sizeController.InitialHalfSize, _clampSkin);
			
			Vector3 offset = hit.point - surfaceCenter;
			
			// For small portals, don't clamp to bounds - allow placement anywhere
			Vector2 localPos;
			float currentScale = _sizeController.CurrentScale;
			if (currentScale < 0.5f) {
				// Small portal - no clamping, skip bounds validation
				localPos = new Vector2(
					Vector3.Dot(offset, right),
					Vector3.Dot(offset, up)
				);
			} else {
				// Normal portal - validate bounds and clamp
				if (clampRange.x <= 0f || clampRange.y <= 0f) {
					return false;
				}
				localPos = new Vector2(
					Mathf.Clamp(Vector3.Dot(offset, right), -clampRange.x, clampRange.x),
					Mathf.Clamp(Vector3.Dot(offset, up), -clampRange.y, clampRange.y)
				);
			}

			placement = new PortalPlacement {
				Surface = surface,
				Normal = normal,
				Up = up,
				Right = right,
				SurfaceCenter = surfaceCenter,
				ClampRange = clampRange,
				LocalPosition = localPos
			};

			return true;
		}
	}
}
