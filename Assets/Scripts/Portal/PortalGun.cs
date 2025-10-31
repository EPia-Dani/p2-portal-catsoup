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

		static readonly Vector3 ViewportCenter = new(0.5f, 0.5f, 0f);
		const float SEP = 2.1f;       // push target in ellipse-space
		const float SEP_CHECK = 2.05f; // verify after clamp

		void Update() {
			var c = InputManager.PlayerInput;
			if (c.Player.ShootBlue.WasPerformedThisFrame())  Fire(0);
			if (c.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
		}

		void Fire(int i) {
			if (!shootCamera || !portalManager) return;

			Ray ray = shootCamera.ViewportPointToRay(ViewportCenter);
			if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			var col = hit.collider; if (!col || !col.enabled) return;

			// Basis
			Vector3 n = hit.normal;
			Vector3 r = Vector3.ProjectOnPlane(shootCamera.transform.right, n);
			float rsq = r.sqrMagnitude;
			if (rsq < 1e-4f) {
				r = Vector3.Cross(n, Vector3.up);
				if (r.sqrMagnitude < 1e-6f) r = Vector3.Cross(n, Vector3.forward);
			}
			r.Normalize();
			Vector3 u = Vector3.Cross(n, r); // orthonormal by construction

			// Plane center on the hit surface
			Bounds b = col.bounds;
			Vector3 bc = b.center;
			Vector3 center = bc + Vector3.Project(hit.point - bc, n);

			// Clamp extents along r/u using fused-abs dot
			Vector3 ext = b.extents;
			float ax = Mathf.Abs(r.x), ay = Mathf.Abs(r.y), az = Mathf.Abs(r.z);
			float ux = Mathf.Abs(u.x), uy = Mathf.Abs(u.y), uz = Mathf.Abs(u.z);
			float maxR = ext.x * ax + ext.y * ay + ext.z * az;
			float maxU = ext.x * ux + ext.y * uy + ext.z * uz;

			float clampR = maxR - portalHalfSize.x - clampSkin;
			float clampU = maxU - portalHalfSize.y - clampSkin;
			if (clampR <= 0f || clampU <= 0f) return;

			// Initial UV
			Vector3 d = hit.point - center;
			float uvR = Mathf.Clamp(Vector3.Dot(d, r), -clampR, clampR);
			float uvU = Mathf.Clamp(Vector3.Dot(d, u), -clampU, clampU);

			// Separation vs other portal on same face
			var otherOpt = portalManager.GetPortalState(1 - i);
			if (otherOpt.HasValue) {
				var op = otherOpt.Value;
				if (col == op.surface && Vector3.Dot(n, op.normal) > 0.99f) {
					Vector3 oc = op.worldCenter - center;
					float oR = Vector3.Dot(oc, r);
					float oU = Vector3.Dot(oc, u);

					float dR = uvR - oR;
					float dU = uvU - oU;

					// ellipse-space
					float eR = dR / portalHalfSize.x;
					float eU = dU / portalHalfSize.y;
					float mag = Mathf.Sqrt(eR * eR + eU * eU);

					if (mag < SEP) {
						// push along ellipse direction
						float dirR, dirU;
						if (mag > 1e-3f) {
							float s = SEP / mag;
							dirR = eR * s;
							dirU = eU * s;
						} else {
							dirR = SEP; dirU = 0f;
						}

						// back to RU
						uvR = oR + dirR * portalHalfSize.x;
						uvU = oU + dirU * portalHalfSize.y;

						// Clamp and verify
						uvR = Mathf.Clamp(uvR, -clampR, clampR);
						uvU = Mathf.Clamp(uvU, -clampU, clampU);

						float fR = (uvR - oR) / portalHalfSize.x;
						float fU = (uvU - oU) / portalHalfSize.y;
						if (Mathf.Sqrt(fR * fR + fU * fU) < SEP_CHECK) return;
					}
				}
			}

			Vector3 worldPos = center + r * uvR + u * uvU;
			portalManager.PlacePortal(i, worldPos, n, r, u, col, wallOffset);
		}
	}
}
