using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;
		[SerializeField] private float frustumCullMargin = 3.0f;

		private RenderTexture _renderTexture;
		private Material _portalMaterial;
		private Matrix4x4[] _recursionMatrices = Array.Empty<Matrix4x4>();
		private Plane[] _frustumPlanes = new Plane[6];
		private readonly Matrix4x4 _mirrorMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
		private bool _isVisible = true;
		private bool _isReadyToRender;

		public bool IsReadyToRender {
			get => _isReadyToRender;
			set => _isReadyToRender = value;
		}

		private void Awake() {
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>();

			SetupCamera();
			CreateRenderTexture();

			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}

			_isVisible = surfaceRenderer != null && surfaceRenderer.enabled;
		}

		private void OnDestroy() {
			if (_renderTexture != null) {
				_renderTexture.Release();
				Destroy(_renderTexture);
			}
			if (_portalMaterial != null) {
				Destroy(_portalMaterial);
			}
		}

		private void SetupCamera() {
			if (portalCamera == null) return;
			portalCamera.enabled = false;
			portalCamera.forceIntoRenderTexture = true;
			portalCamera.allowHDR = false;
			portalCamera.clearFlags = CameraClearFlags.Skybox;

			var extra = portalCamera.GetUniversalAdditionalCameraData();
			if (extra != null) {
				extra.renderPostProcessing = false;
				extra.antialiasing = AntialiasingMode.None;
				extra.requiresColorOption = CameraOverrideOption.On;
				extra.requiresDepthOption = CameraOverrideOption.On;
				extra.SetRenderer(0);
			}
		}

		private void CreateRenderTexture() {
			if (_renderTexture != null) {
				_renderTexture.Release();
				Destroy(_renderTexture);
			}

			_renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_renderTexture.Create();

			if (portalCamera) portalCamera.targetTexture = _renderTexture;
			if (surfaceRenderer) {
				_portalMaterial = surfaceRenderer.material;
				_portalMaterial.mainTexture = _renderTexture;
			}
		}

		public void ConfigurePortal(int width, int height, int recLimit, int frameskip) {
			if (textureWidth != width || textureHeight != height) {
				textureWidth = width;
				textureHeight = height;
				CreateRenderTexture();
			}
			SetRecursionLimit(recLimit);
			SetFrameSkipInterval(frameskip);
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

	private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
	private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

	public void SetVisible(bool visible) {
		if (_isVisible == visible) return;
		_isVisible = visible;
		if (portalCamera) portalCamera.gameObject.SetActive(visible);
		if (surfaceRenderer) surfaceRenderer.enabled = visible;
		if (!visible) {
			_isReadyToRender = false;
			ClearTexture();
		}
	}

	private void OnBeginCameraRendering(ScriptableRenderContext context, Camera currentCamera) {
		if (currentCamera != mainCamera) return;
		if ((Time.frameCount % frameSkipInterval) != 0) return;
		if (!pair) return;
		if (!_isVisible || !_isReadyToRender) return;
		if (!pair._isVisible || !pair._isReadyToRender) return;

		// Optional: keep a cheap front-face check for main camera. Remove if you want zero gating.
		if (!MainCameraCanSeeThis()) return;

		RenderPortal(context);
	}

		// Cheap main-camera gate. Remove this method call above if you want literally zero culling.
		private bool MainCameraCanSeeThis() {
			if (!mainCamera || !surfaceRenderer) return false;

			// Frustum check
			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) return false;

			// Face the camera
			Vector3 toCamera = (mainCamera.transform.position - surfaceRenderer.transform.position).normalized;
			return Vector3.Dot(surfaceRenderer.transform.forward, toCamera) < 0.1f;
		}

		// Check if the pair portal is visible through the portal camera at a given recursion level
		private bool IsPortalVisibleThroughCamera(int recursionLevel) {
			if (!portalCamera || !pair || !pair.surfaceRenderer) return false;

			// Calculate the portal camera's position and orientation for this recursion level
			Matrix4x4 worldMatrix = _recursionMatrices[recursionLevel];
			Vector3 position = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);

			// Temporarily set the portal camera to this recursion level's position
			Vector3 originalPos = portalCamera.transform.position;
			Quaternion originalRot = portalCamera.transform.rotation;
			portalCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

			// Check if pair portal is in the frustum with tunable margin
			GeometryUtility.CalculateFrustumPlanes(portalCamera, _frustumPlanes);
			
			// Expand bounds by tunable margin to prevent edge culling
			Bounds expandedBounds = pair.surfaceRenderer.bounds;
			expandedBounds.Expand(frustumCullMargin);
			
			bool isVisible = GeometryUtility.TestPlanesAABB(_frustumPlanes, expandedBounds);

			// Restore original camera position
			portalCamera.transform.SetPositionAndRotation(originalPos, originalRot);

			return isVisible;
		}

		private void RenderPortal(ScriptableRenderContext context) {
			if (!mainCamera || !pair || !portalCamera) return;

			ClearTexture();

			// Build all recursion transforms
			Matrix4x4 stepMatrix = pair.transform.localToWorldMatrix * _mirrorMatrix * transform.worldToLocalMatrix;
			Matrix4x4 current = mainCamera.transform.localToWorldMatrix;
			for (int i = 0; i < _recursionMatrices.Length; i++) {
				current = stepMatrix * current;
				_recursionMatrices[i] = current;
			}

			Vector3 destinationForward = pair.transform.forward;
			Vector3 destinationPosition = pair.transform.position;

			// Cull recursion levels based on portal orientation to each other
			int maxRenderLevel = GetMaxRecursionLevelForPair();
			int startLevel = Mathf.Min(maxRenderLevel, _recursionMatrices.Length - 1);
			
			for (int i = startLevel; i >= 0; i--) {
				// Check if the pair portal is visible through the portal camera at this recursion level
				if (!IsPortalVisibleThroughCamera(i)) continue;
				
				RenderLevel(mainCamera, _recursionMatrices[i], destinationForward, destinationPosition);
			}
		}

		private int GetMaxRecursionLevelForPair() {
			if (!pair) return recursionLimit - 1;

			// Check if either portal is vertical (floor/ceiling mounted)
			bool thisIsVertical = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) > 0.9f;
			bool pairIsVertical = Mathf.Abs(Vector3.Dot(pair.transform.forward, Vector3.up)) > 0.9f;

			if (thisIsVertical || pairIsVertical) {
				// If either portal is on the floor/ceiling, allow 2 recursion levels
				return 2;
			}

			// Calculate the angle between this portal's normal and the pair's normal
			float dotProduct = Vector3.Dot(transform.forward, pair.transform.forward);
			float angle = Mathf.Acos(Mathf.Clamp(dotProduct, -1f, 1f)) * Mathf.Rad2Deg;

			// angle ≈ 0°: same direction → no recursion
			// angle ≈ 90°: perpendicular → 1 level recursion
			// angle ≈ 180°: facing each other → full recursion

			if (angle < 45f) {
				// 0° difference: skip recursion entirely
				return 0;
			} else if (angle < 135f) {
				// 90° difference (45° to 135° range): 1 level recursion
				return 1;
			}

			// 180° difference (facing each other): allow full recursion
			return recursionLimit - 1;
		}

		private void RenderLevel(Camera mainCam, Matrix4x4 worldMatrix, Vector3 destinationForward, Vector3 destinationPosition) {
			if (!portalCamera || !_isVisible) return;

			// Position and orientation
			Vector3 position = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			portalCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

			// Oblique clip plane to clip at destination portal plane
			Vector3 planePoint = destinationPosition + destinationForward * 0.001f;
			Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
			Vector3 cameraPlaneNormal = worldToCamera.MultiplyVector(destinationForward);
			cameraPlaneNormal = Vector3.Normalize(-cameraPlaneNormal);

			Vector3 cameraPlanePoint = worldToCamera.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(
				cameraPlaneNormal.x, cameraPlaneNormal.y, cameraPlaneNormal.z,
				-Vector3.Dot(cameraPlanePoint, cameraPlaneNormal)
			);

			portalCamera.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
		}

		private void ClearTexture() {
			var prev = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
		}
	}
}

