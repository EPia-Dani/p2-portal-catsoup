// PortalGun.cs
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
		[SerializeField] float minPortalSize = 0.2f;
		[SerializeField] float maxPortalSize = 1.0f;
		[SerializeField] float resizeSpeed = 0.5f;
		
		[Header("Portal Bounds Visualization")]
		[SerializeField] bool showPlacementBounds = true;
		[SerializeField] Color boundsColor = new Color(1f, 1f, 0f, 0.8f);
		[SerializeField] float boundsOffset = 0.001f;
		[SerializeField] int ellipseSegments = 64;

		private PlayerInput input;
		private LineRenderer boundsRenderer;
		private float currentPortalScale = 1f;
		private float portalAspectRatio = 1f;
		private Vector2 initialPortalHalfSize;
		static readonly Vector3 ViewCenter = new(0.5f, 0.5f, 0f);

		struct PortalPlacement {
			public Vector3 position;
			public Vector3 normal;
			public Vector3 right;
			public Vector3 up;
			public Collider surface;
			public bool isValid;
		}

		void Start() {
			if (!shootCamera) shootCamera = Camera.main;
			if (!portalManager) portalManager = GetComponent<PortalManager>();
			input = InputManager.PlayerInput;
			
			// Store initial portal half size and calculate aspect ratio
			initialPortalHalfSize = portalHalfSize;
			if (initialPortalHalfSize.y > 0.001f) {
				portalAspectRatio = initialPortalHalfSize.x / initialPortalHalfSize.y;
			}
			UpdatePortalSizeFromScale();
			
			// Initialize LineRenderer
			boundsRenderer = gameObject.GetComponent<LineRenderer>();
			if (boundsRenderer == null) {
				boundsRenderer = gameObject.AddComponent<LineRenderer>();
			}
			
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

		void Update() {
			HandlePortalResize();
			
			// Calculate placement info once per frame
			PortalPlacement placement = CalculatePortalPlacement();
			
			// Update visualization
			if (showPlacementBounds) {
				UpdateBoundsVisualization(placement);
			} else if (boundsRenderer != null) {
				boundsRenderer.enabled = false;
			}
			
			// Handle shooting
			if (input.Player.ShootBlue.WasPerformedThisFrame() && placement.isValid) {
				PlacePortal(0, placement);
			}
			if (input.Player.ShootOrange.WasPerformedThisFrame() && placement.isValid) {
				PlacePortal(1, placement);
			}
		}
		
		void HandlePortalResize() {
			if (input == null || input.Player.ResizePortal == null) return;
			
			Vector2 scrollDelta = input.Player.ResizePortal.ReadValue<Vector2>();
			float scrollY = scrollDelta.y;
			
			if (Mathf.Abs(scrollY) > 0.001f) {
				float scaleDelta = scrollY * resizeSpeed;
				float baseHeight = initialPortalHalfSize.y > 0.001f ? initialPortalHalfSize.y : initialPortalHalfSize.x;
				float minScale = minPortalSize / baseHeight;
				float maxScale = maxPortalSize / baseHeight;
				currentPortalScale = Mathf.Clamp(currentPortalScale + scaleDelta, minScale, maxScale);
				UpdatePortalSizeFromScale();
			}
		}
		
		void UpdatePortalSizeFromScale() {
			float scaledHeight = initialPortalHalfSize.y * currentPortalScale;
			float scaledWidth = scaledHeight * portalAspectRatio;
			portalHalfSize = new Vector2(scaledWidth, scaledHeight);
		}

		PortalPlacement CalculatePortalPlacement() {
			PortalPlacement placement = new PortalPlacement { isValid = false };
			
			if (!shootCamera) return placement;
			
			// Raycast
			if (!Physics.Raycast(shootCamera.ViewportPointToRay(ViewCenter), out var hit, shootDistance, shootMask, QueryTriggerInteraction.Ignore)) {
				return placement;
			}
			
			if (hit.collider == null || !hit.collider.enabled) {
				return placement;
			}
			
			// Calculate orientation
			Vector3 normal = hit.normal;
			Vector3 up = GetUpVector(normal);
			Vector3 right = Vector3.Cross(normal, up);
			
			// Calculate position
			Vector3 surfaceCenter = hit.collider.bounds.center + normal * Vector3.Dot(hit.point - hit.collider.bounds.center, normal);
			Vector2 clampRange = GetClampRange(hit.collider.bounds, right, up);
			
			if (clampRange.x <= 0f || clampRange.y <= 0f) {
				return placement;
			}
			
			Vector3 offset = hit.point - surfaceCenter;
			Vector2 localPos = new Vector2(
				Mathf.Clamp(Vector3.Dot(offset, right), -clampRange.x, clampRange.x),
				Mathf.Clamp(Vector3.Dot(offset, up), -clampRange.y, clampRange.y)
			);
			
			placement.position = surfaceCenter + right * localPos.x + up * localPos.y;
			placement.normal = normal;
			placement.right = right;
			placement.up = up;
			placement.surface = hit.collider;
			placement.isValid = true;
			
			return placement;
		}

		void PlacePortal(int index, PortalPlacement placement) {
			if (!portalManager || index < 0 || index > 1) return;
			
			// Recalculate surface center and local position for overlap check
			Vector3 surfaceCenter = placement.surface.bounds.center + placement.normal * Vector3.Dot(placement.position - placement.surface.bounds.center, placement.normal);
			Vector3 offset = placement.position - surfaceCenter;
			Vector2 localPos = new Vector2(
				Vector3.Dot(offset, placement.right),
				Vector3.Dot(offset, placement.up)
			);
			Vector2 clampRange = GetClampRange(placement.surface.bounds, placement.right, placement.up);
			
			// Check overlap prevention
			if (ShouldPreventOverlap(index, placement.surface, placement.normal, surfaceCenter, placement.right, placement.up, ref localPos, clampRange)) {
				return;
			}
			
			Vector3 finalPosition = surfaceCenter + placement.right * localPos.x + placement.up * localPos.y;
			
			int otherIndex = 1 - index;
			bool removeOtherPortal = false;
			if (otherIndex >= 0 && otherIndex < portalManager.portalSurfaces.Length) {
				var otherSurface = portalManager.portalSurfaces[otherIndex];
				var otherNormal = portalManager.portalNormals[otherIndex];
				if (otherSurface != null && otherSurface == placement.surface && Vector3.Dot(placement.normal, otherNormal) >= 0.99f) {
					float otherScale = portalManager.portalScaleMultipliers.Length > otherIndex ? portalManager.portalScaleMultipliers[otherIndex] : 1f;
					if (otherScale <= 0f) otherScale = 1f;
					Vector2 otherPortalHalfSize = new Vector2(initialPortalHalfSize.x * otherScale, initialPortalHalfSize.y * otherScale);

					// If the new portal placement wouldn't fit with the other portal's scale, mark the other portal for removal
					if (!PortalFitsOnSurface(placement.surface, finalPosition, placement.normal, placement.right, placement.up, otherPortalHalfSize)) {
						removeOtherPortal = true;
					}

					if (!removeOtherPortal) {
						// Ensure the existing portal would fit if it used the new portal's scale
						Vector3 otherPosition = portalManager.portalCenters[otherIndex];
						Vector3 otherRight = portalManager.portalRights != null && portalManager.portalRights.Length > otherIndex ? portalManager.portalRights[otherIndex] : Vector3.zero;
						Vector3 otherUp = portalManager.portalUps != null && portalManager.portalUps.Length > otherIndex ? portalManager.portalUps[otherIndex] : Vector3.zero;

						if (otherRight.sqrMagnitude < 1e-4f || otherUp.sqrMagnitude < 1e-4f) {
							otherUp = Vector3.ProjectOnPlane(Vector3.up, otherNormal);
							if (otherUp.sqrMagnitude < 1e-4f) {
								otherUp = GetUpVector(otherNormal);
							}
							otherUp.Normalize();
							otherRight = Vector3.Cross(otherNormal, otherUp).normalized;
						} else {
							otherRight = otherRight.normalized;
							otherUp = otherUp.normalized;
						}

						if (!PortalFitsOnSurface(otherSurface, otherPosition, otherNormal, otherRight, otherUp, portalHalfSize)) {
							removeOtherPortal = true;
						}
					}
				}
			}

			if (removeOtherPortal) {
				portalManager.RemovePortal(otherIndex);
			}

			portalManager.PlacePortal(index, finalPosition, placement.normal, placement.right, placement.up, placement.surface, wallOffset, currentPortalScale);
		}

		void UpdateBoundsVisualization(PortalPlacement placement) {
			if (boundsRenderer == null) return;
			
			if (!placement.isValid) {
				boundsRenderer.enabled = false;
				return;
			}
			
			Vector3 ellipseCenter = placement.position + placement.normal * boundsOffset;
			boundsRenderer.startColor = boundsColor;
			boundsRenderer.endColor = boundsColor;
			
			for (int i = 0; i <= ellipseSegments; i++) {
				float angle = 2f * Mathf.PI * i / ellipseSegments;
				float x = portalHalfSize.x * Mathf.Cos(angle);
				float y = portalHalfSize.y * Mathf.Sin(angle);
				boundsRenderer.SetPosition(i, ellipseCenter + placement.right * x + placement.up * y);
			}
			
			boundsRenderer.enabled = true;
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

		Vector2 GetClampRange(Bounds b, Vector3 r, Vector3 u) {
			return GetClampRange(b, r, u, portalHalfSize);
		}

		Vector2 GetClampRange(Bounds b, Vector3 r, Vector3 u, Vector2 halfSize) {
			Vector3 e = b.extents;
			float cr = e.x * Mathf.Abs(r.x) + e.y * Mathf.Abs(r.y) + e.z * Mathf.Abs(r.z) - halfSize.x - clampSkin;
			float cu = e.x * Mathf.Abs(u.x) + e.y * Mathf.Abs(u.y) + e.z * Mathf.Abs(u.z) - halfSize.y - clampSkin;
			return new Vector2(cr, cu);
		}

		bool PortalFitsOnSurface(Collider surface, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Vector2 halfSize) {
			if (surface == null) return false;

			Vector3 surfaceCenter = surface.bounds.center + normal * Vector3.Dot(position - surface.bounds.center, normal);
			Vector3 offset = position - surfaceCenter;
			Vector2 localPos = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up));
			Vector2 clampRange = GetClampRange(surface.bounds, right, up, halfSize);
			if (clampRange.x <= 0f || clampRange.y <= 0f) return false;
			return Mathf.Abs(localPos.x) <= clampRange.x && Mathf.Abs(localPos.y) <= clampRange.y;
		}

		bool ShouldPreventOverlap(int index, Collider surface, Vector3 normal, Vector3 center, Vector3 right, Vector3 up, ref Vector2 localPos, Vector2 clampRange) {
			if (portalManager == null) return false;
			int otherIndex = 1 - index;
			if (otherIndex < 0 || otherIndex >= portalManager.portalSurfaces.Length) return false;

			var otherSurface = portalManager.portalSurfaces[otherIndex];
			var otherNormal = portalManager.portalNormals[otherIndex];
			if (otherSurface == null || otherSurface != surface || Vector3.Dot(normal, otherNormal) < 0.99f) return false;

			Vector3 otherOffset = portalManager.portalCenters[otherIndex] - center;
			Vector2 otherLocalPos = new Vector2(Vector3.Dot(otherOffset, right), Vector3.Dot(otherOffset, up));

			// Calculate other portal's half size using its stored scale multiplier
			float otherScale = portalManager.portalScaleMultipliers.Length > otherIndex ? portalManager.portalScaleMultipliers[otherIndex] : 1f;
			if (otherScale <= 0f) otherScale = 1f;
			Vector2 otherPortalHalfSize = new Vector2(initialPortalHalfSize.x * otherScale, initialPortalHalfSize.y * otherScale);
			Vector2 minSeparation = portalHalfSize + otherPortalHalfSize;
			if (minSeparation.x < 1e-3f) minSeparation.x = 1e-3f;
			if (minSeparation.y < 1e-3f) minSeparation.y = 1e-3f;

			Vector2 dist = localPos - otherLocalPos;
			Vector2 distNormalized = new Vector2(dist.x / minSeparation.x, dist.y / minSeparation.y);
			float m = distNormalized.magnitude;

			if (m < 1.05f) {
				float k = m > 1e-3f ? 1.05f / m : 1f;
				localPos = otherLocalPos + distNormalized * minSeparation * k;
				localPos.x = Mathf.Clamp(localPos.x, -clampRange.x, clampRange.x);
				localPos.y = Mathf.Clamp(localPos.y, -clampRange.y, clampRange.y);
				dist = localPos - otherLocalPos;
				distNormalized = new Vector2(dist.x / minSeparation.x, dist.y / minSeparation.y);
				if (distNormalized.magnitude < 1.05f) return true;
			}
			return false;
		}
	}
}
