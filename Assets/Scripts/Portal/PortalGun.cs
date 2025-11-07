// PortalGun.cs  (kept, only relies on PortalManager.PlacePortal and arrays)
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
		
		[Header("Portal Resizing")]
		[SerializeField] float minScale = 0.5f; // Minimum scale multiplier (e.g., 0.5 = 50% of base size)
		[SerializeField] float maxScale = 2.0f; // Maximum scale multiplier (e.g., 2.0 = 200% of base size)
		[SerializeField] float resizeSpeed = 0.5f; // How fast the portal resizes per scroll unit
		[SerializeField] float basePortalMeshScale = 2.0f; // Base scale to apply to portal mesh (mesh radius = portalHalfSize, so full size needs 2x scale)
		
		[Header("Portal Bounds Visualization")]
		[SerializeField] bool showPlacementBounds = true;
		[SerializeField] Color boundsColor = new Color(1f, 1f, 0f, 0.8f);
		[SerializeField] float boundsOffset = 0.001f; // Slight offset from surface to avoid z-fighting
		[SerializeField] int ellipseSegments = 64; // Number of segments for smooth ellipse

		private PlayerInput input;
		private LineRenderer boundsRenderer;
		private GameObject boundsRendererObject; // Separate GameObject for LineRenderer
		private float currentPortalScale = 1f; // Current scale multiplier (1.0 = base size)
		static readonly Vector3 ViewCenter = new(0.5f, 0.5f, 0f);

		void Start() {
			if (!shootCamera) shootCamera = Camera.main;
			if (!portalManager) portalManager = GetComponent<PortalManager>();
			input = InputManager.PlayerInput;
			
			// Create a separate GameObject for the LineRenderer on UI layer
			boundsRendererObject = new GameObject("PortalBoundsVisualization");
			boundsRendererObject.transform.SetParent(transform);
			boundsRendererObject.transform.localPosition = Vector3.zero;
			boundsRendererObject.transform.localRotation = Quaternion.identity;
			boundsRendererObject.transform.localScale = Vector3.one;
			
			// Set to UI layer so it's only visible to main camera (portal cameras exclude UI layer)
			boundsRendererObject.layer = LayerMask.NameToLayer("UI");
			
			// Initialize LineRenderer for portal bounds visualization
			boundsRenderer = boundsRendererObject.AddComponent<LineRenderer>();
			
			// Create material for line rendering
			Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Transparent/Diffuse");
			if (shader != null) {
				boundsRenderer.material = new Material(shader);
			}
			
			boundsRenderer.startWidth = 0.02f;
			boundsRenderer.endWidth = 0.02f;
			boundsRenderer.useWorldSpace = true;
			boundsRenderer.loop = true;
			boundsRenderer.positionCount = ellipseSegments + 1;
			boundsRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			boundsRenderer.receiveShadows = false;
			boundsRenderer.enabled = false;
		}
		
		void OnDestroy() {
			// Clean up the bounds renderer GameObject
			if (boundsRendererObject != null) {
				Destroy(boundsRendererObject);
			}
		}

		void Update() {
			// Handle portal resizing with scroll wheel
			HandlePortalResize();
			
			if (input.Player.ShootBlue.WasPerformedThisFrame())  Fire(0);
			if (input.Player.ShootOrange.WasPerformedThisFrame()) Fire(1);
			
			// Update portal bounds visualization
			if (showPlacementBounds) {
				UpdateBoundsVisualization();
			} else if (boundsRenderer != null) {
				boundsRenderer.enabled = false;
			}
		}
		
		void HandlePortalResize() {
			if (input == null || input.Player.ResizePortal == null) return;
			
			Vector2 scrollDelta = input.Player.ResizePortal.ReadValue<Vector2>();
			float scrollY = scrollDelta.y;
			
			if (Mathf.Abs(scrollY) > 0.001f) {
				// Update scale multiplier based on scroll input (scrollY is already a delta, typically ~0.1 per scroll tick)
				float scaleDelta = scrollY * resizeSpeed;
				currentPortalScale = Mathf.Clamp(currentPortalScale + scaleDelta, minScale, maxScale);
			}
		}

		void Fire(int index) {
			if (!shootCamera || !portalManager || index < 0 || index > 1) return;
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(ViewCenter), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) return;
			if (hit.collider == null || !hit.collider.enabled) return;

			Vector3 normal = hit.normal;
			Vector3 up = GetUpVector(normal);
			Vector3 right = Vector3.Cross(normal, up);

			Vector3 surfaceCenter = GetSurfaceCenter(hit, hit.collider.bounds, normal);
			Vector2 clampRange = GetClampRange(hit.collider.bounds, right, up);
			if (clampRange.x <= 0f || clampRange.y <= 0f) return;

			Vector2 localPos = GetLocalPosition(hit.point, surfaceCenter, right, up, clampRange);
			if (ShouldPreventOverlap(index, hit.collider, normal, surfaceCenter, right, up, ref localPos, clampRange)) return;

			Vector3 finalPosition = surfaceCenter + right * localPos.x + up * localPos.y;
			// Calculate actual transform scale: base scale * current multiplier
			// The base mesh radius matches portalHalfSize, so we need to scale by 2x to get full size, then apply multiplier
			float transformScale = basePortalMeshScale * currentPortalScale;
			portalManager.PlacePortal(index, finalPosition, normal, right, up, hit.collider, wallOffset, transformScale);
		}

		Vector3 GetUpVector(Vector3 normal) {
			if (shootCamera == null) return Vector3.Cross(normal, Vector3.right).normalized;
			Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
			if (up.sqrMagnitude < 1e-4f) {
				up = Vector3.ProjectOnPlane(shootCamera.transform.forward, normal);
				if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(normal, Vector3.right);
			}
			return up.normalized;
		}

		static Vector3 GetSurfaceCenter(RaycastHit hit, Bounds bounds, Vector3 normal) {
			return bounds.center + normal * Vector3.Dot(hit.point - bounds.center, normal);
		}

		Vector2 GetClampRange(Bounds b, Vector3 r, Vector3 u) {
			Vector3 e = b.extents;
			// Use scaled portal size for bounds calculations
			float scaledHalfSizeX = portalHalfSize.x * currentPortalScale;
			float scaledHalfSizeY = portalHalfSize.y * currentPortalScale;
			float cr = e.x * Mathf.Abs(r.x) + e.y * Mathf.Abs(r.y) + e.z * Mathf.Abs(r.z) - scaledHalfSizeX - clampSkin;
			float cu = e.x * Mathf.Abs(u.x) + e.y * Mathf.Abs(u.y) + e.z * Mathf.Abs(u.z) - scaledHalfSizeY - clampSkin;
			return new Vector2(cr, cu);
		}

		static Vector2 GetLocalPosition(Vector3 hitPoint, Vector3 center, Vector3 r, Vector3 u, Vector2 clamp) {
			Vector3 d = hitPoint - center;
			return new Vector2(Mathf.Clamp(Vector3.Dot(d, r), -clamp.x, clamp.x),
			                   Mathf.Clamp(Vector3.Dot(d, u), -clamp.y, clamp.y));
		}

		bool ShouldPreventOverlap(int index, Collider surface, Vector3 normal, Vector3 center, Vector3 right, Vector3 up, ref Vector2 localPos, Vector2 clampRange) {
			if (portalManager == null) return false;
			int otherIndex = 1 - index;
			if (otherIndex < 0 || otherIndex >= portalManager.portalSurfaces.Length) return false;

			var otherSurface = portalManager.portalSurfaces[otherIndex];
			var otherNormal  = portalManager.portalNormals[otherIndex];
			if (otherSurface == null || otherSurface != surface || Vector3.Dot(normal, otherNormal) < 0.99f) return false;

			Vector3 otherOffset = portalManager.portalCenters[otherIndex] - center;
			Vector2 otherLocalPos = new(Vector3.Dot(otherOffset, right), Vector3.Dot(otherOffset, up));

			// Use scaled portal size for overlap calculations
			float scaledHalfSizeX = portalHalfSize.x * currentPortalScale;
			float scaledHalfSizeY = portalHalfSize.y * currentPortalScale;
			Vector2 scaledHalfSize = new Vector2(scaledHalfSizeX, scaledHalfSizeY);
			
			Vector2 dist = (localPos - otherLocalPos) / scaledHalfSize;
			float m = dist.magnitude;

			if (m < 2.1f) {
				float k = m > 1e-3f ? 2.1f / m : 1f;
				localPos = otherLocalPos + dist * scaledHalfSize * k;
				localPos.x = Mathf.Clamp(localPos.x, -clampRange.x, clampRange.x);
				localPos.y = Mathf.Clamp(localPos.y, -clampRange.y, clampRange.y);
				dist = (localPos - otherLocalPos) / scaledHalfSize;
				if (dist.magnitude < 2.05f) return true;
			}
			return false;
		}
		
		void UpdateBoundsVisualization() {
			if (boundsRenderer == null || !shootCamera) {
				if (boundsRenderer != null) boundsRenderer.enabled = false;
				return;
			}
			
			// Perform raycast to see where portal would be placed
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(ViewCenter), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) {
				boundsRenderer.enabled = false;
				return;
			}
			
			if (hit.collider == null || !hit.collider.enabled) {
				boundsRenderer.enabled = false;
				return;
			}
			
			// Calculate portal placement orientation
			Vector3 normal = hit.normal;
			Vector3 up = GetUpVector(normal);
			Vector3 right = Vector3.Cross(normal, up);
			
			Vector3 surfaceCenter = GetSurfaceCenter(hit, hit.collider.bounds, normal);
			Vector2 clampRange = GetClampRange(hit.collider.bounds, right, up);
			
			if (clampRange.x <= 0f || clampRange.y <= 0f) {
				boundsRenderer.enabled = false;
				return;
			}
			
			Vector2 localPos = GetLocalPosition(hit.point, surfaceCenter, right, up, clampRange);
			Vector3 ellipseCenter = surfaceCenter + right * localPos.x + up * localPos.y + normal * boundsOffset;
			
			// Generate ellipse points using scaled portal size (multiplier applied)
			boundsRenderer.startColor = boundsColor;
			boundsRenderer.endColor = boundsColor;
			
			// Apply scale multiplier to ellipse size for visualization
			float ellipseSizeX = portalHalfSize.x * currentPortalScale;
			float ellipseSizeY = portalHalfSize.y * currentPortalScale;
			
			for (int i = 0; i <= ellipseSegments; i++) {
				float angle = 2f * Mathf.PI * i / ellipseSegments;
				float x = ellipseSizeX * Mathf.Cos(angle);
				float y = ellipseSizeY * Mathf.Sin(angle);
				Vector3 point = ellipseCenter + right * x + up * y;
				boundsRenderer.SetPosition(i, point);
			}
			
			boundsRenderer.enabled = true;
		}
	}
}

