// PortalRenderer.cs
// Handles portal rendering with recursive portal views using URP

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal.Rendering {
	public class PortalRenderer : MonoBehaviour {
		[Header("Portal Pair")]
		[SerializeField] public PortalRenderer pair;

		[Header("Scene References")]
		[SerializeField] private Camera mainCamera;
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;

		[Header("Render Settings")]
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		[Header("Culling (Optional)")]
		[SerializeField] private bool gateByMainCamera = false;
		[SerializeField] private bool gateByScreenCoverage = false;
		[SerializeField] private float minScreenCoverageFraction = 0.01f;

		[Header("Wall Collider")]
		[Tooltip("Wall collider to disable for player when entering portal")]
		[SerializeField] private Collider wallCollider;

		// Portal scale for size-based teleportation
		public float PortalScale { get; set; } = 1f;

		// Rendering state
		private RenderTexture _renderTexture;
		private Material _portalMaterial;
		private bool _visible = true;
		private bool _ready = false;

		// Recursion matrices (cached to avoid allocations)
		private Matrix4x4[] _recursionMatrices = Array.Empty<Matrix4x4>();
		private readonly Matrix4x4 _mirrorMatrix = Matrix4x4.Scale(new Vector3(-1, 1, -1));

		// Frustum culling cache
		private readonly Plane[] _frustumPlanes = new Plane[6];

		// Dynamic RT sizing (simplified)
		private const float RtResizeThreshold = 0.2f; // 20% change required
		private const int RtResizeStableFrames = 6;
		private int _rtTargetWidth;
		private int _rtTargetHeight;
		private int _rtStableCounter;

		// Screen coverage cache (reused to avoid allocations)
		private readonly Vector3[] _coveragePoints = new Vector3[8];
		private Vector2 _screenMin;
		private Vector2 _screenMax;

		// Cached recursion level
		private int _lastMaxRecursionLevel;

		public bool IsReadyToRender {
			get => _ready;
			set => _ready = value;
		}

		public Transform PortalTransform => transform;

		void Awake() {
			InitializeReferences();
			DetectGeometry();
			SetupCamera();
			AllocateRenderTexture();
			InitializeRecursion();
		}

		void OnDestroy() {
			ReleaseRenderTexture();
			if (_portalMaterial) Destroy(_portalMaterial);
		}

		void OnEnable() {
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		void OnDisable() {
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		#region Initialization

		void InitializeReferences() {
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			if (surfaceRenderer && !_portalMaterial) {
				_portalMaterial = surfaceRenderer.material;
			}

			_visible = surfaceRenderer && surfaceRenderer.enabled;
		}

		void DetectGeometry() {
			// Simplified: Just cache bounds for coverage calculation
			// Removed complex cylinder detection - not needed for basic portal rendering
		}

		void SetupCamera() {
			if (!portalCamera) return;

			portalCamera.enabled = false;
			portalCamera.forceIntoRenderTexture = true;
			portalCamera.allowHDR = false;
			portalCamera.clearFlags = CameraClearFlags.SolidColor;
			portalCamera.backgroundColor = Color.black;
			portalCamera.useOcclusionCulling = false;

			var urpData = portalCamera.GetUniversalAdditionalCameraData();
			if (urpData != null) {
				urpData.renderPostProcessing = false;
				urpData.antialiasing = AntialiasingMode.None;
				urpData.requiresColorOption = CameraOverrideOption.On;
				urpData.requiresDepthOption = CameraOverrideOption.On;
				urpData.SetRenderer(0);
			}
		}

		void AllocateRenderTexture() {
			ReleaseRenderTexture();

			_renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_renderTexture.Create();

			if (portalCamera) {
				portalCamera.targetTexture = _renderTexture;
				portalCamera.pixelRect = new Rect(0, 0, textureWidth, textureHeight);
			}

			if (_portalMaterial) {
				_portalMaterial.mainTexture = _renderTexture;
			}
		}

		void ReleaseRenderTexture() {
			if (_renderTexture) {
				_renderTexture.Release();
				Destroy(_renderTexture);
				_renderTexture = null;
			}
		}

		void InitializeRecursion() {
			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}
		}

		#endregion

		#region Public API

		public void ConfigurePortal(int width, int height, int recLimit, int frameSkip) {
			bool needsReallocation = textureWidth != width || textureHeight != height;
			
			textureWidth = width;
			textureHeight = height;
			
			if (needsReallocation) {
				AllocateRenderTexture();
			}

			SetRecursionLimit(recLimit);
			SetFrameSkipInterval(frameSkip);
		}

		public void SetRecursionLimit(int limit) {
			recursionLimit = Mathf.Max(1, limit);
			if (_recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}
		}

		public void SetFrameSkipInterval(int interval) {
			frameSkipInterval = Mathf.Max(1, interval);
		}

		public void SetVisible(bool visible) {
			_visible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			
			if (!visible) {
				_ready = false;
				ClearRenderTexture();
			}
		}

		public void SetWallCollider(Collider col) {
			wallCollider = col;
		}

		#endregion

		#region Rendering

		void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) {
			// Only render for main camera
			if (cam != mainCamera) return;

			// Frame skipping
			if ((Time.frameCount % frameSkipInterval) != 0) return;

			// Early exit checks
			if (!pair) return;
			if (!_visible || !_ready) return;
			if (!pair._visible || !pair._ready) return;

			// Optional culling gates
			if (gateByMainCamera && !IsMainCameraVisible()) return;
			if (gateByScreenCoverage && GetScreenSpaceCoverage() < minScreenCoverageFraction) return;

			// Calculate and cache recursion level
			_lastMaxRecursionLevel = CalculateMaxRecursionLevel();
			
			// Adjust render texture size if needed
			UpdateRenderTextureSize(_lastMaxRecursionLevel);

			// Render portal view
			RenderPortalView(ctx, _lastMaxRecursionLevel);
		}

		void RenderPortalView(ScriptableRenderContext ctx, int maxRecursionLevel) {
			ClearRenderTexture();

			// Build recursion matrices
			BuildRecursionMatrices();

			// Render each recursion level
			Vector3 exitPos = pair.transform.position;
			Vector3 exitForward = pair.transform.forward;
			int startLevel = Mathf.Min(maxRecursionLevel, _recursionMatrices.Length - 1);

			for (int i = startLevel; i >= 0; i--) {
				RenderRecursionLevel(_recursionMatrices[i], exitPos, exitForward);
			}
		}

		void BuildRecursionMatrices() {
			Matrix4x4 portalTransform = pair.transform.localToWorldMatrix * _mirrorMatrix * transform.worldToLocalMatrix;
			Matrix4x4 current = mainCamera.transform.localToWorldMatrix;

			for (int i = 0; i < _recursionMatrices.Length; i++) {
				current = portalTransform * current;
				_recursionMatrices[i] = current;
			}
		}

		void RenderRecursionLevel(Matrix4x4 worldMatrix, Vector3 exitPos, Vector3 exitForward) {
			if (!portalCamera || !_visible) return;

			// Set camera position and rotation
			Vector3 pos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, up));

			// Ensure pixel rect matches render texture
			if (portalCamera.targetTexture != null) {
				portalCamera.pixelRect = new Rect(0, 0, portalCamera.targetTexture.width, portalCamera.targetTexture.height);
			}

			// Calculate oblique clipping plane
			Vector3 planePoint = exitPos + exitForward * 0.001f;
			Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
			Vector3 normalCamera = Vector3.Normalize(-worldToCamera.MultiplyVector(exitForward));
			Vector3 pointCamera = worldToCamera.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(normalCamera.x, normalCamera.y, normalCamera.z, -Vector3.Dot(pointCamera, normalCamera));

			// Apply oblique projection and render
			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
		}

		void ClearRenderTexture() {
			if (_renderTexture == null) return;
			
			var previousActive = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = previousActive;
		}

		#endregion

		#region Recursion Calculation

		int CalculateMaxRecursionLevel() {
			if (!pair) return recursionLimit - 1;

			// Reduce recursion for vertical portals (common case)
			bool thisVertical = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) > 0.9f;
			bool pairVertical = Mathf.Abs(Vector3.Dot(pair.transform.forward, Vector3.up)) > 0.9f;
			if (thisVertical || pairVertical) {
				return Mathf.Min(2, recursionLimit - 1);
			}

			// Adjust based on portal angle
			float dot = Mathf.Clamp(Vector3.Dot(transform.forward, pair.transform.forward), -1f, 1f);
			float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

			if (angle < 45f) return 0; // Nearly parallel - no recursion needed
			if (angle < 135f) return Mathf.Min(1, recursionLimit - 1); // Moderate angle
			return recursionLimit - 1; // Opposite portals - full recursion
		}

		#endregion

		#region Dynamic Render Texture Sizing

		void UpdateRenderTextureSize(int maxRecursionLevel) {
			if (mainCamera == null) return;

			float coverage = Mathf.Clamp01(GetScreenSpaceCoverage());
			
			// Simplified scaling: coverage-based with quality floor
			float coverageFactor = Mathf.Pow(coverage, 0.6f); // Gentler curve
			float minQuality = 0.5f; // Never below 50% resolution
			float maxQuality = 1.5f; // Can go up to 150%
			float qualityScale = Mathf.Lerp(minQuality, maxQuality, coverageFactor);

			// Recursion bias: +10% per level (capped at 5 levels)
			float recursionBias = 1f + 0.1f * Mathf.Clamp(maxRecursionLevel, 0, 5);
			float finalScale = Mathf.Clamp(qualityScale * recursionBias, minQuality, 2.5f);

			// Calculate target size
			int targetW = GetClosestSizeTier(mainCamera.pixelWidth * finalScale);
			int targetH = GetClosestSizeTier(mainCamera.pixelHeight * finalScale);

			// Clamp to reasonable bounds
			int minW = Mathf.Max(512, Mathf.RoundToInt(mainCamera.pixelWidth * minQuality));
			int minH = Mathf.Max(288, Mathf.RoundToInt(mainCamera.pixelHeight * minQuality));
			targetW = Mathf.Clamp(targetW, minW, 2048);
			targetH = Mathf.Clamp(targetH, minH, 2048);

			// Check if resize is needed (with hysteresis)
			bool needsResize = (Mathf.Abs(targetW - textureWidth) > textureWidth * RtResizeThreshold) ||
			                   (Mathf.Abs(targetH - textureHeight) > textureHeight * RtResizeThreshold);

			if (!needsResize) {
				_rtTargetWidth = textureWidth;
				_rtTargetHeight = textureHeight;
				_rtStableCounter = 0;
				return;
			}

			// Debounce resize
			if (_rtTargetWidth != targetW || _rtTargetHeight != targetH) {
				_rtTargetWidth = targetW;
				_rtTargetHeight = targetH;
				_rtStableCounter = 0;
			} else {
				_rtStableCounter++;
			}

			if (_rtStableCounter >= RtResizeStableFrames) {
				textureWidth = _rtTargetWidth;
				textureHeight = _rtTargetHeight;
				_rtStableCounter = 0;
				AllocateRenderTexture();
			}
		}

		int GetClosestSizeTier(float value) {
			int[] tiers = { 256, 384, 512, 640, 768, 896, 1024, 1280, 1536, 1792, 2048 };
			int best = tiers[0];
			float bestDistance = Mathf.Abs(value - best);

			for (int i = 1; i < tiers.Length; i++) {
				float distance = Mathf.Abs(value - tiers[i]);
				if (distance < bestDistance) {
					bestDistance = distance;
					best = tiers[i];
				}
			}

			return best;
		}

		#endregion

		#region Culling

		bool IsMainCameraVisible() {
			if (!mainCamera || !surfaceRenderer) return false;

			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) {
				return false;
			}

			// Check if camera is looking at portal (simplified)
			Vector3 toCamera = (mainCamera.transform.position - surfaceRenderer.transform.position).normalized;
			return Vector3.Dot(surfaceRenderer.transform.forward, toCamera) < 0.1f;
		}

		float GetScreenSpaceCoverage() {
			if (!mainCamera || !surfaceRenderer) return 0f;

			// Use bounds to calculate screen coverage (simplified, no cylinder detection)
			Bounds bounds = surfaceRenderer.bounds;
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;

			// Calculate 8 corner points of bounding box
			_coveragePoints[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
			_coveragePoints[1] = center + new Vector3(-extents.x, -extents.y,  extents.z);
			_coveragePoints[2] = center + new Vector3(-extents.x,  extents.y, -extents.z);
			_coveragePoints[3] = center + new Vector3(-extents.x,  extents.y,  extents.z);
			_coveragePoints[4] = center + new Vector3( extents.x, -extents.y, -extents.z);
			_coveragePoints[5] = center + new Vector3( extents.x, -extents.y,  extents.z);
			_coveragePoints[6] = center + new Vector3( extents.x,  extents.y, -extents.z);
			_coveragePoints[7] = center + new Vector3( extents.x,  extents.y,  extents.z);

			// Find screen-space bounds
			_screenMin.x = float.MaxValue;
			_screenMin.y = float.MaxValue;
			_screenMax.x = float.MinValue;
			_screenMax.y = float.MinValue;

			int visiblePoints = 0;
			for (int i = 0; i < 8; i++) {
				Vector3 screenPoint = mainCamera.WorldToScreenPoint(_coveragePoints[i]);
				if (screenPoint.z <= 0) continue; // Behind camera

				visiblePoints++;
				if (screenPoint.x < _screenMin.x) _screenMin.x = screenPoint.x;
				if (screenPoint.y < _screenMin.y) _screenMin.y = screenPoint.y;
				if (screenPoint.x > _screenMax.x) _screenMax.x = screenPoint.x;
				if (screenPoint.y > _screenMax.y) _screenMax.y = screenPoint.y;
			}

			if (visiblePoints == 0) return 0f;

			float area = (_screenMax.x - _screenMin.x) * (_screenMax.y - _screenMin.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}

		#endregion
	}
}
