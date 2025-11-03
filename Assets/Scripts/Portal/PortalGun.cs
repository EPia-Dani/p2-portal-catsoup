// PortalGun.cs  (kept, only relies on PortalManager.PlacePortal and arrays)
using Input;
using UnityEngine;

namespace Portal {
	public class PortalGun : MonoBehaviour {
		[SerializeField] LayerMask shootMask = ~0;
		[SerializeField] float shootDistance = 1000f;
		[SerializeField] Camera shootCamera;
		[SerializeField] PortalManager portalManager;
		[SerializeField] Vector2 portalHalfSize = new(0.45f, 0.45f);
		[SerializeField] float wallOffset = 0.02f;
		[SerializeField] float clampSkin = 0.01f;

		private PlayerInput input;
		static readonly Vector3 ViewCenter = new(0.5f, 0.5f, 0f);

		void Start() {
			if (!shootCamera) shootCamera = Camera.main;
			if (!portalManager) portalManager = GetComponent<PortalManager>();
			input = InputManager.PlayerInput;
		}

		void Update() {
			if (input.Player.ShootBlue.WasPerformedThisFrame())  Fire(0);
			if (input.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
		}

		void Fire(int index) {
			if (!shootCamera || !portalManager || index < 0 || index > 1) return;
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(ViewCenter), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			if (hit.collider == null || !hit.collider.enabled) return;

			Vector3 normal = hit.normal;
			Vector3 up = GetUpVector(normal);
			Vector3 right = Vector3.Cross(normal, up);

			Vector3 surfaceCenter = GetSurfaceCenter(hit, hit.collider.bounds, normal);
			Vector2 clampRange = GetClampRange(hit.collider.bounds, right, up);
			if (clampRange.x <= 0f || clampRange.y <= 0f) return;

			Vector2 localPos = GetLocalPosition(hit.point, surfaceCenter, right, up, clampRange);
			if (ShouldPreventOverlap(index, hit.collider, normal, surfaceCenter, right, up, ref localPos, clampRange)) return;

			Vector3 finalPosition = surfaceCenter + right * localPos.x + up * localPos.y;
			portalManager.PlacePortal(index, finalPosition, normal, right, up, hit.collider, wallOffset);
		}

		Vector3 GetUpVector(Vector3 normal) {
			if (shootCamera == null) return Vector3.Cross(normal, Vector3.right).normalized;
			Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
			if (up.sqrMagnitude < 1e-4f) {
				up = Vector3.ProjectOnPlane(shootCamera.transform.forward, normal);
				if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(normal, Vector3.right);
			}
			return up.normalized;
		}

		static Vector3 GetSurfaceCenter(RaycastHit hit, Bounds bounds, Vector3 normal) {
			return bounds.center + normal * Vector3.Dot(hit.point - bounds.center, normal);
		}

		Vector2 GetClampRange(Bounds b, Vector3 r, Vector3 u) {
			Vector3 e = b.extents;
			float cr = e.x * Mathf.Abs(r.x) + e.y * Mathf.Abs(r.y) + e.z * Mathf.Abs(r.z) - portalHalfSize.x - clampSkin;
			float cu = e.x * Mathf.Abs(u.x) + e.y * Mathf.Abs(u.y) + e.z * Mathf.Abs(u.z) - portalHalfSize.y - clampSkin;
			return new Vector2(cr, cu);
		}

		static Vector2 GetLocalPosition(Vector3 hitPoint, Vector3 center, Vector3 r, Vector3 u, Vector2 clamp) {
			Vector3 d = hitPoint - center;
			return new Vector2(Mathf.Clamp(Vector3.Dot(d, r), -clamp.x, clamp.x),
			                   Mathf.Clamp(Vector3.Dot(d, u), -clamp.y, clamp.y));
		}

		bool ShouldPreventOverlap(int index, Collider surface, Vector3 normal, Vector3 center, Vector3 right, Vector3 up, ref Vector2 localPos, Vector2 clampRange) {
			if (portalManager == null) return false;
			int otherIndex = 1 - index;
			if (otherIndex < 0 || otherIndex >= portalManager.portalSurfaces.Length) return false;

			var otherSurface = portalManager.portalSurfaces[otherIndex];
			var otherNormal  = portalManager.portalNormals[otherIndex];
			if (otherSurface == null || otherSurface != surface || Vector3.Dot(normal, otherNormal) < 0.99f) return false;

			Vector3 otherOffset = portalManager.portalCenters[otherIndex] - center;
			Vector2 otherLocalPos = new(Vector3.Dot(otherOffset, right), Vector3.Dot(otherOffset, up));

			Vector2 dist = (localPos - otherLocalPos) / portalHalfSize;
			float m = dist.magnitude;

			if (m < 2.1f) {
				float k = m > 1e-3f ? 2.1f / m : 1f;
				localPos = otherLocalPos + dist * portalHalfSize * k;
				localPos.x = Mathf.Clamp(localPos.x, -clampRange.x, clampRange.x);
				localPos.y = Mathf.Clamp(localPos.y, -clampRange.y, clampRange.y);
				dist = (localPos - otherLocalPos) / portalHalfSize;
				if (dist.magnitude < 2.05f) return true;
			}
			return false;
		}
	}
}
