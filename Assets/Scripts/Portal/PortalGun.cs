using UnityEngine;

namespace Portal {
	public class PortalGun : MonoBehaviour
	{
		[SerializeField] private LayerMask shootMask = ~0;
		[SerializeField] private float shootDistance = 1000f;
		[SerializeField] private Camera shootCamera;
		[SerializeField] private PortalManager portalManager;
		[SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f);
		[SerializeField] private float wallOffset = 0.02f;
		[SerializeField] private float clampSkin = 0.01f;

		private const float Eps = 1e-6f;
		private const float SurfDot = 0.995f;
		private const float OverlapTol = 0.95f;
		private static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

		private Input.PlayerInput _controls;
		private Transform _cameraTransform;
		
		// Cached vectors to avoid allocations
		private Vector2 _cachedClamp;
		private Vector2 _cachedUV;
		private Vector2 _cachedOtherUV;
		private Vector2 _cachedDelta;
		private Vector2 _cachedDir;
		private Vector2 _cachedCand;
		private Vector2 _cachedOff;
		private Vector3 _cachedNormal;
		private Vector3 _cachedRight;
		private Vector3 _cachedUp;
		private Vector3 _cachedCenter;
		private Vector3 _cachedPos;
		private Bounds _cachedBounds;

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

			Ray ray = shootCamera.ViewportPointToRay(ViewportCenter);
			if (!Physics.Raycast(ray, out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			if (!hit.collider || !hit.collider.enabled) return;

			// Build plane basis at hit (cache vectors to avoid allocations)
			_cachedNormal = hit.normal;
			float normalMag = Mathf.Sqrt(_cachedNormal.x * _cachedNormal.x + _cachedNormal.y * _cachedNormal.y + _cachedNormal.z * _cachedNormal.z);
			if (normalMag > 1e-6f)
			{
				float invMag = 1f / normalMag;
				_cachedNormal.x *= invMag;
				_cachedNormal.y *= invMag;
				_cachedNormal.z *= invMag;
			}
			
			Vector3 cameraRight = _cameraTransform.right;
			ComputeBasis(_cachedNormal, cameraRight, out _cachedRight, out _cachedUp);

			// Plane center: project collider bounds center onto plane along its normal (cache bounds)
			_cachedBounds = hit.collider.bounds;
			float dot = _cachedNormal.x * (hit.point.x - _cachedBounds.center.x) + 
			            _cachedNormal.y * (hit.point.y - _cachedBounds.center.y) + 
			            _cachedNormal.z * (hit.point.z - _cachedBounds.center.z);
			_cachedCenter.x = _cachedBounds.center.x + _cachedNormal.x * dot;
			_cachedCenter.y = _cachedBounds.center.y + _cachedNormal.y * dot;
			_cachedCenter.z = _cachedBounds.center.z + _cachedNormal.z * dot;

			// Compute clamp extents and check if there's room
			ComputeClampExtents(_cachedBounds, _cachedRight, _cachedUp, out _cachedClamp);
			if (_cachedClamp.x <= 0f || _cachedClamp.y <= 0f) return;

			// Get initial UV position clamped to surface bounds
			GetInitialUVClamped(hit.point, _cachedCenter, _cachedRight, _cachedUp, _cachedClamp, out _cachedUV);

			// Try to resolve overlap with other portal
			if (!TryResolveOverlapWithOtherPortal(i, ref _cachedUV, _cachedClamp, _cachedCenter, _cachedRight, _cachedUp, _cachedNormal, hit.collider)) return;

			// Notify portal manager to place the portal
			_cachedPos.x = _cachedCenter.x + _cachedRight.x * _cachedUV.x + _cachedUp.x * _cachedUV.y;
			_cachedPos.y = _cachedCenter.y + _cachedRight.y * _cachedUV.x + _cachedUp.y * _cachedUV.y;
			_cachedPos.z = _cachedCenter.z + _cachedRight.z * _cachedUV.x + _cachedUp.z * _cachedUV.y;
			portalManager.PlacePortal(i, _cachedPos, _cachedNormal, _cachedRight, _cachedUp, hit.collider, wallOffset);
		}

		private void ComputeClampExtents(Bounds b, Vector3 right, Vector3 up, out Vector2 result)
		{
			result.x = ProjectExtent(b.extents, right) - portalHalfSize.x - clampSkin;
			result.y = ProjectExtent(b.extents, up) - portalHalfSize.y - clampSkin;
		}

		private void GetInitialUVClamped(Vector3 hitPoint, Vector3 center, Vector3 right, Vector3 up, Vector2 clamp, out Vector2 result)
		{
			float dx = hitPoint.x - center.x;
			float dy = hitPoint.y - center.y;
			float dz = hitPoint.z - center.z;
			result.x = Mathf.Clamp(dx * right.x + dy * right.y + dz * right.z, -clamp.x, clamp.x);
			result.y = Mathf.Clamp(dx * up.x + dy * up.y + dz * up.z, -clamp.y, clamp.y);
		}

		private bool TryResolveOverlapWithOtherPortal(int i, ref Vector2 uv, Vector2 clamp, Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Collider col)
		{
			PortalManager.PortalState? otherPortalOpt = portalManager.GetPortalState(1 - i);
			if (!otherPortalOpt.HasValue) return true;
		
			PortalManager.PortalState otherPortal = otherPortalOpt.Value;
			if (col != otherPortal.surface || !AreOnSameSurface(normal, otherPortal.normal)) return true;

			// Calculate other UV using cached vectors (avoid allocations)
			float otherDx = otherPortal.worldCenter.x - center.x;
			float otherDy = otherPortal.worldCenter.y - center.y;
			float otherDz = otherPortal.worldCenter.z - center.z;
			_cachedOtherUV.x = otherDx * right.x + otherDy * right.y + otherDz * right.z;
			_cachedOtherUV.y = otherDx * up.x + otherDy * up.y + otherDz * up.z;
			
			_cachedDelta.x = uv.x - _cachedOtherUV.x;
			_cachedDelta.y = uv.y - _cachedOtherUV.y;

			float need = MinSpacing(_cachedDelta, right, up, otherPortal.right, otherPortal.up);
			float deltaSqrMag = _cachedDelta.x * _cachedDelta.x + _cachedDelta.y * _cachedDelta.y;
			if (deltaSqrMag >= need * need) return true; // No overlap, position is valid

			// Try to find alternative position (avoid allocation from normalized)
			if (deltaSqrMag > Eps)
			{
				float invMag = 1f / Mathf.Sqrt(deltaSqrMag);
				_cachedDir.x = _cachedDelta.x * invMag;
				_cachedDir.y = _cachedDelta.y * invMag;
			}
			else
			{
				_cachedDir.x = 1f;
				_cachedDir.y = 0f;
			}
			
			float step = Mathf.Max(portalHalfSize.x, portalHalfSize.y);

			for (int k = 0; k < 3; k++)
			{
				float dist = need + step * k;
				_cachedCand.x = _cachedOtherUV.x + _cachedDir.x * dist;
				_cachedCand.y = _cachedOtherUV.y + _cachedDir.y * dist;
				_cachedCand.x = Mathf.Clamp(_cachedCand.x, -clamp.x, clamp.x);
				_cachedCand.y = Mathf.Clamp(_cachedCand.y, -clamp.y, clamp.y);

				// Skip if candidate hasn't moved from other portal due to clamping
				_cachedOff.x = _cachedCand.x - _cachedOtherUV.x;
				_cachedOff.y = _cachedCand.y - _cachedOtherUV.y;
				float offSqrMag = _cachedOff.x * _cachedOff.x + _cachedOff.y * _cachedOff.y;
				if (offSqrMag < Eps) continue;

				float req = MinSpacing(_cachedOff, right, up, otherPortal.right, otherPortal.up);
				if (offSqrMag >= req * req * OverlapTol)
				{
					uv = _cachedCand;
					return true;
				}
			}

			return false; // No valid position found
		}

		private bool AreOnSameSurface(Vector3 normalA, Vector3 normalB)
			=> Mathf.Abs(Vector3.Dot(normalA, normalB)) > SurfDot;

		// ---- tiny helpers ----
		static void ComputeBasis(in Vector3 normal, in Vector3 refRight, out Vector3 right, out Vector3 up)
		{
			Vector3 proj = Vector3.ProjectOnPlane(refRight, normal);
			right = proj.sqrMagnitude > Eps ? proj.normalized : Vector3.Cross(normal, Vector3.up).normalized;
			up = Vector3.Cross(normal, right).normalized;
		}

		static float ProjectExtent(in Vector3 e, in Vector3 axis)
			=> Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;

		// Ellipse support distance along dir for both portals
		float MinSpacing(in Vector2 dirUV, in Vector3 rightA, in Vector3 upA, in Vector3 rightB, in Vector3 upB)
		{
			Vector2 d;
			float dirUVSqrMag = dirUV.x * dirUV.x + dirUV.y * dirUV.y;
			if (dirUVSqrMag > Eps)
			{
				float invMag = 1f / Mathf.Sqrt(dirUVSqrMag);
				d.x = dirUV.x * invMag;
				d.y = dirUV.y * invMag;
			}
			else
			{
				d.x = 1f;
				d.y = 0f;
			}
			float ra = GetEllipseRadius(d);

			Vector3 worldDir;
			worldDir.x = rightA.x * d.x + upA.x * d.y;
			worldDir.y = rightA.y * d.x + upA.y * d.y;
			worldDir.z = rightA.z * d.x + upA.z * d.y;
			
			float worldDirSqrMag = worldDir.x * worldDir.x + worldDir.y * worldDir.y + worldDir.z * worldDir.z;
			if (worldDirSqrMag > Eps)
			{
				float invMag = 1f / Mathf.Sqrt(worldDirSqrMag);
				worldDir.x *= invMag;
				worldDir.y *= invMag;
				worldDir.z *= invMag;
			}
			else
			{
				worldDir = rightA;
			}
			
			Vector2 dOther;
			dOther.x = worldDir.x * rightB.x + worldDir.y * rightB.y + worldDir.z * rightB.z;
			dOther.y = worldDir.x * upB.x + worldDir.y * upB.y + worldDir.z * upB.z;
			
			float dOtherSqrMag = dOther.x * dOther.x + dOther.y * dOther.y;
			if (dOtherSqrMag > Eps)
			{
				float invMag = 1f / Mathf.Sqrt(dOtherSqrMag);
				dOther.x *= invMag;
				dOther.y *= invMag;
			}
			else
			{
				dOther.x = 1f;
				dOther.y = 0f;
			}

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
}
