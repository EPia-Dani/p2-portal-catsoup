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
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		/// <summary>
		/// Exposed so PortalSlot can adjust portal scale for travel/render cohesion.
		/// </summary>
		public float PortalScale { get; set; } = 1f;

		private PortalRenderTextureController _textureController;
		private PortalVisibilityCuller _visibilityCuller;
		private PortalViewChain _viewChain;

		private Material _surfaceMaterial;
		private Matrix4x4[] _viewMatrices = Array.Empty<Matrix4x4>();

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
			ConfigureRenderTargets();
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

		void ConfigureRenderTargets() {
			if (_textureController == null) return;

			_textureController.Configure(textureWidth, textureHeight);
			_textureController.BindCamera(portalCamera);
			if (_surfaceMaterial) {
				_textureController.BindMaterial(_surfaceMaterial);
			}
		}

		public void ConfigurePortal(int width, int height, int limit, int skipInterval) {
			if (textureWidth != width || textureHeight != height) {
				textureWidth = width;
				textureHeight = height;
				ConfigureRenderTargets();
			}

			recursionLimit = Mathf.Max(1, limit);
			EnsureCapacity(recursionLimit);

			frameSkipInterval = Mathf.Max(1, skipInterval);
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

		void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
			if (camera != mainCamera) return;
			if (!pair) return;
			if (!_visible || !IsReadyToRender) return;
			if (!pair._visible || !pair.IsReadyToRender) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;

			if (_visibilityCuller != null && _visibilityCuller.ShouldCull(mainCamera, surfaceRenderer)) return;

			Render(context);
		}

		void Render(ScriptableRenderContext context) {
			if (_viewMatrices.Length == 0 || !pair || _viewChain == null) return;

			_textureController?.Clear(Color.clear);

			// Calculate dynamic recursion limit based on portal orientation
			int effectiveLimit = CalculateEffectiveRecursionLimit();
			int levelCount = _viewChain.BuildViewChain(mainCamera, this, pair, effectiveLimit, _viewMatrices);
			if (levelCount == 0) return;

			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;

			for (int i = levelCount - 1; i >= 0; i--) {
				RenderLevel(context, _viewMatrices[i], exitPos, exitFwd);
			}
		}

		int CalculateEffectiveRecursionLimit() {
			if (!pair) return recursionLimit;

			// Calculate angle between portal forward vectors
			Vector3 thisForward = transform.forward;
			Vector3 pairForward = pair.transform.forward;
			float dot = Vector3.Dot(thisForward, pairForward);

			// Portals facing same direction (dot ≈ 1, 0°) - won't see each other, only render first level
			// Using threshold to account for floating point precision
			if (dot > 0.9f) {
				return 1;
			}

			// Portals at 90° (dot ≈ 0) - render one more time (2 levels total)
			// Using threshold around 0
			if (dot > -0.2f && dot < 0.2f) {
				return 2;
			}

			// Portals facing each other (dot ≈ -1, 180°) and other angles - use configured recursion limit
			return recursionLimit;
		}

		void RenderLevel(ScriptableRenderContext context, Matrix4x4 worldMatrix, Vector3 exitPos, Vector3 exitForward) {
			if (!portalCamera || !_visible) return;

			Vector3 cameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 cameraForward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 cameraUp = worldMatrix.MultiplyVector(Vector3.up);

			if (!IsValidVector3(cameraPos) || !IsValidVector3(cameraForward) || !IsValidVector3(cameraUp)) {
				return;
			}

			portalCamera.transform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(cameraForward, cameraUp));

			if (portalCamera.targetTexture != null) {
				portalCamera.pixelRect = new Rect(0, 0, portalCamera.targetTexture.width, portalCamera.targetTexture.height);
			}

			Vector3 planePoint = exitPos + exitForward * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 clipNormal = -w2c.MultiplyVector(exitForward).normalized;
			Vector3 clipPoint = w2c.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));

			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
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

