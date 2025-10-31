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
			if (!shootCamera || !portalManager) return;
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			var col = hit.collider;
			if (!col || !col.enabled) return;

			Vector3 n = hit.normal;
			Vector3 u = Vector3.ProjectOnPlane(Vector3.up, n);
			if (u.sqrMagnitude < 1e-4f) {
				u = Vector3.ProjectOnPlane(shootCamera.transform.forward, n);
				if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(n, Vector3.right);
			}
			u.Normalize();
			Vector3 r = Vector3.Cross(n, u);

			Vector3 center = col.bounds.center + Vector3.Project(hit.point - col.bounds.center, n);
			Vector3 ext = col.bounds.extents;
			float clampR = ext.x * Mathf.Abs(r.x) + ext.y * Mathf.Abs(r.y) + ext.z * Mathf.Abs(r.z) - portalHalfSize.x - clampSkin;
			float clampU = ext.x * Mathf.Abs(u.x) + ext.y * Mathf.Abs(u.y) + ext.z * Mathf.Abs(u.z) - portalHalfSize.y - clampSkin;
			if (clampR <= 0f || clampU <= 0f) return;

			float uvR = Mathf.Clamp(Vector3.Dot(hit.point - center, r), -clampR, clampR);
			float uvU = Mathf.Clamp(Vector3.Dot(hit.point - center, u), -clampU, clampU);

			int oi = 1 - index;
			if (portalManager.portalSurfaces[oi] == col && Vector3.Dot(n, portalManager.portalNormals[oi]) > 0.99f) {
				Vector3 toOther = portalManager.portalCenters[oi] - center;
				float oR = Vector3.Dot(toOther, r);
				float oU = Vector3.Dot(toOther, u);
				float eR = (uvR - oR) / portalHalfSize.x;
				float eU = (uvU - oU) / portalHalfSize.y;
				float mag = Mathf.Sqrt(eR * eR + eU * eU);

				if (mag < 2.1f) {
					float scale = mag > 1e-3f ? 2.1f / mag : 1f;
					uvR = Mathf.Clamp(oR + eR * scale * portalHalfSize.x, -clampR, clampR);
					uvU = Mathf.Clamp(oU + eU * scale * portalHalfSize.y, -clampU, clampU);
					eR = (uvR - oR) / portalHalfSize.x;
					eU = (uvU - oU) / portalHalfSize.y;
					if (Mathf.Sqrt(eR * eR + eU * eU) < 2.05f) return;
				}
			}

			portalManager.PlacePortal(index, center + r * uvR + u * uvU, n, r, u, col, wallOffset);
		}
	}
}
