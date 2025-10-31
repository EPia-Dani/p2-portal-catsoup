using UnityEngine;

namespace Portal {
	public class PortalGun : MonoBehaviour {
		[SerializeField] LayerMask shootMask = ~0;
		[SerializeField] float shootDistance = 1000f;
		[SerializeField] Camera shootCamera;
		[SerializeField] PortalManager portalManager;
		[SerializeField] Vector2 portalHalfSize = new Vector2(0.45f, 0.45f);
		[SerializeField] float wallOffset = 0.02f;
		[SerializeField] float clampSkin = 0.01f;

		const float Eps = 1e-6f;
		static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

		void Update() {
			var controls = InputManager.PlayerInput;
			if (controls.Player.ShootBlue.WasPerformedThisFrame()) Fire(0);
			if (controls.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
		}

		void Fire(int i) {
			if (!shootCamera || !portalManager) return;
			Ray ray = shootCamera.ViewportPointToRay(ViewportCenter);
			if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			var col = hit.collider; if (!col || !col.enabled) return;

			// surface basis
			Vector3 n = hit.normal;
			Vector3 r = Vector3.ProjectOnPlane(shootCamera.transform.right, n).normalized;
			if (r.sqrMagnitude < 0.001f) r = Vector3.Cross(n, Vector3.up).normalized;
			Vector3 u = Vector3.Cross(n, r);

			// plane center (project bounds center onto plane through hit)
			Bounds b = col.bounds; Vector3 center = b.center + Vector3.Project(hit.point - b.center, n);

			// clamp extents along portal axes
			Vector3 ext = b.extents;
			float maxR = Vector3.Dot(ext, new Vector3(Mathf.Abs(r.x), Mathf.Abs(r.y), Mathf.Abs(r.z)));
			float maxU = Vector3.Dot(ext, new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)));
			Vector2 clamp = new Vector2(maxR - portalHalfSize.x - clampSkin, maxU - portalHalfSize.y - clampSkin);
			if (clamp.x <= 0f || clamp.y <= 0f) return;

			// initial UV (local portal coords)
			Vector3 delta = hit.point - center;
			Vector2 uv = new Vector2(Mathf.Clamp(Vector3.Dot(delta, r), -clamp.x, clamp.x),
				Mathf.Clamp(Vector3.Dot(delta, u), -clamp.y, clamp.y));

			// check overlap with other portal and try to move uv away
			var otherOpt = portalManager.GetPortalState(1 - i);
			if (otherOpt.HasValue) {
				var op = otherOpt.Value;
				if (col == op.surface && Vector3.Dot(n, op.normal) > 0.99f) {
					// Calculate ellipse distance properly (not circle)
					Vector2 otherUV = new Vector2(Vector3.Dot(op.worldCenter - center, r), Vector3.Dot(op.worldCenter - center, u));
					Vector2 toHit = uv - otherUV; // direction from other portal toward hit point
					
					// Check if overlapping using ellipse distance
					float dx = toHit.x / portalHalfSize.x;
					float dy = toHit.y / portalHalfSize.y;
					float ellipseDist = Mathf.Sqrt(dx * dx + dy * dy);
					
					if (ellipseDist < 2.1f) { // portals overlap (2 radii + small margin)
						// Try position in direction from other portal toward raycast hit
						Vector2 dir = toHit.magnitude > 0.01f ? toHit.normalized : Vector2.right;
						
						// Calculate required separation in ellipse space
						float sepX = portalHalfSize.x * 2.1f;
						float sepY = portalHalfSize.y * 2.1f;
						Vector2 minSep = new Vector2(sepX, sepY);
						
						// Project direction onto ellipse axes to get proper separation
						Vector2 ellipseSep = new Vector2(dir.x * minSep.x, dir.y * minSep.y);
						float minDist = ellipseSep.magnitude;
						
						uv = otherUV + dir * minDist;
						
						// Clamp and check if still in bounds
						uv.x = Mathf.Clamp(uv.x, -clamp.x, clamp.x);
						uv.y = Mathf.Clamp(uv.y, -clamp.y, clamp.y);
						
						// If clamping moved us back into overlap, reject placement
						Vector2 newOffset = uv - otherUV;
						float newDx = newOffset.x / portalHalfSize.x;
						float newDy = newOffset.y / portalHalfSize.y;
						float newEllipseDist = Mathf.Sqrt(newDx * newDx + newDy * newDy);
						if (newEllipseDist < 2.05f) return;
					}
				}
			}
			portalManager.PlacePortal(i, center + r * uv.x + u * uv.y, n, r, u, col, wallOffset);
		}
	}
}
