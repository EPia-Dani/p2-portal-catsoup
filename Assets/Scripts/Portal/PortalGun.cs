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

		const float Eps = 1e-6f, SurfDot = 0.995f, OverlapTol = 0.95f;
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
			var camTransform = shootCamera ? shootCamera.transform : null;
			Vector3 r = camTransform != null ? Vector3.ProjectOnPlane(camTransform.right, n) : Vector3.right;
			if (r.sqrMagnitude <= Eps) r = Vector3.Cross(n, Vector3.up);
			r.Normalize(); Vector3 u = Vector3.Cross(n, r).normalized;

			// plane center (project bounds center onto plane through hit)
			Bounds b = col.bounds; Vector3 center = b.center + Vector3.Project(hit.point - b.center, n);

			// clamp extents along portal axes
			Vector3 absR = new Vector3(Mathf.Abs(r.x), Mathf.Abs(r.y), Mathf.Abs(r.z));
			Vector3 absU = new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z));
			Vector3 ext = b.extents;
			Vector2 clamp = new Vector2(Vector3.Dot(ext, absR) - portalHalfSize.x - clampSkin,
					Vector3.Dot(ext, absU) - portalHalfSize.y - clampSkin);
			if (clamp.x <= 0f || clamp.y <= 0f) return;

			// initial UV (local portal coords)
			Vector3 delta = hit.point - center;
			Vector2 uv = new Vector2(Mathf.Clamp(Vector3.Dot(delta, r), -clamp.x, clamp.x),
					Mathf.Clamp(Vector3.Dot(delta, u), -clamp.y, clamp.y));

			// check overlap with other portal and try to move uv away
			var otherOpt = portalManager.GetPortalState(1 - i);
			if (otherOpt.HasValue) {
				var op = otherOpt.Value;
				if (col == op.surface && Mathf.Abs(Vector3.Dot(n, op.normal)) > SurfDot) {
					Vector2 otherUV = new Vector2(Vector3.Dot(op.worldCenter - center, r), Vector3.Dot(op.worldCenter - center, u));
					Vector2 dUV = uv - otherUV;
					// inline RequiredSpacing for 'need'
					Vector2 d1 = dUV.sqrMagnitude > Eps ? dUV.normalized : Vector2.right;
					float ra1 = Vector2.Scale(portalHalfSize, d1).magnitude;
					Vector3 w1 = r * d1.x + u * d1.y; w1 = w1.sqrMagnitude > Eps ? w1.normalized : r;
					Vector2 db1 = new Vector2(Vector3.Dot(w1, op.right), Vector3.Dot(w1, op.up)); db1 = db1.sqrMagnitude > Eps ? db1.normalized : Vector2.right;
					float rb1 = Vector2.Scale(portalHalfSize, db1).magnitude;
					float need = ra1 + rb1 + clampSkin * 2f;
					if (dUV.sqrMagnitude < need * need) {
						Vector2 dir = dUV.sqrMagnitude > Eps ? dUV.normalized : Vector2.right;
						float step = Mathf.Max(portalHalfSize.x, portalHalfSize.y);
						for (int k = 0; k < 3; ++k) {
							float dist = need + step * k;
							Vector2 cand = otherUV + dir * dist;
							cand.x = Mathf.Clamp(cand.x, -clamp.x, clamp.x); cand.y = Mathf.Clamp(cand.y, -clamp.y, clamp.y);
							Vector2 off = cand - otherUV; if (off.sqrMagnitude < Eps) continue;
							// inline RequiredSpacing for 'req'
							Vector2 d2 = off.sqrMagnitude > Eps ? off.normalized : Vector2.right;
							float ra2 = Vector2.Scale(portalHalfSize, d2).magnitude;
							Vector3 w2 = r * d2.x + u * d2.y; w2 = w2.sqrMagnitude > Eps ? w2.normalized : r;
							Vector2 db2 = new Vector2(Vector3.Dot(w2, op.right), Vector3.Dot(w2, op.up)); db2 = db2.sqrMagnitude > Eps ? db2.normalized : Vector2.right;
							float rb2 = Vector2.Scale(portalHalfSize, db2).magnitude;
							float req = ra2 + rb2 + clampSkin * 2f;

							if (off.sqrMagnitude >= req * req * OverlapTol) { uv = cand; goto PLACE; }
						}
						return; // couldn't resolve
					}
				}
			}

			PLACE:
			portalManager.PlacePortal(i, center + r * uv.x + u * uv.y, n, r, u, col, wallOffset);
		}
	}
}
