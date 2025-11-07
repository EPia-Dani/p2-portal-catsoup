// PortalRenderer.cs - Simplified and refactored
// Handles portal rendering with proper viewport and projection

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;

		[Header("Scene Refs")]
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

		// Portal scale for size-based rendering
		public float PortalScale { get; set; } = 1f;

		private RenderTexture _rt;
		private Material _mat;
		private Matrix4x4[] _recursionMatrices;
		private Plane[] _frustumPlanes = new Plane[6];
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1, 1, -1));
		
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

			if (surfaceRenderer && !_mat) {
				_mat = surfaceRenderer.material;
			}

			SetupCamera();
			AllocRT();

			recursionLimit = Mathf.Max(1, recursionLimit);
			_recursionMatrices = new Matrix4x4[recursionLimit];

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

		void AllocRT() {
			if (_rt) {
				_rt.Release();
				Destroy(_rt);
			}

			_rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_rt.Create();

			if (portalCamera) {
				portalCamera.targetTexture = _rt;
				portalCamera.pixelRect = new Rect(0, 0, textureWidth, textureHeight);
			}
			
			if (_mat) {
				_mat.mainTexture = _rt;
			}
		}

		public void ConfigurePortal(int w, int h, int recLimit, int frameskip) {
			if (textureWidth != w || textureHeight != h) {
				textureWidth = w;
				textureHeight = h;
				AllocRT();
			}

			recursionLimit = Mathf.Max(1, recLimit);
			if (_recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}
			
			frameSkipInterval = Mathf.Max(1, frameskip);
		}

		void OnEnable() {
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		void OnDisable() {
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		void OnDestroy() {
			if (_rt) {
				_rt.Release();
				Destroy(_rt);
			}
			if (_mat) Destroy(_mat);
		}

		void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) {
			if (cam != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;
			if (!pair) return;
			if (!_visible || !IsReadyToRender) return;
			if (!pair._visible || !pair.IsReadyToRender) return;

			if (enableCulling && ShouldCull()) return;

			Render(ctx);
		}

		bool ShouldCull() {
			if (!mainCamera || !surfaceRenderer) return true;
			
			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);
			if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, surfaceRenderer.bounds)) return true;
			
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

			// Sample 8 corners of bounds
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

		void Render(ScriptableRenderContext ctx) {
			if (!pair || !portalCamera) return;

			ClearTexture();

			// Calculate portal transformation step matrix (rotation/translation only, no scaling)
			Matrix4x4 portalStep = CalculatePortalStepMatrix();
			
			// Calculate scale ratio for size-based rendering
			float scaleRatio = pair.PortalScale / PortalScale;
			
			// Start with main camera's transform
			Matrix4x4 initialCameraMatrix = mainCamera.transform.localToWorldMatrix;
			
			// Build recursion matrices normally first
			Matrix4x4 currentMatrix = initialCameraMatrix;
			for (int i = 0; i < _recursionMatrices.Length; i++) {
				currentMatrix = portalStep * currentMatrix;
				_recursionMatrices[i] = currentMatrix;
			}
			
			// Now scale positions if portals have different sizes
			// This happens AFTER building the matrices so rotations are correct
			// Track which portal we're at for each recursion level
			PortalRenderer currentPortal = this;
			PortalRenderer nextPortal = pair;
			
			for (int i = 0; i < _recursionMatrices.Length; i++) {
				float currentScaleRatio = nextPortal.PortalScale / currentPortal.PortalScale;
				
				if (Mathf.Abs(currentScaleRatio - 1f) > 0.001f) {
					// Extract position and rotation
					Vector3 pos = _recursionMatrices[i].GetColumn(3);
					Quaternion rot = _recursionMatrices[i].rotation;
					
					// Get offset from current destination portal center
					Vector3 offset = pos - nextPortal.transform.position;
					
					// Scale offset in destination portal's local space
					Vector3 offsetLocal = nextPortal.transform.InverseTransformDirection(offset);
					Vector3 scaledOffsetLocal = offsetLocal * currentScaleRatio;
					Vector3 scaledOffsetWorld = nextPortal.transform.TransformDirection(scaledOffsetLocal);
					
					// Rebuild with scaled position, same rotation
					_recursionMatrices[i] = Matrix4x4.TRS(
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

			// Determine max recursion level
			int maxLevel = GetMaxRecursionLevel();
			int startLevel = Mathf.Min(maxLevel, _recursionMatrices.Length - 1);

			// Render from deepest to shallowest
			// Track which portal we're looking through at each level
			PortalRenderer[] exitPortals = new PortalRenderer[_recursionMatrices.Length];
			PortalRenderer currentExit = pair;
			for (int i = 0; i < exitPortals.Length; i++) {
				exitPortals[i] = currentExit;
				// Alternate between portals
				currentExit = (currentExit == pair) ? this : pair;
			}
			
			for (int i = startLevel; i >= 0; i--) {
				PortalRenderer exitPortal = exitPortals[i];
				RenderLevel(ctx, _recursionMatrices[i], exitPortal.transform.position, exitPortal.transform.forward);
			}
		}

		Matrix4x4 CalculatePortalStepMatrix() {
			// Transform from this portal's space to pair's space with mirror
			// This handles rotation and translation only - scaling is handled separately for positions
			return pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix;
		}

		int GetMaxRecursionLevel() {
			if (!pair) return recursionLimit - 1;
			
			// Limit recursion for vertical portals
			bool thisVertical = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) > 0.9f;
			bool pairVertical = Mathf.Abs(Vector3.Dot(pair.transform.forward, Vector3.up)) > 0.9f;
			if (thisVertical || pairVertical) {
				return Mathf.Min(2, recursionLimit - 1);
			}

			// Adjust based on portal alignment
			float dot = Mathf.Clamp(Vector3.Dot(transform.forward, pair.transform.forward), -1f, 1f);
			float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
			
			if (angle < 45f) return 0;
			if (angle < 135f) return Mathf.Min(1, recursionLimit - 1);
			return recursionLimit - 1;
		}

		void RenderLevel(ScriptableRenderContext ctx, Matrix4x4 worldMatrix, Vector3 exitPos, Vector3 exitForward) {
			if (!portalCamera || !_visible) return;

			// Extract position and rotation from matrix
			Vector3 cameraPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 cameraForward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 cameraUp = worldMatrix.MultiplyVector(Vector3.up);

			// Validate camera position (prevent NaN/Infinity)
			if (!IsValidVector3(cameraPos) || !IsValidVector3(cameraForward) || !IsValidVector3(cameraUp)) {
				return;
			}

			// Set camera transform
			portalCamera.transform.SetPositionAndRotation(cameraPos, Quaternion.LookRotation(cameraForward, cameraUp));

			// Ensure pixelRect matches render texture exactly
			if (portalCamera.targetTexture != null) {
				portalCamera.pixelRect = new Rect(0, 0, portalCamera.targetTexture.width, portalCamera.targetTexture.height);
			}

			// Calculate oblique clipping plane
			Vector3 planePoint = exitPos + exitForward * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 normal = -w2c.MultiplyVector(exitForward).normalized;
			Vector3 planePointCam = w2c.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(planePointCam, normal));

			// Apply oblique projection
			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);
			
			// Render
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			
			// Reset projection matrix
			portalCamera.ResetProjectionMatrix();
		}

		bool IsValidVector3(Vector3 v) {
			return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
			       !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
		}

		void ClearTexture() {
			if (_rt == null) return;
			var prev = RenderTexture.active;
			RenderTexture.active = _rt;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
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

		public void SetWallCollider(Collider col) {
			var handler = GetComponent<PortalTravellerHandler>();
			if (handler) {
				handler.wallCollider = col;
			} else {
				// Auto-add handler if it doesn't exist
				handler = gameObject.AddComponent<PortalTravellerHandler>();
				handler.wallCollider = col;
			}
		}
	}
}
