using System.Collections;
using Input;
using UnityEngine;

public enum PortalColor { Blue, Orange }

public class PortalGun : MonoBehaviour {
	[SerializeField] private PortalRenderer bluePortal;
	[SerializeField] private PortalRenderer orangePortal;

	[SerializeField] private LayerMask shootMask = ~0;
	[SerializeField] private float shootDistance = 1000f;
	[SerializeField] private Camera shootCamera;

	[SerializeField] private float portalAppearDuration = 0.3f;
	[SerializeField] private float portalTargetRadius = 0.4f;
	[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f);
	[SerializeField] private float wallOffset = 0.02f;
	[SerializeField] private float clampSkin = 0.01f;
	
	private readonly PortalRenderer[] _portals = new PortalRenderer[2];
	private readonly int[] _portalLayers = new int[2];

	private PlayerInput _controls;

	// support data of the wall plane each portal is mounted on
	private struct Support {
		public Collider col;
		public Vector3 n;         // plane normal (points out of surface)
		public Vector3 planePoint;// a point on plane (usually where we placed)
	}
	private readonly Support[] _support = new Support[2];

	private void Awake() {
		_controls = new PlayerInput();
		if (!shootCamera) shootCamera = Camera.main;

		_portals[0] = bluePortal;
		_portals[1] = orangePortal;

		_portalLayers[0] = LayerMask.NameToLayer("Blue");
		_portalLayers[1] = LayerMask.NameToLayer("Orange");
	}

	private void OnEnable() => _controls?.Enable();
	private void OnDisable() => _controls?.Disable();
	private void OnDestroy() => _controls?.Dispose();

	private void Update() {
		if (_controls.Player.ShootBlue.WasPerformedThisFrame()) FirePortal(PortalColor.Blue);
		if (_controls.Player.ShootOrange.WasPerformedThisFrame()) FirePortal(PortalColor.Orange);
	}

	private void FirePortal(PortalColor color) {
		int portalIndex = (int)color;
		int otherIndex = 1 - portalIndex;
		
		if (!shootCamera) return;
		
		Ray ray = shootCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
		if (!Physics.Raycast(ray, out RaycastHit hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
		if (!hit.collider || !hit.collider.enabled) return;

		int hitLayer = hit.collider.gameObject.layer;
		if (hitLayer == _portalLayers[portalIndex]) return;

		bool snapToWall = hitLayer == _portalLayers[otherIndex];
		if (snapToWall && _support[otherIndex].col == null) return;

		if (!GetPlacementData(ray, hit, snapToWall, otherIndex, out Vector3 normal, out Vector3 targetPoint, out Collider surfaceCollider))
			return;

		ComputePlaneBasis(normal, shootCamera.transform, out Vector3 right, out Vector3 up);

		if (!TryClampOnBounds(surfaceCollider, targetPoint, normal, right, up, 
			portalHalfSize, clampSkin, _portals[otherIndex], portalHalfSize,
			out Vector3 finalPoint, out Vector3 planeCenter))
			return;

		PlacePortal(portalIndex, finalPoint, normal, up, surfaceCollider, planeCenter);
	}

	private bool GetPlacementData(Ray ray, RaycastHit hit, bool snapToWall, int otherIndex,
		out Vector3 normal, out Vector3 targetPoint, out Collider surfaceCollider) {
		
		if (snapToWall) {
			Support otherSupport = _support[otherIndex];
			normal = otherSupport.n;
			Plane wallPlane = new Plane(normal, otherSupport.planePoint);
			if (!wallPlane.Raycast(ray, out float distance)) {
				targetPoint = Vector3.zero;
				surfaceCollider = null;
				return false;
			}
			targetPoint = ray.GetPoint(distance);
			surfaceCollider = otherSupport.col;
		} else {
			normal = hit.normal;
			targetPoint = hit.point;
			surfaceCollider = hit.collider;
		}
		
		return true;
	}

	private void PlacePortal(int portalIndex, Vector3 position, Vector3 normal, Vector3 up, Collider surface, Vector3 planeCenter) {
		_portals[portalIndex].transform.SetPositionAndRotation(
			position + normal * wallOffset,
			Quaternion.LookRotation(-normal, up)
		);

		_support[portalIndex] = new Support { 
			col = surface, 
			n = normal, 
			planePoint = planeCenter 
		};

		StartCoroutine(AppearPortal(_portals[portalIndex]));
	}

	private static void ComputePlaneBasis(Vector3 normal, Transform cam, out Vector3 right, out Vector3 up) {
		Vector3 camRight = cam ? cam.right : Vector3.right;
		Vector3 projected = Vector3.ProjectOnPlane(camRight, normal);
		
		right = projected.sqrMagnitude > 1e-8f ? projected.normalized : Vector3.Cross(normal, Vector3.up).normalized;
		up = Vector3.Cross(normal, right).normalized;
	}

	private static float ProjectAABBExtent(Vector3 extents, Vector3 direction) {
		return Mathf.Abs(direction.x) * extents.x + 
		       Mathf.Abs(direction.y) * extents.y + 
		       Mathf.Abs(direction.z) * extents.z;
	}

	private static bool EllipsesOverlap(
		float u1, float v1, Vector2 size1,
		float u2, float v2, Vector2 size2,
		Vector3 planeRight, Vector3 planeUp,
		Vector3 ellipse2Right, Vector3 ellipse2Up)
	{
		float du = u2 - u1;
		float dv = v2 - v1;
		float centerDistSq = du * du + dv * dv;
		
		float maxRadius = Mathf.Max(size1.x, size1.y, size2.x, size2.y);
		if (centerDistSq > (maxRadius * 4) * (maxRadius * 4)) return false;
		if (centerDistSq < 0.0001f) return true;
		
		float centerDist = Mathf.Sqrt(centerDistSq);
		float dirU = du / centerDist;
		float dirV = dv / centerDist;
		
		float radius1 = GetEllipseRadiusInDirection(size1.x, size1.y, dirU, dirV);
		
		// Transform direction to ellipse 2's local space
		float e2RightU = Vector3.Dot(ellipse2Right, planeRight);
		float e2RightV = Vector3.Dot(ellipse2Right, planeUp);
		float e2UpU = Vector3.Dot(ellipse2Up, planeRight);
		float e2UpV = Vector3.Dot(ellipse2Up, planeUp);
		
		float localDirX = -(dirU * e2RightU + dirV * e2RightV);
		float localDirY = -(dirU * e2UpU + dirV * e2UpV);
		float localDirLen = Mathf.Sqrt(localDirX * localDirX + localDirY * localDirY);
		
		if (localDirLen > 0.001f) {
			localDirX /= localDirLen;
			localDirY /= localDirLen;
		}
		
		float radius2 = GetEllipseRadiusInDirection(size2.x, size2.y, localDirX, localDirY);
		
		return centerDist < (radius1 + radius2);
	}
	
	private static float GetEllipseRadiusInDirection(float a, float b, float dirX, float dirY) {
		return Mathf.Sqrt((a * dirX) * (a * dirX) + (b * dirY) * (b * dirY));
	}

	private static bool TryClampOnBounds(
		Collider collider, Vector3 hitPoint,
		Vector3 normal, Vector3 right, Vector3 up,
		Vector2 portalSize, float margin,
		PortalRenderer otherPortal, Vector2 otherSize,
		out Vector3 clampedPoint, out Vector3 planeCenter)
	{
		Bounds bounds = collider.bounds;
		float maxRight = ProjectAABBExtent(bounds.extents, right);
		float maxUp = ProjectAABBExtent(bounds.extents, up);

		if (maxRight <= portalSize.x + margin || maxUp <= portalSize.y + margin) {
			clampedPoint = hitPoint;
			planeCenter = bounds.center;
			return false;
		}

		planeCenter = bounds.center + normal * Vector3.Dot(normal, hitPoint - bounds.center);
		Vector3 offset = hitPoint - planeCenter;
		float u = Vector3.Dot(offset, right);
		float v = Vector3.Dot(offset, up);

		float clampRange = maxRight - portalSize.x - margin;
		u = Mathf.Clamp(u, -clampRange, clampRange);
		clampRange = maxUp - portalSize.y - margin;
		v = Mathf.Clamp(v, -clampRange, clampRange);

		if (otherPortal && otherPortal.gameObject.activeInHierarchy) {
			Transform other = otherPortal.transform;
			Vector3 otherNormal = -other.forward;
			
			if (Mathf.Abs(Vector3.Dot(otherNormal, normal)) > 0.995f) {
				float dist = Vector3.Dot(other.position - planeCenter, normal);
				if (Mathf.Abs(dist) < 0.05f) {
					Vector3 otherOnPlane = other.position - normal * dist;
					float otherU = Vector3.Dot(otherOnPlane - planeCenter, right);
					float otherV = Vector3.Dot(otherOnPlane - planeCenter, up);
					
					if (EllipsesOverlap(u, v, portalSize, otherU, otherV, otherSize, 
					                    right, up, other.right, other.up)) {
						clampedPoint = hitPoint;
						return false;
					}
				}
			}
		}

		clampedPoint = planeCenter + right * u + up * v;
		return true;
	}

	private IEnumerator AppearPortal(PortalRenderer portal) {
		portal.gameObject.SetActive(true);
		portal.SetCircleRadius(0f);
		
		for (float t = 0; t < portalAppearDuration; t += Time.deltaTime) {
			float progress = t / portalAppearDuration;
			if (portalAppearCurve != null) progress = portalAppearCurve.Evaluate(progress);
			portal.SetCircleRadius(portalTargetRadius * progress);
			yield return null;
		}
		
		portal.SetCircleRadius(portalTargetRadius);
	}
}
