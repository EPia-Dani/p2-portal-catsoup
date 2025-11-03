// PortalRenderer.cs  (URP recursion + oblique clip, gates optional and OFF by default)
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

		[Header("Render")]
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		[Header("Cull Gates")]
		[SerializeField] private bool gateByMainCamera = false;        // was true; default off
		[SerializeField] private bool gateByScreenCoverage = false;     // was true; default off
		[SerializeField] private float minScreenCoverageFraction = 0.01f;
		[SerializeField] private bool gateByThroughCamera = true;       // keep this, cheap cull
		[SerializeField] private float frustumCullMargin = 3.0f;

		private RenderTexture _rt;
		private Material _mat;
		private Matrix4x4[] _recursion = Array.Empty<Matrix4x4>();
		private Plane[] _frustum = new Plane[6];
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1, 1, -1));
		private bool _visible = true;
		private bool _ready;

		public bool IsReadyToRender { get => _ready; set => _ready = value; }

		void Awake() {
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			SetupCamera();
			AllocRT();

			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursion.Length != recursionLimit) _recursion = new Matrix4x4[recursionLimit];

			_visible = surfaceRenderer && surfaceRenderer.enabled;
		}

		void OnDestroy() {
			if (_rt) { _rt.Release(); Destroy(_rt); }
			if (_mat) Destroy(_mat);
		}

		void SetupCamera() {
			if (!portalCamera) return;
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

		void AllocRT() {
			if (_rt) { _rt.Release(); Destroy(_rt); }
			_rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_rt.Create();

			if (portalCamera) portalCamera.targetTexture = _rt;
			if (surfaceRenderer) {
				_mat = surfaceRenderer.material;
				_mat.mainTexture = _rt;
			}
		}

		public void ConfigurePortal(int w, int h, int recLimit, int frameskip) {
			if (textureWidth != w || textureHeight != h) { textureWidth = w; textureHeight = h; AllocRT(); }
			SetRecursionLimit(recLimit);
			SetFrameSkipInterval(frameskip);
		}

		public void SetRecursionLimit(int limit) {
			recursionLimit = Mathf.Max(1, limit);
			if (_recursion.Length != recursionLimit) _recursion = new Matrix4x4[recursionLimit];
		}

		public void SetFrameSkipInterval(int interval) {
			frameSkipInterval = Mathf.Max(1, interval);
		}

		void OnEnable()  => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

		public void SetVisible(bool visible) {
			_visible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			if (!visible) { _ready = false; ClearTexture(); }
		}

		void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) {
			if (cam != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;
			if (!pair) return;
			if (!_visible || !_ready) return;
			if (!pair._visible || !pair._ready) return;

			if (gateByMainCamera && !MainCameraCanSeeThis()) return;
			if (gateByScreenCoverage && GetScreenSpaceCoverage() < minScreenCoverageFraction) return;

			Render(ctx);
		}

		bool MainCameraCanSeeThis() {
			if (!mainCamera || !surfaceRenderer) return false;
			GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustum);
			if (!GeometryUtility.TestPlanesAABB(_frustum, surfaceRenderer.bounds)) return false;
			Vector3 toCam = (mainCamera.transform.position - surfaceRenderer.transform.position).normalized;
			return Vector3.Dot(surfaceRenderer.transform.forward, toCam) < 0.1f;
		}

		float GetScreenSpaceCoverage() {
			if (!mainCamera || !surfaceRenderer) return 0f;
			var b = surfaceRenderer.bounds;
			Vector3 c = b.center, e = b.extents;
			Vector3[] pts = {
				c + new Vector3(-e.x,-e.y,-e.z), c + new Vector3(-e.x,-e.y, e.z),
				c + new Vector3(-e.x, e.y,-e.z), c + new Vector3(-e.x, e.y, e.z),
				c + new Vector3( e.x,-e.y,-e.z), c + new Vector3( e.x,-e.y, e.z),
				c + new Vector3( e.x, e.y,-e.z), c + new Vector3( e.x, e.y, e.z),
			};
			Vector3 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
			int on = 0;
			for (int i = 0; i < 8; i++) {
				var sp = mainCamera.WorldToScreenPoint(pts[i]);
				if (sp.z <= 0) continue;
				on++;
				if (sp.x < min.x) min.x = sp.x; if (sp.y < min.y) min.y = sp.y;
				if (sp.x > max.x) max.x = sp.x; if (sp.y > max.y) max.y = sp.y;
			}
			if (on == 0) return 0f;
			float area = (max.x - min.x) * (max.y - min.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}

		void Render(ScriptableRenderContext ctx) {
			ClearTexture();

			Matrix4x4 step = pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix;
			Matrix4x4 cur  = mainCamera.transform.localToWorldMatrix;
			for (int i = 0; i < _recursion.Length; i++) { cur = step * cur; _recursion[i] = cur; }

			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;

			int maxLevel = GetMaxRecursionLevelForPair();
			int start = Mathf.Min(maxLevel, _recursion.Length - 1);

			for (int i = start; i >= 0; i--) {
				if (gateByThroughCamera && !IsPortalVisibleThroughCamera(i)) continue;
				RenderLevel(_recursion[i], exitPos, exitFwd);
			}
		}

		bool IsPortalVisibleThroughCamera(int level) {
			if (!portalCamera || !pair || !pair.surfaceRenderer) return false;

			Matrix4x4 world = _recursion[level];
			Vector3 pos = world.MultiplyPoint(Vector3.zero);
			Vector3 fwd = world.MultiplyVector(Vector3.forward);
			Vector3 up  = world.MultiplyVector(Vector3.up);

			Vector3 oPos = portalCamera.transform.position;
			Quaternion oRot = portalCamera.transform.rotation;
			portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

			GeometryUtility.CalculateFrustumPlanes(portalCamera, _frustum);
			Bounds expanded = pair.surfaceRenderer.bounds; expanded.Expand(frustumCullMargin);
			bool ok = GeometryUtility.TestPlanesAABB(_frustum, expanded);

			portalCamera.transform.SetPositionAndRotation(oPos, oRot);
			return ok;
		}

		int GetMaxRecursionLevelForPair() {
			if (!pair) return recursionLimit - 1;
			bool aVert = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up)) > 0.9f;
			bool bVert = Mathf.Abs(Vector3.Dot(pair.transform.forward, Vector3.up)) > 0.9f;
			if (aVert || bVert) return Mathf.Min(2, recursionLimit - 1);

			float dot = Mathf.Clamp(Vector3.Dot(transform.forward, pair.transform.forward), -1f, 1f);
			float ang = Mathf.Acos(dot) * Mathf.Rad2Deg;
			if (ang < 45f) return 0;
			if (ang < 135f) return Mathf.Min(1, recursionLimit - 1);
			return recursionLimit - 1;
		}

		void RenderLevel(Matrix4x4 world, Vector3 exitPos, Vector3 exitFwd) {
			if (!portalCamera || !_visible) return;

			Vector3 pos = world.MultiplyPoint(Vector3.zero);
			Vector3 fwd = world.MultiplyVector(Vector3.forward);
			Vector3 up  = world.MultiplyVector(Vector3.up);
			portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

			Vector3 planePoint = exitPos + exitFwd * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 nCam = Vector3.Normalize(-w2c.MultiplyVector(exitFwd));
			Vector3 pCam = w2c.MultiplyPoint(planePoint);
			Vector4 clip = new(nCam.x, nCam.y, nCam.z, -Vector3.Dot(pCam, nCam));

			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clip);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
		}

		void ClearTexture() {
			if (_rt == null) return;
			var prev = RenderTexture.active;
			RenderTexture.active = _rt;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
		}
	}
}
