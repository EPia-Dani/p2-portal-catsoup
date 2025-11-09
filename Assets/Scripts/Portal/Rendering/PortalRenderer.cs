// PortalRenderer.cs - Simplified and refactored
// Handles portal rendering with proper viewport and projection

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
		[SerializeField] private int frameSkipInterval = 1;
		[SerializeField] private bool autoSizeToScreen = true;
		[SerializeField, Range(0.25f, 1.5f)] private float screenHeightFraction = 1f;
		[SerializeField, Range(0.25f, 1.5f)] private float screenWidthFraction = 1f;
		[SerializeField] private int minTextureSize = 256;
		[SerializeField] private int maxTextureSize = 2048;
		[SerializeField, HideInInspector] private Vector2Int manualTextureSize = new Vector2Int(1024, 1024);

		[Header("Adaptive Recursion")]
		[SerializeField] private bool enableAdaptiveRecursion = true;
		[SerializeField] private bool useFrameSkipping = false;
		[SerializeField] private bool adaptiveResolution = true;
		[SerializeField, Range(0.1f, 1f)] private float perLevelResolutionScale = 0.6f;
		[SerializeField, Range(0.1f, 1f)] private float minResolutionScale = 0.25f;
		[SerializeField] private int fullResolutionLevels = 1;

		/// <summary>
		/// Exposed so PortalSlot can adjust portal scale for travel/render cohesion.
		/// </summary>
		public float PortalScale { get; set; } = 1f;

		private PortalRenderTextureController _textureController;
		private PortalVisibilityCuller _visibilityCuller;
		private PortalViewChain _viewChain;

		private Material _surfaceMaterial;
		private Matrix4x4[] _viewMatrices = Array.Empty<Matrix4x4>();
		private Vector2Int _currentTextureSize = Vector2Int.zero;
		private LevelRenderState[] _levelStates = Array.Empty<LevelRenderState>();
		private struct LevelRenderState {
			public int lastRenderedFrame;
			public RenderTexture cachedTexture;
			public float resolutionScale;
		}

		private bool _visible = true;
		public bool IsReadyToRender { get; set; }

		public Transform PortalTransform => transform;

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

		void ReleaseLevelTexture(ref LevelRenderState state) {
			if (state.cachedTexture) {
				state.cachedTexture.Release();
				Destroy(state.cachedTexture);
				state.cachedTexture = null;
			}

			state.lastRenderedFrame = -1;
			state.resolutionScale = 1f;
		}

		void ReleaseLevelTextures(int startIndex = 0) {
			if (_levelStates == null) return;

			for (int i = startIndex; i < _levelStates.Length; i++) {
				ReleaseLevelTexture(ref _levelStates[i]);
			}
		}

		void EnsureCapacity(int limit) {
			int length = Mathf.Max(1, limit);
			if (_viewMatrices.Length < length) {
				_viewMatrices = new Matrix4x4[length];
			}
			if (_levelStates.Length < length) {
				if (_levelStates.Length > 0 && _levelStates.Length < length) {
					Array.Resize(ref _levelStates, length);
				} else {
					_levelStates = new LevelRenderState[length];
				}
			} else if (_levelStates.Length > length) {
				for (int i = length; i < _levelStates.Length; i++) {
					ReleaseLevelTexture(ref _levelStates[i]);
				}
				Array.Resize(ref _levelStates, length);
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

		bool UpdateTextureResolution(bool force) {
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

			return true;
		}

		Vector2Int DetermineTextureSize() {
			int minSize = minTextureSize < 16 ? 16 : minTextureSize;
			int maxSize = maxTextureSize < minSize ? minSize : maxTextureSize;

			if (!autoSizeToScreen) {
				return new Vector2Int(
					manualTextureSize.x < minSize ? minSize : (manualTextureSize.x > maxSize ? maxSize : manualTextureSize.x),
					manualTextureSize.y < minSize ? minSize : (manualTextureSize.y > maxSize ? maxSize : manualTextureSize.y)
				);
			}

			int screenWidth = Screen.width;
			int screenHeight = Screen.height;
			if (screenWidth < 1) screenWidth = 1;
			if (screenHeight < 1) screenHeight = 1;

			if (!Application.isPlaying && screenWidth <= 1 && screenHeight <= 1) {
				return new Vector2Int(
					manualTextureSize.x < minSize ? minSize : (manualTextureSize.x > maxSize ? maxSize : manualTextureSize.x),
					manualTextureSize.y < minSize ? minSize : (manualTextureSize.y > maxSize ? maxSize : manualTextureSize.y)
				);
			}

			float heightFrac = screenHeightFraction < 0.1f ? 0.1f : (screenHeightFraction > 2f ? 2f : screenHeightFraction);
			float widthFrac = screenWidthFraction < 0.1f ? 0.1f : (screenWidthFraction > 2f ? 2f : screenWidthFraction);

			int targetHeight = Mathf.RoundToInt(screenHeight * heightFrac);
			if (targetHeight < minSize) targetHeight = minSize;
			else if (targetHeight > maxSize) targetHeight = maxSize;

			float aspect = EstimatePortalAspect();
			int aspectWidth = Mathf.RoundToInt(targetHeight * aspect);
			int widthLimit = Mathf.RoundToInt(screenWidth * widthFrac);
			if (widthLimit < minSize) widthLimit = minSize;
			else if (widthLimit > maxSize) widthLimit = maxSize;

			int targetWidth = aspectWidth > 0 && aspectWidth < widthLimit ? aspectWidth : widthLimit;
			if (targetWidth < minSize) targetWidth = minSize;
			else if (targetWidth > maxSize) targetWidth = maxSize;

			return new Vector2Int(targetWidth, targetHeight);
		}

		float EstimatePortalAspect() {
			if (!surfaceRenderer) return 1f;
			Vector3 size = surfaceRenderer.bounds.size;
			float height = size.y < 0.001f ? 0.001f : size.y;
			float width = size.x < 0.001f ? 0.001f : size.x;
			float aspect = width / height;
			return aspect < 0.2f ? 0.2f : (aspect > 4f ? 4f : aspect);
		}

		public void ConfigurePortal(int width, int height, int limit, int skipInterval) {
			recursionLimit = Mathf.Max(1, limit);
			EnsureCapacity(recursionLimit);

			frameSkipInterval = Mathf.Max(1, skipInterval);

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
			ReleaseLevelTextures();
		}

		void OnValidate() {
			recursionLimit = Mathf.Max(1, recursionLimit);
			frameSkipInterval = Mathf.Max(1, frameSkipInterval);
			minTextureSize = Mathf.Max(16, minTextureSize);
			maxTextureSize = Mathf.Max(minTextureSize, maxTextureSize);
			screenHeightFraction = Mathf.Clamp(screenHeightFraction, 0.1f, 2f);
			screenWidthFraction = Mathf.Clamp(screenWidthFraction, 0.1f, 2f);
			manualTextureSize = new Vector2Int(
				Mathf.Clamp(manualTextureSize.x, minTextureSize, maxTextureSize),
				Mathf.Clamp(manualTextureSize.y, minTextureSize, maxTextureSize)
			);

			if (!autoSizeToScreen && Application.isPlaying && _currentTextureSize != Vector2Int.zero) {
				manualTextureSize = new Vector2Int(
					Mathf.Clamp(_currentTextureSize.x, minTextureSize, maxTextureSize),
					Mathf.Clamp(_currentTextureSize.y, minTextureSize, maxTextureSize)
				);
			}

			if (Application.isPlaying) {
				EnsureCapacity(recursionLimit);
				ConfigureRenderTargets(true);
			}
		}

		void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
			if (camera != mainCamera || !pair) return;
			if (!_visible || !IsReadyToRender || !pair._visible || !pair.IsReadyToRender) return;
			
			if (useFrameSkipping) {
				int interval = frameSkipInterval < 1 ? 1 : frameSkipInterval;
				if ((Time.frameCount % interval) != 0) return;
			}

			if (_visibilityCuller != null && _visibilityCuller.ShouldCull(mainCamera, surfaceRenderer)) return;

			Render(context);
		}

		void Render(ScriptableRenderContext context) {
			if (_viewMatrices.Length == 0 || !pair || _viewChain == null) return;

			if (!enableAdaptiveRecursion) {
				ReleaseLevelTextures(1);
			}

			int effectiveLimit = CalculateEffectiveRecursionLimit();
			int levelCount = _viewChain.BuildViewChain(mainCamera, this, pair, effectiveLimit, _viewMatrices);
			if (levelCount == 0) return;

			if (levelCount > 2) {
				levelCount = TrimInvisibleLevels(levelCount);
				if (levelCount == 0) return;
			}

			RenderTexture baseTexture = _textureController?.Texture;
			if (baseTexture == null || portalCamera == null) return;

			// Cache transform properties and frame count
			Transform pairTransform = pair.transform;
			Vector3 exitPos = pairTransform.position;
			Vector3 exitFwd = pairTransform.forward;
			int currentFrame = Time.frameCount;
			int baseWidth = baseTexture.width;
			int baseHeight = baseTexture.height;

			portalCamera.targetTexture = baseTexture;
			portalCamera.pixelRect = new Rect(0, 0, baseWidth, baseHeight);

			for (int i = levelCount - 1; i >= 0; i--) {
				ref LevelRenderState state = ref _levelStates[i];
				RenderTexture targetTexture = AcquireTargetTextureForLevel(i, baseTexture);
				
				if (i == 0 || ShouldRenderLevel(i)) {
					if (RenderLevel(context, _viewMatrices[i], exitPos, exitFwd, targetTexture)) {
						state.lastRenderedFrame = currentFrame;
						if (targetTexture != baseTexture) {
							Graphics.Blit(targetTexture, baseTexture);
						}
					}
				}
			}
		}

		int CalculateEffectiveRecursionLimit() {
			if (!pair) return recursionLimit;
			// Cache forward vectors to avoid repeated property access
			Vector3 thisForward = transform.forward;
			Vector3 pairForward = pair.transform.forward;
			float dot = Vector3.Dot(thisForward, pairForward);
			if (dot > 0.9f) return 1;
			if (dot > -0.2f && dot < 0.2f) return recursionLimit < 2 ? recursionLimit : 2;
			return recursionLimit;
		}

		int TrimInvisibleLevels(int levelCount) {
			if (!portalCamera || levelCount <= 1) return levelCount;

			for (int i = 0; i < levelCount - 1; i++) {
				PortalRenderer targetPortal = (i & 1) == 0 ? this : pair; // Bitwise check faster than modulo
				if (!targetPortal) continue;

				var targetRenderer = targetPortal.surfaceRenderer;
				if (!targetRenderer) continue;

				if (!IsPortalVisibleFromLevel(_viewMatrices[i], targetRenderer)) {
					return i + 1;
				}
			}

			return levelCount;
		}

		bool IsPortalVisibleFromLevel(Matrix4x4 worldMatrix, MeshRenderer targetRenderer) {
			if (!portalCamera || targetRenderer == null) return true;

			Vector3 position = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);

			if (forward.sqrMagnitude < 1e-4f) return false;

			// Fast distance and angle check - avoid sqrt and normalization
			Bounds targetBounds = targetRenderer.bounds;
			Vector3 toPortal = targetBounds.center - position;
			float distanceSq = toPortal.sqrMagnitude;
			
			if (distanceSq > 400f) return false; // Too far

			// Angle check without sqrt/normalize - use dot product directly
			float forwardDot = Vector3.Dot(forward, toPortal);
			if (forwardDot < 0f || forwardDot * forwardDot < distanceSq * 0.01f) { // cos(angle) < 0.1
				return false;
			}

			return true;
		}

		bool RenderLevel(ScriptableRenderContext context, Matrix4x4 worldMatrix, Vector3 exitPos, Vector3 exitForward, RenderTexture targetTexture) {
			if (!portalCamera || !_visible || !targetTexture) return false;

			Vector3 cameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 cameraForward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 cameraUp = worldMatrix.MultiplyVector(Vector3.up);

			if (cameraForward.sqrMagnitude < 1e-4f) return false;

			RenderTexture originalTarget = portalCamera.targetTexture;
			Rect originalRect = portalCamera.pixelRect;
			Transform camTransform = portalCamera.transform;

			camTransform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(cameraForward, cameraUp));
			portalCamera.targetTexture = targetTexture;
			int texWidth = targetTexture.width;
			int texHeight = targetTexture.height;
			portalCamera.pixelRect = new Rect(0, 0, texWidth, texHeight);

			Vector3 planePoint = exitPos + exitForward * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 clipNormal = -w2c.MultiplyVector(exitForward);
			float clipNormalMagSq = clipNormal.sqrMagnitude;
			if (clipNormalMagSq > 1e-8f) {
				float clipNormalMag = Mathf.Sqrt(clipNormalMagSq);
				clipNormal /= clipNormalMag;
				Vector3 clipPoint = w2c.MultiplyPoint(planePoint);
				Vector4 clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
				portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
			}

			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
			portalCamera.targetTexture = originalTarget;
			if (originalTarget) {
				portalCamera.pixelRect = originalRect;
			}

			return true;
		}

		bool ShouldRenderLevel(int levelIndex) {
			if (_levelStates == null || levelIndex < 0 || levelIndex >= _levelStates.Length) return true;
			if (!enableAdaptiveRecursion) return true;
			if (!useFrameSkipping) return true;

			int interval = Mathf.Max(1, frameSkipInterval);
			int lastFrame = _levelStates[levelIndex].lastRenderedFrame;
			if (lastFrame < 0) return true;

			return (Time.frameCount - lastFrame) >= interval;
		}

		RenderTexture AcquireTargetTextureForLevel(int levelIndex, RenderTexture baseTexture) {
			if (levelIndex <= 0) return baseTexture;
			if (!enableAdaptiveRecursion || !adaptiveResolution) {
				if (_levelStates != null && levelIndex < _levelStates.Length) {
					ref LevelRenderState state = ref _levelStates[levelIndex];
					if (state.cachedTexture) {
						ReleaseLevelTexture(ref state);
					}
				}
				return baseTexture;
			}

			if (_textureController == null || baseTexture == null) return baseTexture;
			if (_levelStates == null || levelIndex >= _levelStates.Length) return baseTexture;

			ref LevelRenderState levelState = ref _levelStates[levelIndex];

			float scale = ComputeResolutionScale(levelIndex);
			if (scale >= 0.999f) {
				if (levelState.cachedTexture) {
					ReleaseLevelTexture(ref levelState);
				}
				return baseTexture;
			}

			// Cache base texture dimensions to avoid repeated property access
			int baseWidth = baseTexture.width;
			int baseHeight = baseTexture.height;
			int width = Mathf.Max(1, Mathf.RoundToInt(baseWidth * scale));
			int height = Mathf.Max(1, Mathf.RoundToInt(baseHeight * scale));

			if (levelState.cachedTexture &&
			    (levelState.cachedTexture.width != width ||
			     levelState.cachedTexture.height != height ||
			     Mathf.Abs(levelState.resolutionScale - scale) > 0.001f)) {
				ReleaseLevelTexture(ref levelState);
			}

			if (!levelState.cachedTexture) {
				var descriptor = _textureController.CreateDescriptor(width, height);
				var renderTexture = new RenderTexture(descriptor);
				_textureController.ApplySettings(renderTexture);
				renderTexture.Create();
				levelState.cachedTexture = renderTexture;
			}

			levelState.resolutionScale = scale;
			return levelState.cachedTexture;
		}

		float ComputeResolutionScale(int levelIndex) {
			if (!enableAdaptiveRecursion || !adaptiveResolution) return 1f;
			if (levelIndex < fullResolutionLevels) return 1f;

			int depth = levelIndex - fullResolutionLevels + 1;
			if (depth <= 0) return 1f;

			// Avoid expensive Mathf.Pow - use iterative multiplication
			float scale = 1f;
			for (int i = 0; i < depth && scale > minResolutionScale; i++) {
				scale *= perLevelResolutionScale;
			}
			
			return scale < minResolutionScale ? minResolutionScale : scale;
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

