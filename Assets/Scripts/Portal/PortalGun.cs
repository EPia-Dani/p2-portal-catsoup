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
			if (input.Player.ShootBlue.WasPerformedThisFrame()) Fire(0);
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

		Vector3 GetSurfaceCenter(RaycastHit hit, Bounds bounds, Vector3 normal) {
			return bounds.center + normal * Vector3.Dot(hit.point - bounds.center, normal);
		}

		Vector2 GetClampRange(Bounds bounds, Vector3 right, Vector3 up) {
			Vector3 extents = bounds.extents;
			float clampRight = extents.x * Mathf.Abs(right.x) + extents.y * Mathf.Abs(right.y) + extents.z * Mathf.Abs(right.z) - portalHalfSize.x - clampSkin;
			float clampUp = extents.x * Mathf.Abs(up.x) + extents.y * Mathf.Abs(up.y) + extents.z * Mathf.Abs(up.z) - portalHalfSize.y - clampSkin;
			return new Vector2(clampRight, clampUp);
		}

		Vector2 GetLocalPosition(Vector3 hitPoint, Vector3 center, Vector3 right, Vector3 up, Vector2 clampRange) {
			Vector3 offset = hitPoint - center;
			return new Vector2(
				Mathf.Clamp(Vector3.Dot(offset, right), -clampRange.x, clampRange.x),
				Mathf.Clamp(Vector3.Dot(offset, up), -clampRange.y, clampRange.y)
			);
		}

		bool ShouldPreventOverlap(int index, Collider surface, Vector3 normal, Vector3 center, Vector3 right, Vector3 up, ref Vector2 localPos, Vector2 clampRange) {
			if (portalManager == null) return false;
			
			int otherIndex = 1 - index;
			if (otherIndex < 0 || otherIndex >= portalManager.portalSurfaces.Length) return false;
			
			var otherSurface = portalManager.portalSurfaces[otherIndex];
			var otherNormal = portalManager.portalNormals[otherIndex];
			
			if (otherSurface == null || otherSurface != surface || Vector3.Dot(normal, otherNormal) < 0.99f) return false;

			Vector3 otherOffset = portalManager.portalCenters[otherIndex] - center;
			Vector2 otherLocalPos = new Vector2(Vector3.Dot(otherOffset, right), Vector3.Dot(otherOffset, up));
			
			Vector2 distance = (localPos - otherLocalPos) / portalHalfSize;
			float distanceMagnitude = distance.magnitude;

			if (distanceMagnitude < 2.1f) {
				float pushScale = distanceMagnitude > 1e-3f ? 2.1f / distanceMagnitude : 1f;
				localPos = otherLocalPos + distance * portalHalfSize * pushScale;
				localPos.x = Mathf.Clamp(localPos.x, -clampRange.x, clampRange.x);
				localPos.y = Mathf.Clamp(localPos.y, -clampRange.y, clampRange.y);
				
				distance = (localPos - otherLocalPos) / portalHalfSize;
				if (distance.magnitude < 2.05f) return true;
			}
			
			return false;
		}
	}
}
