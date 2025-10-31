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

		/// <summary>
		/// Initializes camera and portal manager references on startup.
		/// Falls back to Camera.main and GetComponent if not set in inspector.
		/// </summary>
		void Start() {
			if (!shootCamera) shootCamera = Camera.main;
			if (!portalManager) portalManager = GetComponent<PortalManager>();
			input = InputManager.PlayerInput;
		}

		/// <summary>
		/// Checks for portal shooting input each frame.
		/// Fires blue portal (index 0) or orange portal (index 1) based on input.
		/// </summary>
		void Update() {
			if (input.Player.ShootBlue.WasPerformedThisFrame()) Fire(0);
			if (input.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
		}

		/// <summary>
		/// Main portal firing logic. Casts a ray from camera center, calculates portal position,
		/// prevents overlap with other portal, and places the portal at the final position.
		/// </summary>
		/// <param name="index">Portal index: 0 for blue, 1 for orange</param>
		void Fire(int index) {
			if (!shootCamera || !portalManager) return;
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(ViewCenter), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			if (!hit.collider || !hit.collider.enabled) return;

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

		/// <summary>
		/// Calculates a valid "up" direction vector for portal orientation on a surface.
		/// Uses world up first, falls back to camera forward, then uses world right as last resort.
		/// Ensures portals are always oriented correctly regardless of surface angle.
		/// </summary>
		/// <param name="normal">The surface normal vector</param>
		/// <returns>Normalized up vector perpendicular to the surface normal</returns>
		Vector3 GetUpVector(Vector3 normal) {
			Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
			if (up.sqrMagnitude < 1e-4f) {
				up = Vector3.ProjectOnPlane(shootCamera.transform.forward, normal);
				if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(normal, Vector3.right);
			}
			return up.normalized;
		}

		/// <summary>
		/// Projects the hit point onto the collider's surface center plane.
		/// Finds where the portal center should be positioned on the surface.
		/// </summary>
		/// <param name="hit">The raycast hit result</param>
		/// <param name="bounds">The collider's bounding box</param>
		/// <param name="normal">The surface normal vector</param>
		/// <returns>The projected center point on the surface plane</returns>
		Vector3 GetSurfaceCenter(RaycastHit hit, Bounds bounds, Vector3 normal) {
			return bounds.center + normal * Vector3.Dot(hit.point - bounds.center, normal);
		}

		/// <summary>
		/// Calculates the maximum range the portal can be positioned within the collider bounds.
		/// Accounts for portal size and a small skin value to prevent clipping at edges.
		/// </summary>
		/// <param name="bounds">The collider's bounding box</param>
		/// <param name="right">The right direction vector of the portal coordinate system</param>
		/// <param name="up">The up direction vector of the portal coordinate system</param>
		/// <returns>Vector2 with x = max right offset, y = max up offset</returns>
		Vector2 GetClampRange(Bounds bounds, Vector3 right, Vector3 up) {
			Vector3 extents = bounds.extents;
			float clampRight = extents.x * Mathf.Abs(right.x) + extents.y * Mathf.Abs(right.y) + extents.z * Mathf.Abs(right.z) - portalHalfSize.x - clampSkin;
			float clampUp = extents.x * Mathf.Abs(up.x) + extents.y * Mathf.Abs(up.y) + extents.z * Mathf.Abs(up.z) - portalHalfSize.y - clampSkin;
			return new Vector2(clampRight, clampUp);
		}

		/// <summary>
		/// Converts the world-space hit point to a 2D local position on the surface.
		/// Clamps the position within the valid bounds to prevent portal from going outside collider edges.
		/// </summary>
		/// <param name="hitPoint">The world-space point where the raycast hit</param>
		/// <param name="center">The surface center point</param>
		/// <param name="right">The right direction vector of the portal coordinate system</param>
		/// <param name="up">The up direction vector of the portal coordinate system</param>
		/// <param name="clampRange">Maximum allowed offsets (x = right, y = up)</param>
		/// <returns>Local 2D position on the surface plane, clamped to valid range</returns>
		Vector2 GetLocalPosition(Vector3 hitPoint, Vector3 center, Vector3 right, Vector3 up, Vector2 clampRange) {
			Vector3 offset = hitPoint - center;
			return new Vector2(
				Mathf.Clamp(Vector3.Dot(offset, right), -clampRange.x, clampRange.x),
				Mathf.Clamp(Vector3.Dot(offset, up), -clampRange.y, clampRange.y)
			);
		}

		/// <summary>
		/// Prevents portals from overlapping when placed on the same surface.
		/// If portals are too close (within 2.1 portal sizes), pushes the new portal away.
		/// Returns true if portal placement should be cancelled (still too close after push).
		/// </summary>
		/// <param name="index">Index of the portal being placed (0 or 1)</param>
		/// <param name="surface">The collider surface being hit</param>
		/// <param name="normal">The surface normal vector</param>
		/// <param name="center">The surface center point</param>
		/// <param name="right">The right direction vector of the portal coordinate system</param>
		/// <param name="up">The up direction vector of the portal coordinate system</param>
		/// <param name="localPos">The local 2D position (modified if push is needed)</param>
		/// <param name="clampRange">Maximum allowed offsets for clamping</param>
		/// <returns>True if portal placement should be prevented, false otherwise</returns>
		bool ShouldPreventOverlap(int index, Collider surface, Vector3 normal, Vector3 center, Vector3 right, Vector3 up, ref Vector2 localPos, Vector2 clampRange) {
			int otherIndex = 1 - index;
			var otherSurface = portalManager.portalSurfaces[otherIndex];
			var otherNormal = portalManager.portalNormals[otherIndex];
			
			if (otherSurface != surface || Vector3.Dot(normal, otherNormal) < 0.99f) return false;

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
