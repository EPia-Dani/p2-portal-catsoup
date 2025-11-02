using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	[RequireComponent(typeof(PortalAnimator))]
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		private const float PLANE_OFFSET = 0.001f;
		private const float FACE_THRESHOLD = 0.1f;

		private PortalAnimator _animator;
		private RenderTexture _renderTexture;
		private Material _portalMaterial;
		private Matrix4x4[] _recursionMatrices;
		private Plane[] _frustumPlanes = new Plane[6];
		private readonly Matrix4x4 _mirrorMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
		private bool _isVisible = true;

		private Matrix4x4 _cachedStepMatrix;
		private bool _stepMatrixDirty = true;
		private int _cachedMaxRecursionLevel = -1;
		private UniversalRenderPipeline.SingleCameraRequest _cachedRequest;
		private bool _textureCleared = false;

		private void Awake() {
			_animator = GetComponent<PortalAnimator>();
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>();

			SetupCamera();
			CreateRenderTexture();

			if (_animator != null && surfaceRenderer != null) {
				_animator.Configure(surfaceRenderer);
			}

			recursionLimit = Mathf.Max(1, recursionLimit);
			_recursionMatrices = new Matrix4x4[recursionLimit];
			_cachedRequest = new UniversalRenderPipeline.SingleCameraRequest();

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
			if (_recursionMatrices == null || _recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}
			_stepMatrixDirty = true;
			_cachedMaxRecursionLevel = -1;
		}

		public void SetFrameSkipInterval(int interval) { frameSkipInterval = Mathf.Max(1, interval); }

		public bool IsReadyToRender {
			get => _animator != null && (_animator.IsOpening || _animator.IsFullyOpen);
			set { }
		}

		private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

		private void UpdateStepMatrix() {
			if (!_stepMatrixDirty || !pair) return;
			_cachedStepMatrix = pair.transform.localToWorldMatrix * _mirrorMatrix * transform.worldToLocalMatrix;
			_stepMatrixDirty = false;
		}

		public void StartOpening() {
			if (!_isVisible) SetVisible(true);
			_animator?.StartOpening();
		}

		public void PlayAppear() {
			if (!_isVisible) SetVisible(true);
			_animator?.PlayAppear();
		}

		public void SetVisible(bool visible) {
			if (_isVisible == visible) return;
			_isVisible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			if (!visible) {
				_animator?.HideImmediate();
				ClearTexture();
			} else {
				_stepMatrixDirty = true;
				_cachedMaxRecursionLevel = -1;
			}
		}

		private void OnBeginCameraRendering(ScriptableRenderContext context, Camera currentCamera) {
			if (currentCamera != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;
			if (!pair || !_animator || !pair._animator) return;
			if (!_isVisible) return;

			// Only the main camera gates rendering based on portal openness.
			bool thisReady = _animator.IsOpening || _animator.IsFullyOpen;
			bool pairReady = pair._animator.IsOpening || pair._animator.IsFullyOpen;
			if (!thisReady || !pairReady) return;

			// Optional: keep a cheap front-face check for main camera. Remove if you want zero gating.
			if (!MainCameraCanSeeThis()) return;

			RenderPortal(context);
		}

		private bool MainCameraCanSeeThis() {
			if (!mainCamera || !surfaceRenderer) return false;

			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) return false;

			Vector3 toCamera = (mainCamera.transform.position - surfaceRenderer.transform.position).normalized;
			return Vector3.Dot(surfaceRenderer.transform.forward, toCamera) < FACE_THRESHOLD;
		}

		private void RenderPortal(ScriptableRenderContext context) {
			if (!mainCamera || !pair || !portalCamera) return;

			UpdateStepMatrix();

			Matrix4x4 current = mainCamera.transform.localToWorldMatrix;
			for (int i = 0; i < _recursionMatrices.Length; i++) {
				current = _cachedStepMatrix * current;
				_recursionMatrices[i] = current;
			}

			int maxRenderLevel = GetMaxRecursionLevelForPair();
			int startLevel = Mathf.Min(maxRenderLevel, _recursionMatrices.Length - 1);

			if (!_textureCleared) {
				ClearTexture();
			}

			Vector3 destinationForward = pair.transform.forward;
			Vector3 destinationPosition = pair.transform.position;

			for (int i = startLevel; i >= 0; i--) {
				RenderLevel(mainCamera, _recursionMatrices[i], destinationForward, destinationPosition);
			}

			_textureCleared = false;
		}

		private int GetMaxRecursionLevelForPair() {
			if (_cachedMaxRecursionLevel >= 0) return _cachedMaxRecursionLevel;

			bool thisIsVertical = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) > 0.9f;
			bool pairIsVertical = Mathf.Abs(Vector3.Dot(pair.transform.forward, Vector3.up)) > 0.9f;

			if (thisIsVertical || pairIsVertical) {
				_cachedMaxRecursionLevel = 2;
				return 2;
			}

			float dotProduct = Vector3.Dot(transform.forward, pair.transform.forward);
			float angle = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

			if (angle < 45f) {
				_cachedMaxRecursionLevel = 0;
				return 0;
			}
			else if (angle < 135f) {
				_cachedMaxRecursionLevel = 1;
				return 1;
			}

			_cachedMaxRecursionLevel = recursionLimit - 1;
			return _cachedMaxRecursionLevel;
		}

		private void RenderLevel(
			Camera mainCam,
			Matrix4x4 worldMatrix,
			Vector3 destinationForward,
			Vector3 destinationPosition) {
			if (!portalCamera || !_isVisible) return;

			Vector3 position = new Vector3(worldMatrix.m03, worldMatrix.m13, worldMatrix.m23);
			Vector3 forward = new Vector3(worldMatrix.m02, worldMatrix.m12, worldMatrix.m22);
			Vector3 up = new Vector3(worldMatrix.m01, worldMatrix.m11, worldMatrix.m21);
			portalCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

			Vector3 planePoint = destinationPosition + destinationForward * PLANE_OFFSET;
			Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
			Vector3 cameraPlaneNormal = -worldToCamera.MultiplyVector(destinationForward);
			cameraPlaneNormal = Vector3.Normalize(cameraPlaneNormal);

			Vector3 cameraPlanePoint = worldToCamera.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(
				cameraPlaneNormal.x, cameraPlaneNormal.y, cameraPlaneNormal.z,
				-(cameraPlanePoint.x * cameraPlaneNormal.x + cameraPlanePoint.y * cameraPlaneNormal.y +
				  cameraPlanePoint.z * cameraPlaneNormal.z)
			);

			portalCamera.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(portalCamera, _cachedRequest);
			portalCamera.ResetProjectionMatrix();
		}

		private void ClearTexture() {
			var prev = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
			_textureCleared = true;
		}
	}
}