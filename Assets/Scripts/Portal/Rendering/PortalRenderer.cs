// PortalRenderer.cs - Clean and simple portal rendering
// Handles portal rendering with recursion support

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	[RequireComponent(typeof(PortalRenderTextureController))]
	[RequireComponent(typeof(PortalViewChain))]
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;

		[Header("Scene References")]
		[SerializeField] private Camera mainCamera;
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;

		[Header("Render Settings")]
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private bool autoSizeToScreen = true;
		[SerializeField, Range(0.5f, 2f)] private float screenHeightFraction = 1f;
		[SerializeField] private int minTextureSize = 256;
		[SerializeField] private int maxTextureSize = 4096; // Higher max for 4K+ support
		[SerializeField, HideInInspector] private Vector2Int manualTextureSize = new Vector2Int(1024, 1024);

		public float PortalScale { get; set; } = 1f;
		public bool IsReadyToRender { get; set; }
		public Transform PortalTransform => transform;

		private PortalRenderTextureController _textureController;
		private PortalVisibilityCuller _visibilityCuller;
		private PortalViewChain _viewChain;
		private Material _surfaceMaterial;
		private Matrix4x4[] _viewMatrices = Array.Empty<Matrix4x4>();
		private Vector2Int _currentTextureSize = Vector2Int.zero;
		private bool _visible = true;

		void Awake() {
			Initialize();
		}

		void Initialize() {
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			_textureController = GetComponent<PortalRenderTextureController>();
			_viewChain = GetComponent<PortalViewChain>();
			_visibilityCuller = GetComponent<PortalVisibilityCuller>();

			if (surfaceRenderer && !_surfaceMaterial) {
				_surfaceMaterial = surfaceRenderer.material;
			}

			SetupCamera();
			ConfigureRenderTargets(true);
			EnsureCapacity(recursionLimit);

			_visible = surfaceRenderer && surfaceRenderer.enabled;
		}

		void EnsureCapacity(int limit) {
			int length = Mathf.Max(1, limit);
			if (_viewMatrices.Length != length) {
				_viewMatrices = new Matrix4x4[length];
			}
		}

		void SetupCamera() {
			if (!portalCamera) return;

			portalCamera.enabled = false;
			portalCamera.forceIntoRenderTexture = true;
			portalCamera.allowHDR = false;
			portalCamera.clearFlags = CameraClearFlags.SolidColor;
			portalCamera.backgroundColor = Color.black;
			portalCamera.useOcclusionCulling = false;

			var extra = portalCamera.GetUniversalAdditionalCameraData();
			if (extra != null) {
				extra.renderPostProcessing = false;
				extra.antialiasing = AntialiasingMode.None;
				extra.requiresColorOption = CameraOverrideOption.On;
				extra.requiresDepthOption = CameraOverrideOption.On;
				extra.SetRenderer(0);
			}

			_textureController?.BindCamera(portalCamera);
		}

		void ConfigureRenderTargets(bool force = false) {
			if (_textureController == null) return;

			if (UpdateTextureResolution(force) == false) {
				_textureController.BindCamera(portalCamera);
				if (_surfaceMaterial) {
					_textureController.BindMaterial(_surfaceMaterial);
				}
			}
		}

		public bool UpdateTextureResolution(bool force) {
			if (_textureController == null) return false;

			Vector2Int desiredSize = DetermineTextureSize();
			if (desiredSize.x <= 0 || desiredSize.y <= 0) return false;

			if (!force && desiredSize == _currentTextureSize && _textureController.Texture != null) {
				return false;
			}

			_currentTextureSize = desiredSize;
			_textureController.Configure(desiredSize.x, desiredSize.y);
			_textureController.BindCamera(portalCamera);
			if (_surfaceMaterial) {
				_textureController.BindMaterial(_surfaceMaterial);
			}

			// Debug: Log texture resolution
			if (force && Application.isPlaying) {
				Debug.Log($"PortalRenderer [{gameObject.name}]: Texture resolution set to {desiredSize.x}x{desiredSize.y} (Screen: {Screen.width}x{Screen.height}, Fraction: {screenHeightFraction}, Texture: {(_textureController.Texture != null ? $"{_textureController.Texture.width}x{_textureController.Texture.height}" : "NULL")})");
			}

			return true;
		}

		Vector2Int DetermineTextureSize() {
			int minSize = Mathf.Max(16, minTextureSize);
			int maxSize = Mathf.Max(minSize, maxTextureSize);

			if (!autoSizeToScreen) {
				int manualWidth = Mathf.Clamp(manualTextureSize.x, minSize, maxSize);
				int manualHeight = Mathf.Clamp(manualTextureSize.y, minSize, maxSize);
				return new Vector2Int(manualWidth, manualHeight);
			}

			int screenWidth = Mathf.Max(1, Screen.width);
			int screenHeight = Mathf.Max(1, Screen.height);
			if (!Application.isPlaying && (screenWidth <= 1 || screenHeight <= 1)) {
				int fallbackWidth = Mathf.Clamp(manualTextureSize.x, minSize, maxSize);
				int fallbackHeight = Mathf.Clamp(manualTextureSize.y, minSize, maxSize);
				return new Vector2Int(fallbackWidth, fallbackHeight);
			}

			// Use full screen resolution (or fraction if specified)
			float heightFraction = Mathf.Clamp(screenHeightFraction, 0.5f, 2f);
			int targetHeight = Mathf.RoundToInt(screenHeight * heightFraction);
			// Only clamp to max, not min - allow full resolution
			targetHeight = Mathf.Min(targetHeight, maxTextureSize);
			targetHeight = Mathf.Max(targetHeight, minSize); // Ensure minimum quality

			float aspect = EstimatePortalAspect();
			int targetWidth = Mathf.RoundToInt(targetHeight * aspect);
			targetWidth = Mathf.Min(targetWidth, maxTextureSize);
			targetWidth = Mathf.Max(targetWidth, minSize);

			return new Vector2Int(targetWidth, targetHeight);
		}

		float EstimatePortalAspect() {
			if (!surfaceRenderer) return 1f;
			Bounds bounds = surfaceRenderer.bounds;
			float height = Mathf.Max(0.001f, bounds.size.y);
			float width = Mathf.Max(0.001f, bounds.size.x);
			return Mathf.Clamp(width / height, 0.2f, 4f);
		}

		public void ConfigurePortal(int width, int height, int limit, int skipInterval) {
			recursionLimit = Mathf.Max(1, limit);
			EnsureCapacity(recursionLimit);

			if (!autoSizeToScreen) {
				int minSize = Mathf.Max(16, minTextureSize);
				int maxSize = Mathf.Max(minSize, maxTextureSize);
				manualTextureSize = new Vector2Int(
					Mathf.Clamp(width, minSize, maxSize),
					Mathf.Clamp(height, minSize, maxSize)
				);
			}

			ConfigureRenderTargets(true);
		}

		void OnEnable() {
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		void OnDisable() {
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		void OnDestroy() {
			if (_surfaceMaterial) {
				Destroy(_surfaceMaterial);
			}
		}

		void OnValidate() {
			recursionLimit = Mathf.Max(1, recursionLimit);
			minTextureSize = Mathf.Max(16, minTextureSize);
			maxTextureSize = Mathf.Max(minTextureSize, maxTextureSize);
			screenHeightFraction = Mathf.Clamp(screenHeightFraction, 0.1f, 2f);
			manualTextureSize = new Vector2Int(
				Mathf.Clamp(manualTextureSize.x, minTextureSize, maxTextureSize),
				Mathf.Clamp(manualTextureSize.y, minTextureSize, maxTextureSize)
			);

			if (Application.isPlaying) {
				EnsureCapacity(recursionLimit);
				ConfigureRenderTargets(true);
			}
		}

		void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
			if (camera != mainCamera) return;
			if (!pair) return;
			if (!_visible || !IsReadyToRender) return;
			if (!pair._visible || !pair.IsReadyToRender) return;

			if (_visibilityCuller != null && _visibilityCuller.ShouldCull(mainCamera, surfaceRenderer)) return;

			Render(context);
		}

		void Render(ScriptableRenderContext context) {
			if (_viewMatrices.Length == 0 || !pair || _viewChain == null) return;
			if (_textureController == null || _textureController.Texture == null || portalCamera == null) return;

			// Ensure texture resolution is up to date for this portal
			UpdateTextureResolution(false);

			// Ensure pair portal also has texture configured with same resolution
			if (pair) {
				pair.UpdateTextureResolution(false);
			}

			// Build view chain
			int levelCount = _viewChain.BuildViewChain(mainCamera, this, pair, recursionLimit, _viewMatrices);
			if (levelCount == 0) return;

			// Cull invisible recursion levels
			levelCount = CullInvisibleLevels(levelCount);
			if (levelCount == 0) return;

			// Setup render target
			RenderTexture baseTexture = _textureController.Texture;
			if (baseTexture == null) return;
			
			portalCamera.targetTexture = baseTexture;
			portalCamera.pixelRect = new Rect(0, 0, baseTexture.width, baseTexture.height);

			// Render each recursion level
			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;

			for (int i = levelCount - 1; i >= 0; i--) {
				RenderLevel(context, _viewMatrices[i], exitPos, exitFwd, baseTexture);
			}
		}

		int CullInvisibleLevels(int levelCount) {
			if (!portalCamera || levelCount <= 1) return levelCount;
			if (!pair) return levelCount;

			// Calculate angle between portal normals
			Vector3 thisNormal = transform.forward;
			Vector3 pairNormal = pair.transform.forward;
			float dot = Vector3.Dot(thisNormal, pairNormal);
			
			// Limit recursion based on portal angle
			// dot = 1 → 0° (same direction) → no recursion
			// dot = 0 → 90° (perpendicular) → max 1 recursion
			// dot = -1 → 180° (opposite) → full recursion
			int maxAllowedLevels = levelCount;
			
			if (dot > 0.9f) {
				// 0° (same direction/complanar) - no recursion
				maxAllowedLevels = 1;
			}
			else if (Mathf.Abs(dot) < 0.1f) {
				// 90° (perpendicular) - maximum 1 recursion
				maxAllowedLevels = Mathf.Min(2, levelCount);
			}
			else if (dot < -0.9f) {
				// 180° (opposite directions) - full recursion
				maxAllowedLevels = levelCount;
			}
			else {
				// Interpolate between thresholds
				if (dot > 0f) {
					// Between 0° and 90°: interpolate from no recursion (dot=0.9) to 1 recursion (dot=0.1)
					float t = (0.9f - dot) / (0.9f - 0.1f); // Map from [0.1, 0.9] to [1, 0]
					t = Mathf.Clamp01(t);
					maxAllowedLevels = Mathf.RoundToInt(Mathf.Lerp(1, 2, t));
				}
				else {
					// Between 90° and 180°: interpolate from 1 recursion (dot=-0.1) to full recursion (dot=-0.9)
					float t = (-dot - 0.1f) / (0.9f - 0.1f); // Map from [0.1, 0.9] to [0, 1]
					t = Mathf.Clamp01(t);
					maxAllowedLevels = Mathf.RoundToInt(Mathf.Lerp(2, levelCount, t));
				}
			}

			// Apply visibility culling for allowed levels
			// For each level, check if we can see the next portal before rendering it
			for (int i = 0; i < maxAllowedLevels - 1; i++) {
				PortalRenderer targetPortal = (i % 2 == 0) ? this : pair;
				if (!targetPortal || !targetPortal.surfaceRenderer) {
					return i + 1; // Can't continue if portal is missing
				}

				// Check if the next portal (at level i+1) is visible from level i
				if (!IsPortalVisibleFromLevel(_viewMatrices[i], targetPortal.surfaceRenderer)) {
					return i + 1; // Stop at first invisible level
				}
			}

			// All checked levels are visible, return the angle-limited count
			return maxAllowedLevels;
		}

		bool IsPortalVisibleFromLevel(Matrix4x4 worldMatrix, MeshRenderer targetRenderer) {
			if (!portalCamera || targetRenderer == null) return true;

			Vector3 position = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);

			if (!IsValidVector3(position) || !IsValidVector3(forward)) {
				return false;
			}

			if (forward.sqrMagnitude < 1e-4f) return false;

			Bounds targetBounds = targetRenderer.bounds;
			Vector3 toPortal = targetBounds.center - position;
			float distanceSq = toPortal.sqrMagnitude;

			// Distance check - skip if too far
			if (distanceSq > 400f) { // 20 units squared
				return false;
			}

			// Simple angle check - is portal roughly in front of camera?
			float distance = Mathf.Sqrt(distanceSq);
			if (distance > 0.01f) {
				Vector3 dirToPortal = toPortal / distance;
				float dot = Vector3.Dot(forward.normalized, dirToPortal);
				// More strict culling - only allow portals that are clearly in front (within ~45 degrees)
				if (dot < 0.7f) { // cos(45°) ≈ 0.707, so we only allow portals within 45° of forward
					return false;
				}
			}

			// For deeper recursion levels, use simpler checks
			// Only do expensive frustum check for first few levels
			return true;
		}

		bool RenderLevel(ScriptableRenderContext context, Matrix4x4 worldMatrix, Vector3 exitPos, Vector3 exitForward, RenderTexture targetTexture) {
			if (!portalCamera || !_visible || !targetTexture) return false;

			// Extract camera transform from matrix
			Vector3 cameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 cameraForward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 cameraUp = worldMatrix.MultiplyVector(Vector3.up);

			if (!IsValidVector3(cameraPos) || !IsValidVector3(cameraForward) || !IsValidVector3(cameraUp)) {
				return false;
			}

			// Save original camera state
			RenderTexture originalTarget = portalCamera.targetTexture;
			Rect originalRect = portalCamera.pixelRect;

			// Set camera transform
			portalCamera.transform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(cameraForward, cameraUp));
			portalCamera.targetTexture = targetTexture;
			portalCamera.pixelRect = new Rect(0, 0, targetTexture.width, targetTexture.height);

			// Calculate oblique projection matrix for portal clipping
			Vector3 planePoint = exitPos + exitForward * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 clipNormal = -w2c.MultiplyVector(exitForward).normalized;
			Vector3 clipPoint = w2c.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));

			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();

			// Restore original camera state
			portalCamera.targetTexture = originalTarget;
			if (originalTarget) {
				portalCamera.pixelRect = originalRect;
			}

			return true;
		}

		bool IsValidVector3(Vector3 value) {
			return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
			       !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
		}

		public void SetVisible(bool visible) {
			_visible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			if (!visible) {
				IsReadyToRender = false;
				_textureController?.Clear(Color.clear);
			}
		}

		public void SetWallCollider(Collider collider) {
			var handler = GetComponent<PortalTravellerHandler>();
			if (handler) {
				handler.wallCollider = collider;
			} else if (collider) {
				handler = gameObject.AddComponent<PortalTravellerHandler>();
				handler.wallCollider = collider;
			}
		}
	}
}
