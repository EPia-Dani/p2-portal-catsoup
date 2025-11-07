using UnityEngine;

namespace Portal {
	public static class PortalSurfaceMath {
		static readonly Vector3 FallbackAxis = Vector3.right;

		public static Vector3 ResolveUpVector(Vector3 normal, Transform cameraTransform) {
			Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
			if (up.sqrMagnitude < 1e-4f && cameraTransform != null) {
				up = Vector3.ProjectOnPlane(cameraTransform.forward, normal);
			}
			if (up.sqrMagnitude < 1e-4f) {
				up = Vector3.Cross(normal, FallbackAxis);
			}
			return up.normalized;
		}

		public static Vector3 ProjectPointToSurfaceCenter(Bounds bounds, Vector3 point, Vector3 normal) {
			return bounds.center + normal * Vector3.Dot(point - bounds.center, normal);
		}

		public static Vector2 GetClampRange(Bounds bounds, Vector3 right, Vector3 up, Vector2 halfSize, Vector2 minHalfSize, float clampSkin) {
			Vector2 effectiveHalfSize = GetEffectiveHalfSize(halfSize, minHalfSize);
			Vector3 extents = bounds.extents;
			float cr = extents.x * Mathf.Abs(right.x) + extents.y * Mathf.Abs(right.y) + extents.z * Mathf.Abs(right.z) - effectiveHalfSize.x - clampSkin;
			float cu = extents.x * Mathf.Abs(up.x) + extents.y * Mathf.Abs(up.y) + extents.z * Mathf.Abs(up.z) - effectiveHalfSize.y - clampSkin;
			return new Vector2(cr, cu);
		}

		public static Vector2 GetEffectiveHalfSize(Vector2 halfSize, Vector2 minHalfSize) {
			return new Vector2(Mathf.Max(halfSize.x, minHalfSize.x), Mathf.Max(halfSize.y, minHalfSize.y));
		}

		public static bool PortalFitsOnSurface(Collider surface, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Vector2 halfSize, Vector2 minHalfSize, float clampSkin) {
			if (surface == null) return false;

			Bounds bounds = surface.bounds;
			Vector3 surfaceCenter = ProjectPointToSurfaceCenter(bounds, position, normal);
			Vector3 offset = position - surfaceCenter;
			Vector2 localPos = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up));
			Vector2 clampRange = GetClampRange(bounds, right, up, halfSize, minHalfSize, clampSkin);
			if (clampRange.x <= 0f || clampRange.y <= 0f) return false;
			return Mathf.Abs(localPos.x) <= clampRange.x && Mathf.Abs(localPos.y) <= clampRange.y;
		}
	}
}
