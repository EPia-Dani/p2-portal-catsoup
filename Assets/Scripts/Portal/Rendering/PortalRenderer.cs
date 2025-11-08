// PortalRenderer.cs
// Main orchestrator for portal rendering - coordinates all rendering components

using System;
using UnityEngine;
using UnityEngine.Rendering;

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

		[Header("Culling")]
		[SerializeField] private bool enableAdvancedCulling = true;
		[SerializeField] private bool gateByMainCamera = false;
		[SerializeField] private bool gateByScreenCoverage = false;
		[SerializeField] private float minScreenCoverageFraction = 0.01f;
		
		[Header("Advanced Culling Settings")]
		[Tooltip("Maximum distance for full recursion (beyond this, recursion is reduced)")]
		[SerializeField] private float maxDistanceForFullRecursion = 20f;
		[Tooltip("Maximum distance for any recursion (beyond this, no recursion)")]
		[SerializeField] private float maxDistanceForAnyRecursion = 100f;
		[Tooltip("Minimum screen coverage to render recursion")]
		[SerializeField] private float minCoverageForRecursion = 0.001f;

		[Header("Wall Collider")]
		[Tooltip("Wall collider to disable for player when entering portal")]
		[SerializeField] private Collider wallCollider;

		// Portal scale for size-based teleportation
		public float PortalScale { get; set; } = 1f;

		// Component references
		private PortalRenderTexture _renderTexture;
		private PortalRenderView _renderView;
		private Material _portalMaterial;

		// Rendering state
		private bool _visible = true;
		private bool _ready = false;

		// Recursion matrices (cached to avoid allocations)
		private Matrix4x4[] _recursionMatrices = Array.Empty<Matrix4x4>();
		private int _lastMaxRecursionLevel;

		public bool IsReadyToRender {
			get => _ready;
			set => _ready = value;
		}

		public Transform PortalTransform => transform;

		void Awake() {
			InitializeReferences();
			InitializeComponents();
		}

		void OnDestroy() {
			_renderTexture?.Release();
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

		void InitializeComponents() {
			// Initialize render texture
			_renderTexture = new PortalRenderTexture(textureWidth, textureHeight, _portalMaterial, portalCamera);

			// Initialize render view
			_renderView = new PortalRenderView(portalCamera, mainCamera);

			// Initialize recursion matrices
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
				_renderTexture?.SetSize(width, height);
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
			_renderView?.SetVisible(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			
			if (!visible) {
				_ready = false;
				_renderTexture?.Clear();
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
			if (gateByMainCamera && !PortalVisibility.IsVisibleToCamera(mainCamera, surfaceRenderer)) return;
			
			float coverage = PortalVisibility.GetScreenSpaceCoverage(mainCamera, surfaceRenderer);
			if (gateByScreenCoverage && coverage < minScreenCoverageFraction) return;

			// Advanced culling: calculate adaptive recursion level
			if (enableAdvancedCulling) {
				// Early exit if portal pair shouldn't render
				if (!PortalAdvancedCulling.ShouldRenderPortalPair(
					mainCamera, 
					transform, 
					pair.transform, 
					surfaceRenderer,
					maxDistanceForAnyRecursion,
					minCoverageForRecursion)) {
					return;
				}

				// Calculate adaptive recursion level based on distance, coverage, and visibility
				_lastMaxRecursionLevel = PortalAdvancedCulling.CalculateAdaptiveRecursionLevel(
					mainCamera,
					transform,
					pair.transform,
					surfaceRenderer,
					recursionLimit,
					maxDistanceForFullRecursion,
					maxDistanceForAnyRecursion,
					minCoverageForRecursion);
				
				// Additional per-level visibility check for even better optimization
				int visibleLevels = PortalAdvancedCulling.CalculateVisibleRecursionLevels(
					mainCamera,
					transform,
					pair.transform,
					surfaceRenderer,
					recursionLimit,
					minCoverageForRecursion);
				
				// Use the more conservative (lower) of the two calculations
				_lastMaxRecursionLevel = Mathf.Min(_lastMaxRecursionLevel, visibleLevels);
			} else {
				// Fallback to basic recursion calculation
				_lastMaxRecursionLevel = PortalRecursionSolver.CalculateMaxRecursionLevel(transform, pair.transform, recursionLimit);
			}
			
			// Early exit if no recursion needed
			if (_lastMaxRecursionLevel < 0) return;
			
			// Adjust render texture size if needed
			_renderTexture?.UpdateDynamicSize(mainCamera, coverage, _lastMaxRecursionLevel);

			// Render portal view
			RenderPortalView(ctx, _lastMaxRecursionLevel);
		}

		void RenderPortalView(ScriptableRenderContext ctx, int maxRecursionLevel) {
			_renderTexture?.Clear();

			// Early exit if no recursion needed
			if (maxRecursionLevel < 0) return;

			// Build recursion matrices
			Matrix4x4 portalTransform = PortalRecursionSolver.BuildPortalTransform(transform, pair.transform);
			PortalRecursionSolver.BuildRecursionMatrices(
				portalTransform,
				mainCamera.transform.localToWorldMatrix,
				_recursionMatrices,
				recursionLimit);

			// Render each recursion level (from deepest to shallowest)
			Vector3 exitPos = pair.transform.position;
			Vector3 exitForward = pair.transform.forward;
			int startLevel = Mathf.Min(maxRecursionLevel, _recursionMatrices.Length - 1);
			
			// Cache coverage for per-level checks
			float baseCoverage = enableAdvancedCulling 
				? PortalVisibility.GetScreenSpaceCoverage(mainCamera, surfaceRenderer) 
				: 1f;

			for (int i = startLevel; i >= 0; i--) {
				// Per-level visibility check (only if advanced culling is enabled)
				if (enableAdvancedCulling && baseCoverage > 0) {
					if (!PortalAdvancedCulling.IsRecursionLevelVisible(
						mainCamera, 
						transform, 
						pair.transform, 
						i, 
						baseCoverage,
						minCoverageForRecursion)) {
						// Skip this level and all shallower levels
						break;
					}
				}

				_renderView?.RenderLevel(_recursionMatrices[i], exitPos, exitForward, _renderTexture?.Texture);
			}
		}

		#endregion
	}
}
