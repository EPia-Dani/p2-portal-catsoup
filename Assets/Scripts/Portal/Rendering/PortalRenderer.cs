// PortalRenderer.cs - Simple portal rendering
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	[RequireComponent(typeof(PortalRenderTextureController))]
	[RequireComponent(typeof(PortalViewChain))]
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;

		[Header("References")]
		[SerializeField] private Camera mainCamera;
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;

		[Header("Settings")]
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int textureSize = 1024;
		[SerializeField] private Color portalColor = Color.cyan;

		public float PortalScale { get; set; } = 1f;
		public bool IsReadyToRender { get; set; }

		private PortalRenderTextureController _textureController;
		private PortalVisibilityCuller _visibilityCuller;
		private PortalViewChain _viewChain;
		private Material _surfaceMaterial;
		private Matrix4x4[] _viewMatrices = Array.Empty<Matrix4x4>();
		private bool _visible = true;
		private readonly Plane[] _frustumPlanes = new Plane[6];

		void Awake() {
			// Get references
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);
			_textureController = GetComponent<PortalRenderTextureController>();
			_viewChain = GetComponent<PortalViewChain>();
			_visibilityCuller = GetComponent<PortalVisibilityCuller>();

			// Setup
			if (surfaceRenderer) _surfaceMaterial = surfaceRenderer.material;
			SetupCamera();
			SetupTexture();
			_viewMatrices = new Matrix4x4[recursionLimit];
			_visible = surfaceRenderer && surfaceRenderer.enabled;
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
		}

		void SetupTexture() {
			if (_textureController == null) return;
			_textureController.Configure(textureSize, textureSize);
			_textureController.BindCamera(portalCamera);
			if (_surfaceMaterial) _textureController.BindMaterial(_surfaceMaterial);
		}

		public void ConfigurePortal(int width, int height, int limit, int skipInterval) {
			recursionLimit = Mathf.Max(1, limit);
			textureSize = Mathf.Clamp(width, 256, 4096);
			if (_viewMatrices.Length != recursionLimit) {
				_viewMatrices = new Matrix4x4[recursionLimit];
			}
			SetupTexture();
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
			textureSize = Mathf.Clamp(textureSize, 256, 4096);
			if (Application.isPlaying && _viewMatrices != null) {
				if (_viewMatrices.Length != recursionLimit) {
					_viewMatrices = new Matrix4x4[recursionLimit];
				}
				SetupTexture();
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
			if (!pair || _viewChain == null || _textureController == null) return;
			RenderTexture texture = _textureController.Texture;
			if (texture == null || portalCamera == null) return;

			// Build view chain
			int effectiveLimit = recursionLimit;
			if (Mathf.Abs(PortalScale - pair.PortalScale) > 0.001f) {
				effectiveLimit = 1; // No recursion when scales differ
			}

			int levelCount = _viewChain.BuildViewChain(mainCamera, this, pair, effectiveLimit, _viewMatrices);
			if (levelCount == 0) return;

			// Simple culling - reduce levels if portals face same direction
			levelCount = SimplifyRecursion(levelCount);

			// Clear and render
			_textureController.Clear(portalColor);
			portalCamera.targetTexture = texture;
			portalCamera.pixelRect = new Rect(0, 0, texture.width, texture.height);

			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;
			
			// Cache portal bounds for frustum culling
			MeshRenderer pairRenderer = pair.GetComponentInChildren<MeshRenderer>(true);
			MeshRenderer thisRenderer = surfaceRenderer;
			Bounds pairBounds = pairRenderer ? pairRenderer.bounds : new Bounds(pair.transform.position, Vector3.one * 2f);
			Bounds thisBounds = thisRenderer ? thisRenderer.bounds : new Bounds(transform.position, Vector3.one * 2f);

			// Render all recursion levels
			// The top-level visibility culler already checks if THIS portal is visible to the main camera
			// So we render all levels - deeper levels will naturally be culled if portals aren't visible
			for (int i = levelCount - 1; i >= 0; i--) {
				RenderLevel(context, _viewMatrices[i], exitPos, exitFwd, texture);
			}
		}

		int SimplifyRecursion(int levelCount) {
			if (levelCount <= 1 || !pair) return levelCount;
			
			// Simple check: if portals face same direction, reduce recursion
			float dot = Vector3.Dot(transform.forward, pair.transform.forward);
			if (dot > 0.8f) {
				return 1; // Same direction - no recursion needed
			}
			
			return levelCount;
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
			if (!handler && collider) {
				handler = gameObject.AddComponent<PortalTravellerHandler>();
			}
			if (handler) {
				handler.wallCollider = collider;
			}
		}
	}
}
