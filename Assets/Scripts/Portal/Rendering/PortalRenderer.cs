// PortalRenderer.cs - Simplified and refactored
// Handles portal rendering with proper viewport and projection

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
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

		[Header("Culling")]
		[SerializeField] private bool enableCulling = false;
		[SerializeField] private float minScreenCoverage = 0.01f;

		/// <summary>
		/// Exposed so PortalSlot can adjust portal scale for travel/render cohesion.
		/// </summary>
		public float PortalScale { get; set; } = 1f;

		private RenderTexture _renderTexture;
		private Material _surfaceMaterial;
		private Matrix4x4[] _viewMatrices = Array.Empty<Matrix4x4>();
		private readonly Plane[] _frustumPlanes = new Plane[6];
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));

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

			if (surfaceRenderer && !_surfaceMaterial) {
				_surfaceMaterial = surfaceRenderer.material;
			}

			SetupCamera();
			AllocateRenderTexture();
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

			if (_renderTexture) {
				portalCamera.targetTexture = _renderTexture;
				portalCamera.pixelRect = new Rect(0, 0, _renderTexture.width, _renderTexture.height);
			}
		}

		void AllocateRenderTexture() {
			if (_renderTexture) {
				_renderTexture.Release();
				Destroy(_renderTexture);
			}

			_renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_renderTexture.Create();

			if (portalCamera) {
				portalCamera.targetTexture = _renderTexture;
				portalCamera.pixelRect = new Rect(0, 0, textureWidth, textureHeight);
			}

			if (_surfaceMaterial) {
				_surfaceMaterial.mainTexture = _renderTexture;
			}
		}

		public void ConfigurePortal(int width, int height, int limit, int skipInterval) {
			if (textureWidth != width || textureHeight != height) {
				textureWidth = width;
				textureHeight = height;
				AllocateRenderTexture();
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
			if (_renderTexture) {
				_renderTexture.Release();
				Destroy(_renderTexture);
			}

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

			if (enableCulling && ShouldCull()) return;

			Render(context);
		}

		bool ShouldCull() {
			if (!mainCamera || !surfaceRenderer) return true;

			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) {
				return true;
			}

			if (minScreenCoverage > 0f) {
				float coverage = GetScreenSpaceCoverage();
				if (coverage < minScreenCoverage) return true;
			}

			return false;
		}

		float GetScreenSpaceCoverage() {
			if (!mainCamera || !surfaceRenderer) return 0f;

			var bounds = surfaceRenderer.bounds;
			Vector3 min = new Vector3(float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue);
			int visiblePoints = 0;

			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			Vector3[] corners = {
				center + new Vector3(-extents.x, -extents.y, -extents.z),
				center + new Vector3(-extents.x, -extents.y,  extents.z),
				center + new Vector3(-extents.x,  extents.y, -extents.z),
				center + new Vector3(-extents.x,  extents.y,  extents.z),
				center + new Vector3( extents.x, -extents.y, -extents.z),
				center + new Vector3( extents.x, -extents.y,  extents.z),
				center + new Vector3( extents.x,  extents.y, -extents.z),
				center + new Vector3( extents.x,  extents.y,  extents.z)
			};

			foreach (var corner in corners) {
				Vector3 screenPos = mainCamera.WorldToScreenPoint(corner);
				if (screenPos.z <= 0) continue;

				visiblePoints++;
				if (screenPos.x < min.x) min.x = screenPos.x;
				if (screenPos.y < min.y) min.y = screenPos.y;
				if (screenPos.x > max.x) max.x = screenPos.x;
				if (screenPos.y > max.y) max.y = screenPos.y;
			}

			if (visiblePoints == 0) return 0f;

			float area = (max.x - min.x) * (max.y - min.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}

		void Render(ScriptableRenderContext context) {
			if (_viewMatrices.Length == 0 || !pair) return;

			ClearTexture();

			// Build recursion matrices with step transformation
			Matrix4x4 step = pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix;
			Matrix4x4 cur = mainCamera.transform.localToWorldMatrix;
			
			// Track which portal we're at for scale adjustments
			PortalRenderer currentPortal = this;
			PortalRenderer nextPortal = pair;
			
			// Build recursion matrices by repeatedly applying the step transformation
			for (int i = 0; i < _viewMatrices.Length; i++) {
				cur = step * cur;
				_viewMatrices[i] = cur;
				
				// Apply scale adjustment if portals have different sizes
				float scaleRatio = nextPortal.PortalScale / currentPortal.PortalScale;
				if (Mathf.Abs(scaleRatio - 1f) > 0.001f) {
					// Extract position and rotation
					Vector3 pos = _viewMatrices[i].GetColumn(3);
					Quaternion rot = _viewMatrices[i].rotation;
					
					// Get offset from destination portal center
					Vector3 offset = pos - nextPortal.transform.position;
					
					// Scale offset in destination portal's local space
					Vector3 offsetLocal = nextPortal.transform.InverseTransformDirection(offset);
					Vector3 scaledOffsetLocal = offsetLocal * scaleRatio;
					Vector3 scaledOffsetWorld = nextPortal.transform.TransformDirection(scaledOffsetLocal);
					
					// Rebuild with scaled position, same rotation
					_viewMatrices[i] = Matrix4x4.TRS(
						nextPortal.transform.position + scaledOffsetWorld,
						rot,
						Vector3.one
					);
				}
				
				// Swap portals for next iteration
				PortalRenderer temp = currentPortal;
				currentPortal = nextPortal;
				nextPortal = temp;
			}

			// Always use pair portal for exit position/forward (like the old working code)
			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;

			int maxLevel = GetMaxRecursionLevel();
			int startLevel = Mathf.Min(maxLevel, _viewMatrices.Length - 1);

			// Render from deepest to shallowest
			for (int i = startLevel; i >= 0; i--) {
				RenderLevel(context, _viewMatrices[i], exitPos, exitFwd);
			}
		}

		int GetMaxRecursionLevel() {
			if (!pair) return recursionLimit - 1;

			// Always respect the configured recursion limit
			// The old optimization logic was too aggressive and prevented recursion from showing
			// Users can control recursion via PortalManager/UI settings
			return recursionLimit - 1;
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

		void ClearTexture() {
			if (!_renderTexture) return;
			var previous = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = previous;
		}

		public void SetVisible(bool visible) {
			_visible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			if (!visible) {
				IsReadyToRender = false;
				ClearTexture();
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
