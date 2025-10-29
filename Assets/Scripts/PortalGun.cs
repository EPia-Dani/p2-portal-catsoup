using System.Collections;
using Input;
using UnityEngine;


public class PortalGun : MonoBehaviour {
	
	[SerializeField] private PortalRenderer[] _portals = new PortalRenderer[2];
	[SerializeField] private LayerMask shootMask = ~0;
	[SerializeField] private float shootDistance = 1000f;
	[SerializeField] private Camera shootCamera;

	[SerializeField] private float portalAppearDuration = 0.3f;
	[SerializeField] private float portalTargetRadius = 0.4f;
	[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	[SerializeField] private Vector2 portalHalfSize = new(0.45f, 0.45f);
	[SerializeField] private float wallOffset = 0.02f;
	[SerializeField] private float clampSkin = 0.01f;

	// Constants
	private const float Epsilon = 1e-6f;
	private const float OverlapTolerance = 0.95f;
	private const int DirectionAttempts = 4;
	private const float SurfaceNormalThreshold = 0.995f;

	
	private readonly Support[] _support = new Support[2];

	private PlayerInput _controls;

	private struct Support {
		public PortalPlane Plane;
		public Vector3 PortalCenter;
		public bool IsValid => Plane.IsValid;
	}

	private void Awake() { _controls = new PlayerInput(); }
	
	private void OnEnable() { _controls.Enable(); }
	
	private void Update() {
		if (_controls.Player.ShootBlue.WasPerformedThisFrame()) FirePortal(0);
		if (_controls.Player.ShootOrange.WasPerformedThisFrame()) FirePortal(1);
	}

	private void FirePortal(int portalIndex) {
		
		int otherIndex = 1 - portalIndex;

		Ray ray = shootCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
		if (!Physics.Raycast(ray, out RaycastHit hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
		if (!hit.collider || !hit.collider.enabled) return;


		Support other = _support[otherIndex];
		
		if (!PortalPlane.TryCreate(
			hit.collider,
			hit.normal,
			hit.point,
			shootCamera.transform,
			portalHalfSize,
			clampSkin,
			out PortalPlane plane
		)) return;

		Vector2 uv = plane.Clamp(plane.ToUV(hit.point));
		
		bool sharePlane = (other.IsValid && plane.SameSurface(other.Plane));
		
		if (sharePlane && !ResolveOverlap(other, plane, ref uv)) return;

		Vector3 worldPoint = plane.FromUV(uv);
		PlacePortal(portalIndex, plane, worldPoint);
	}


	/// <summary>
	///
	/// </summary>
	

	private bool ResolveOverlap(Support other, PortalPlane plane, ref Vector2 uv) {
		if (!other.IsValid || !plane.SameSurface(other.Plane)) return true;

		Vector2 otherUV = plane.ToUV(other.PortalCenter);
		Vector2 delta = uv - otherUV;
		float spacing = MinSpacing(delta, other, plane);
		if (delta.sqrMagnitude >= spacing * spacing) return true;

		Vector2 primary = delta.sqrMagnitude > Epsilon ? delta.normalized : Vector2.right;
		Vector2 orthogonal = new Vector2(-primary.y, primary.x);

		return TryDirection(primary, otherUV, other, plane, ref uv)
		    || TryDirection(-primary, otherUV, other, plane, ref uv)
		    || TryDirection(orthogonal, otherUV, other, plane, ref uv)
		    || TryDirection(-orthogonal, otherUV, other, plane, ref uv);
	}

	private bool TryDirection(Vector2 dir, Vector2 origin, Support other, PortalPlane plane, ref Vector2 uv) {
		if (dir.sqrMagnitude < Epsilon) return false;
		dir.Normalize();

		float required = MinSpacing(dir, other, plane);
		float step = Mathf.Max(portalHalfSize.x, portalHalfSize.y);

		for (int i = 0; i < DirectionAttempts; i++) {
			Vector2 candidate = plane.Clamp(origin + dir * (required + step * i));
			Vector2 offset = candidate - origin;
			if (offset.sqrMagnitude < Epsilon) continue;

			float needed = MinSpacing(offset, other, plane);
			if (offset.sqrMagnitude >= needed * needed * OverlapTolerance) {
				uv = candidate;
				return true;
			}
		}

		return false;
	}

	private float MinSpacing(Vector2 direction, Support other, PortalPlane plane) {
		if (direction.sqrMagnitude < Epsilon) direction = Vector2.right;
		direction.Normalize();

		float radiusA = RadiusInDirection(direction);
		Vector3 worldDir = plane.Direction(direction);
		if (worldDir.sqrMagnitude < Epsilon) worldDir = plane.Right;
		else worldDir.Normalize();

		Vector2 otherDir = other.Plane.ToPlaneVector(worldDir);
		float radiusB = RadiusInDirection(otherDir);
		return radiusA + radiusB + clampSkin * 2f;
	}

	private float RadiusInDirection(Vector2 dir) {
		if (dir.sqrMagnitude < Epsilon) return Mathf.Max(portalHalfSize.x, portalHalfSize.y);
		dir.Normalize();
		float x = portalHalfSize.x * dir.x;
		float y = portalHalfSize.y * dir.y;
		return Mathf.Sqrt(x * x + y * y);
	}

	private void PlacePortal(int index, PortalPlane plane, Vector3 position) {
		Transform portalTransform = _portals[index].transform;
		portalTransform.SetPositionAndRotation(
			position + plane.Normal * wallOffset,
			Quaternion.LookRotation(-plane.Normal, plane.Up)
		);

		_support[index] = new Support {
			Plane = plane,
			PortalCenter = position
		};

		StartCoroutine(AppearPortal(_portals[index]));
		
		// Verificar si ambos portales están colocados para iniciar apertura
		CheckAndStartOpening();
	}

	private void CheckAndStartOpening() {
		if (!_portals[0].gameObject.activeInHierarchy || !_portals[1].gameObject.activeInHierarchy) return;
		TryStartOpening(_portals[0]);
		TryStartOpening(_portals[1]);
	}

	private static void TryStartOpening(PortalRenderer portal) {
		if (!portal.IsOpening && !portal.IsFullyOpen) {
			portal.StartOpening();
		}
	}

	private readonly struct PortalPlane {
		public Collider Surface { get; }
		public Vector3 Center { get; }
		public Vector3 Normal { get; }
		public Vector3 Right { get; }
		public Vector3 Up { get; }
		public Vector2 ClampExtents { get; }
		public bool IsValid => Surface != null;

		private PortalPlane(Collider surface, Vector3 center, Vector3 normal, Vector3 right, Vector3 up, Vector2 clampExtents) {
			Surface = surface;
			Center = center;
			Normal = normal;
			Right = right;
			Up = up;
			ClampExtents = clampExtents;
		}

		public static bool TryCreate(Collider surface, Vector3 normal, Vector3 anchor, Transform reference, Vector2 halfSize, float margin, out PortalPlane plane) {
			if (!surface) {
				plane = default;
				return false;
			}

			ComputeBasis(normal, reference, out Vector3 right, out Vector3 up);

			Bounds bounds = surface.bounds;
			Vector3 center = bounds.center + normal * Vector3.Dot(normal, anchor - bounds.center);
			float maxRight = ProjectExtent(bounds.extents, right);
			float maxUp = ProjectExtent(bounds.extents, up);

			Vector2 clamp = new Vector2(maxRight - halfSize.x - margin, maxUp - halfSize.y - margin);
			if (clamp.x <= 0f || clamp.y <= 0f) {
				plane = default;
				return false;
			}

			plane = new PortalPlane(surface, center, normal, right, up, clamp);
			return true;
		}

		public static bool TryProject(PortalPlane plane, Ray ray, out Vector3 point) {
			Plane wall = new Plane(plane.Normal, plane.Center);
			if (!wall.Raycast(ray, out float distance)) {
				point = default;
				return false;
			}
			point = ray.GetPoint(distance);
			return true;
		}

		public Vector2 ToUV(Vector3 world) {
			Vector3 offset = world - Center;
			return new Vector2(Vector3.Dot(offset, Right), Vector3.Dot(offset, Up));
		}

		public Vector3 FromUV(Vector2 uv) => Center + Right * uv.x + Up * uv.y;

		public Vector2 Clamp(Vector2 uv) => new Vector2(
			Mathf.Clamp(uv.x, -ClampExtents.x, ClampExtents.x),
			Mathf.Clamp(uv.y, -ClampExtents.y, ClampExtents.y)
		);

		public Vector3 Direction(Vector2 dir) => Right * dir.x + Up * dir.y;
		public Vector2 ToPlaneVector(Vector3 dir) => new Vector2(Vector3.Dot(dir, Right), Vector3.Dot(dir, Up));
		public bool SameSurface(PortalPlane other) => Surface == other.Surface && Mathf.Abs(Vector3.Dot(Normal, other.Normal)) > SurfaceNormalThreshold;

		private static void ComputeBasis(Vector3 normal, Transform reference, out Vector3 right, out Vector3 up) {
			Vector3 camRight = reference ? reference.right : Vector3.right;
			Vector3 projected = Vector3.ProjectOnPlane(camRight, normal);
			right = projected.sqrMagnitude > Epsilon ? projected.normalized : Vector3.Cross(normal, Vector3.up).normalized;
			up = Vector3.Cross(normal, right).normalized;
		}

		private static float ProjectExtent(Vector3 extents, Vector3 axis) {
			return Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
		}
	}

	private IEnumerator AppearPortal(PortalRenderer portal) {
		portal.gameObject.SetActive(true);
		portal.SetCircleRadius(0f);

		float elapsed = 0f;
		while (elapsed < portalAppearDuration) {
			elapsed += Time.deltaTime;
			float progress = Mathf.Min(elapsed / portalAppearDuration, 1f);
			progress = portalAppearCurve.Evaluate(progress);
			portal.SetCircleRadius(portalTargetRadius * progress);
			yield return null;
		}

		portal.SetCircleRadius(portalTargetRadius);
	}
}