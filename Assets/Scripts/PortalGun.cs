using UnityEngine;

public class PortalGun : MonoBehaviour
{
	[SerializeField] private LayerMask shootMask = ~0;
	[SerializeField] private float shootDistance = 1000f;
	[SerializeField] private Camera shootCamera;
	[SerializeField] private PortalManager portalManager;
	[SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f);
	[SerializeField] private float wallOffset = 0.02f;
	[SerializeField] private float clampSkin = 0.01f;

	const float EPS = 1e-6f;
	const float SURF_DOT = 0.995f;
	const float OVERLAP_TOL = 0.95f;
	private static readonly Vector3 VIEWPORT_CENTER = new Vector3(0.5f, 0.5f, 0f);

	private Input.PlayerInput _controls;
	private Transform _cameraTransform;

	private void Awake()
	{
		// Get shared input instance from InputManager
		_controls = InputManager.PlayerInput;
		
		// Cache camera transform to avoid per-frame lookups
		if (shootCamera)
			_cameraTransform = shootCamera.transform;
		
		// Auto-find PortalManager if not assigned
		if (!portalManager)
			portalManager = GetComponent<PortalManager>();
		
		if (!portalManager)
			Debug.LogError("PortalManager not found! Assign it in the inspector or add it to the same GameObject as PortalGun.", gameObject);
	}

	private void Update()
	{
		// Poll for input in Update (frame-synchronized, like FPSController)
		if (_controls.Player.ShootBlue.WasPerformedThisFrame()) Fire(0);
		if (_controls.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
	}

	private void Fire(int i)
	{
		if (!shootCamera || !portalManager) return;

		Ray ray = shootCamera.ViewportPointToRay(VIEWPORT_CENTER);
		if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
		if (!hit.collider || !hit.collider.enabled) return;

		// Build plane basis at hit
		Vector3 normal = hit.normal.normalized;
		ComputeBasis(normal, _cameraTransform.right, out Vector3 right, out Vector3 up);

		// Plane center: project collider bounds center onto plane along its normal
		Bounds b = hit.collider.bounds;
		Vector3 center = b.center + normal * Vector3.Dot(normal, hit.point - b.center);

		// Compute clamp extents and check if there's room
		Vector2 clamp = ComputeClampExtents(b, right, up);
		if (clamp.x <= 0f || clamp.y <= 0f) return;

		// Get initial UV position clamped to surface bounds
		Vector2 uv = GetInitialUVClamped(hit.point, center, right, up, clamp);

		// Try to resolve overlap with other portal
		if (!TryResolveOverlapWithOtherPortal(i, ref uv, clamp, center, right, up, normal, hit.collider)) return;

		// Notify portal manager to place the portal
		Vector3 pos = center + right * uv.x + up * uv.y;
		portalManager.PlacePortal(i, pos, normal, right, up, hit.collider, wallOffset);
	}

	private Vector2 ComputeClampExtents(Bounds b, Vector3 right, Vector3 up)
	{
		return new Vector2(
			ProjectExtent(b.extents, right) - portalHalfSize.x - clampSkin,
			ProjectExtent(b.extents, up) - portalHalfSize.y - clampSkin
		);
	}

	private Vector2 GetInitialUVClamped(Vector3 hitPoint, Vector3 center, Vector3 right, Vector3 up, Vector2 clamp)
	{
		Vector2 uv = new Vector2(Vector3.Dot(hitPoint - center, right), Vector3.Dot(hitPoint - center, up));
		uv.x = Mathf.Clamp(uv.x, -clamp.x, clamp.x);
		uv.y = Mathf.Clamp(uv.y, -clamp.y, clamp.y);
		return uv;
	}

	private bool TryResolveOverlapWithOtherPortal(int i, ref Vector2 uv, Vector2 clamp, Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Collider collider)
	{
		PortalManager.PortalState? otherPortalOpt = portalManager.GetPortalState(1 - i);
		if (!otherPortalOpt.HasValue) return true;
		
		PortalManager.PortalState otherPortal = otherPortalOpt.Value;
		if (collider != otherPortal.surface || !AreOnSameSurface(normal, otherPortal.normal)) return true;

		Vector2 otherUV = new Vector2(Vector3.Dot(otherPortal.worldCenter - center, right), Vector3.Dot(otherPortal.worldCenter - center, up));
		Vector2 delta = uv - otherUV;

		float need = MinSpacing(delta, right, up, otherPortal.right, otherPortal.up);
		if (delta.sqrMagnitude >= need * need) return true; // No overlap, position is valid

		// Try to find alternative position
		Vector2 dir = delta.sqrMagnitude > EPS ? delta.normalized : new Vector2(1f, 0f);
		float step = Mathf.Max(portalHalfSize.x, portalHalfSize.y);

		for (int k = 0; k < 3; k++)
		{
			Vector2 cand = otherUV + dir * (need + step * k);
			cand.x = Mathf.Clamp(cand.x, -clamp.x, clamp.x);
			cand.y = Mathf.Clamp(cand.y, -clamp.y, clamp.y);

			// Skip if candidate hasn't moved from other portal due to clamping
			Vector2 off = cand - otherUV;
			if (off.sqrMagnitude < EPS) continue;

			float req = MinSpacing(off, right, up, otherPortal.right, otherPortal.up);
			if (off.sqrMagnitude >= req * req * OVERLAP_TOL)
			{
				uv = cand;
				return true;
			}
		}

		return false; // No valid position found
	}

	private bool AreOnSameSurface(Vector3 normalA, Vector3 normalB)
		=> Mathf.Abs(Vector3.Dot(normalA, normalB)) > SURF_DOT;

	// ---- tiny helpers ----
	static void ComputeBasis(in Vector3 normal, in Vector3 refRight, out Vector3 right, out Vector3 up)
	{
		Vector3 proj = Vector3.ProjectOnPlane(refRight, normal);
		right = proj.sqrMagnitude > EPS ? proj.normalized : Vector3.Cross(normal, Vector3.up).normalized;
		up = Vector3.Cross(normal, right).normalized;
	}

	static float ProjectExtent(in Vector3 e, in Vector3 axis)
		=> Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;

	// Ellipse support distance along dir for both portals
	float MinSpacing(in Vector2 dirUV, in Vector3 rightA, in Vector3 upA, in Vector3 rightB, in Vector3 upB)
	{
		Vector2 d = dirUV.sqrMagnitude > EPS ? dirUV.normalized : new Vector2(1f, 0f);
		float ra = GetEllipseRadius(d);

		Vector3 worldDir = rightA * d.x + upA * d.y;
		worldDir = worldDir.sqrMagnitude > EPS ? worldDir.normalized : rightA;
		Vector2 dOther = new Vector2(Vector3.Dot(worldDir, rightB), Vector3.Dot(worldDir, upB));
		dOther = dOther.sqrMagnitude > EPS ? dOther.normalized : new Vector2(1f, 0f);

		float rb = GetEllipseRadius(dOther);
		return ra + rb + clampSkin * 2f;
	}

	float GetEllipseRadius(in Vector2 dir)
	{
		float dx = portalHalfSize.x * dir.x;
		float dy = portalHalfSize.y * dir.y;
		return Mathf.Sqrt(dx * dx + dy * dy); // Avoid Pow, use multiplication instead
	}
}
