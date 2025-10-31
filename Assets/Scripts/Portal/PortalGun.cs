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
					Vector2 otherUV = new Vector2(Vector3.Dot(op.worldCenter - center, r), Vector3.Dot(op.worldCenter - center, u));
					Vector2 toHit = uv - otherUV;
					
					// Convert to ellipse-space (unit circle)
					Vector2 ellipseToHit = new Vector2(toHit.x / portalHalfSize.x, toHit.y / portalHalfSize.y);
					float ellipseDist = ellipseToHit.magnitude;
					
					if (ellipseDist < 2.1f) { // portals overlap
						// Move away in ellipse-space, then convert back
						Vector2 ellipseDir = ellipseToHit.magnitude > 0.01f ? ellipseToHit.normalized : Vector2.right;
						Vector2 newEllipsePos = ellipseDir * 2.1f;
						
						// Convert back to world space
						Vector2 worldOffset = new Vector2(newEllipsePos.x * portalHalfSize.x, newEllipsePos.y * portalHalfSize.y);
						uv = otherUV + worldOffset;
						
						// Clamp to bounds
						uv.x = Mathf.Clamp(uv.x, -clamp.x, clamp.x);
						uv.y = Mathf.Clamp(uv.y, -clamp.y, clamp.y);
						
						// Verify still separated after clamping
						Vector2 finalToHit = uv - otherUV;
						Vector2 finalEllipse = new Vector2(finalToHit.x / portalHalfSize.x, finalToHit.y / portalHalfSize.y);
						if (finalEllipse.magnitude < 2.05f) return;
					}
				}
			}
			portalManager.PlacePortal(i, center + r * uv.x + u * uv.y, n, r, u, col, wallOffset);
		}
	}
}
