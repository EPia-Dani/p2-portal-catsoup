// PortalRenderer.cs  (URP recursion + oblique clip, gates optional and OFF by default)

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;

		[Header("Scene Refs")] [SerializeField]
		private Camera mainCamera;

		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;

		[Header("Render")] [SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		[Header("Cull Gates")] [SerializeField]
		private bool gateByMainCamera = false; // was true; default off

		[SerializeField] private bool gateByScreenCoverage = false; // was true; default off
		[SerializeField] private float minScreenCoverageFraction = 0.01f;
		[SerializeField] private bool gateByThroughCamera = true; // keep this, cheap cull
		[SerializeField] private float frustumCullMargin = 3.0f;

		[Header("Physics Passthrough")]
		[SerializeField]
		[Tooltip("Wall collider to disable for player when entering portal")]
		private Collider wallCollider;

		[Header("Screen Thickness")]
		[SerializeField]
		[Tooltip("Also ensure screen thickness covers the wall depth to prevent a wall flash when crossing.")]
		private bool useWallThicknessForScreen = true;
		[SerializeField]
		[Tooltip("Extra padding added over computed thickness (meters)")]
		private float screenPadding = 0.01f;

		private RenderTexture _rt;
		private Material _mat;
		private Matrix4x4[] _recursion = Array.Empty<Matrix4x4>();
		private Plane[] _frustum = new Plane[6];
		private readonly Matrix4x4 _mirror = Matrix4x4.Scale(new Vector3(-1, 1, -1));
		private bool _visible = true;
		private bool _ready;
		public System.Collections.Generic.List<PortalTraveller> _trackedTravellers = new();

		public bool IsReadyToRender { get => _ready; set => _ready = value; }

		/// <summary>
		/// Public getter for this portal's transform (needed for teleportation).
		/// </summary>
		public Transform PortalTransform => transform;

		void Awake() {
			if (!mainCamera) mainCamera = Camera.main;
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			// Create material instance once to ensure each portal has its own
			if (surfaceRenderer && !_mat) {
				_mat = surfaceRenderer.material;
			}

			SetupCamera();
			AllocRT();

			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursion.Length != recursionLimit) _recursion = new Matrix4x4[recursionLimit];

			_visible = surfaceRenderer && surfaceRenderer.enabled;
		}

		void OnDestroy() {
			if (_rt) {
				_rt.Release();
				Destroy(_rt);
			}

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
			if (_rt) {
				_rt.Release();
				Destroy(_rt);
			}

			_rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_rt.Create();

			if (portalCamera) portalCamera.targetTexture = _rt;
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

			SetRecursionLimit(recLimit);
			SetFrameSkipInterval(frameskip);
		}

		public void SetRecursionLimit(int limit) {
			recursionLimit = Mathf.Max(1, limit);
			if (_recursion.Length != recursionLimit) _recursion = new Matrix4x4[recursionLimit];
		}

		public void SetFrameSkipInterval(int interval) { frameSkipInterval = Mathf.Max(1, interval); }

		void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

		void LateUpdate() { HandleTravellers(); }

		void HandleTravellers() {
			if (!pair) return;

			for (int i = 0; i < _trackedTravellers.Count; i++) {
				var t = _trackedTravellers[i];
				if (!t) {
					_trackedTravellers.RemoveAt(i--);
					continue;
				}

                // Build full mapping from traveller -> destination world using the same mirror used for rendering.
                // This ensures both rotation AND position are mirrored through the portal plane.
                var toDest = pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix *
                             t.transform.localToWorldMatrix;

				Vector3 offset = t.transform.position - transform.position;
				int sidePrev = Math.Sign(Vector3.Dot(t.previousOffsetFromPortal, transform.forward));
				int sideNow = Math.Sign(Vector3.Dot(offset, transform.forward));

                if (sideNow > 0 && sidePrev < 0) {
                    // front -> back: valid
                    Vector3 newPos = toDest.GetColumn(3);
                    Quaternion newRot = toDest.rotation;
                    newPos += pair.transform.forward * 0.15f;

					Debug.Log($"TELEPORT! {t.name} crossed {name} -> {pair.name}");
					t.Teleport(transform, pair.transform, newPos, newRot);

					_trackedTravellers.RemoveAt(i--);
					continue;
				}

				// update history
				t.previousOffsetFromPortal = offset;
			}
		}


		void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			if (!_trackedTravellers.Contains(traveller)) {
				// Calculate offset from portal
				Vector3 offsetFromPortal = traveller.transform.position - transform.position;

				// If just teleported, ensure previousOffset indicates we're on the BACK side (safe side)
				// to prevent immediate re-teleportation
				if (justTeleported) {
					// Make sure the previous offset is on the negative side (behind the portal)
					float currentDot = Vector3.Dot(offsetFromPortal, transform.forward);
					Debug.Log(
						$"{gameObject.name}: Teleport arrival - currentDot={currentDot:F3}, offsetFromPortal={offsetFromPortal}");
					if (currentDot >= 0) {
						// We're in front, so set previous to be behind
						traveller.previousOffsetFromPortal = offsetFromPortal - transform.forward * (currentDot + 0.1f);
						float newDot = Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward);
						Debug.Log($"{gameObject.name}: Forced to back side - newDot={newDot:F3}");
					}
					else {
						// Already behind, use actual position
						traveller.previousOffsetFromPortal = offsetFromPortal;
						Debug.Log($"{gameObject.name}: Already on back side");
					}
				}
				else {
					// Normal entry, use actual current offset
					traveller.previousOffsetFromPortal = offsetFromPortal;
				}

				_trackedTravellers.Add(traveller);

				// Disable collision between player and wall collider
				if (wallCollider) {
					Collider playerCollider = traveller.GetComponent<Collider>();
					if (playerCollider) {
						Physics.IgnoreCollision(playerCollider, wallCollider, true);
						Debug.Log($"{gameObject.name}: Disabled collision between {traveller.name} and wall");
					}
				}

				Debug.Log($"{gameObject.name}: Traveller {traveller.name} entered portal");
			}
		}

		public void SetWallCollider(Collider col) { wallCollider = col; }

		public void SetVisible(bool visible) {
			_visible = visible;
			if (portalCamera) portalCamera.gameObject.SetActive(visible);
			if (surfaceRenderer) surfaceRenderer.enabled = visible;
			if (!visible) {
				_ready = false;
				ClearTexture();
			}
		}

		void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) {
			if (cam != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;
			if (!pair) return;
			if (!_visible || !_ready) return;
			if (!pair._visible || !pair._ready) return;

			// Ensure the portal screen has enough thickness so the player's near clip plane
			// does not slice it when crossing the threshold. This mirrors the classic
			// ProtectScreenFromClipping technique.
			ProtectScreenFromClipping(mainCamera.transform.position);

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
				c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(-e.x, -e.y, e.z),
				c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(-e.x, e.y, e.z),
				c + new Vector3(e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, e.z),
				c + new Vector3(e.x, e.y, -e.z), c + new Vector3(e.x, e.y, e.z),
			};
			Vector3 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
			int on = 0;
			for (int i = 0; i < 8; i++) {
				var sp = mainCamera.WorldToScreenPoint(pts[i]);
				if (sp.z <= 0) continue;
				on++;
				if (sp.x < min.x) min.x = sp.x;
				if (sp.y < min.y) min.y = sp.y;
				if (sp.x > max.x) max.x = sp.x;
				if (sp.y > max.y) max.y = sp.y;
			}

			if (on == 0) return 0f;
			float area = (max.x - min.x) * (max.y - min.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}

		void Render(ScriptableRenderContext ctx) {
			ClearTexture();

			Matrix4x4 step = pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix;
			Matrix4x4 cur = mainCamera.transform.localToWorldMatrix;
			for (int i = 0; i < _recursion.Length; i++) {
				cur = step * cur;
				_recursion[i] = cur;
			}

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
			Vector3 up = world.MultiplyVector(Vector3.up);

			Vector3 oPos = portalCamera.transform.position;
			Quaternion oRot = portalCamera.transform.rotation;
			portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

			GeometryUtility.CalculateFrustumPlanes(portalCamera, _frustum);
			Bounds expanded = pair.surfaceRenderer.bounds;
			expanded.Expand(frustumCullMargin);
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

			// Skip if render target/pixel rect are invalid (prevents frustum errors)
			if (!portalCamera.targetTexture) return;
			if (portalCamera.pixelWidth <= 0 || portalCamera.pixelHeight <= 0) return;

			Vector3 pos = world.MultiplyPoint(Vector3.zero);
			Vector3 fwd = world.MultiplyVector(Vector3.forward);
			Vector3 up = world.MultiplyVector(Vector3.up);
			portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

			Vector3 planePoint = exitPos + exitFwd * 0.001f;
			Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
			Vector3 nCam = Vector3.Normalize(-w2c.MultiplyVector(exitFwd));
			Vector3 pCam = w2c.MultiplyPoint(planePoint);
			Vector4 clip = new(nCam.x, nCam.y, nCam.z, -Vector3.Dot(pCam, nCam));

			// Validate clip plane before applying
			if (!IsFinite(clip)) return;

			// Use the portal camera's projection for the oblique matrix
			portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clip);
			RenderPipeline.SubmitRenderRequest(portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			portalCamera.ResetProjectionMatrix();
		}

		static bool IsFinite(Vector4 v) {
			return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z) && float.IsFinite(v.w);
		}

		void ClearTexture() {
			if (_rt == null) return;
			var prev = RenderTexture.active;
			RenderTexture.active = _rt;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
		}

		// Sets the thickness of the portal screen so it won't be clipped by the
		// player's near plane when moving through the portal. Returns the thickness
		// applied (useful if callers want to reuse it for slicing logic).
		float ProtectScreenFromClipping(Vector3 viewPoint) {
			if (!mainCamera || !surfaceRenderer) return 0f;

			// Distance from eye to a corner of the near clip plane
			float halfHeight = mainCamera.nearClipPlane * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			float halfWidth = halfHeight * mainCamera.aspect;
			float thickness = new Vector3(halfWidth, halfHeight, mainCamera.nearClipPlane).magnitude;

			Transform screenT = surfaceRenderer.transform;
			bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0f;

			// Optionally extend to cover wall thickness too
			if (useWallThicknessForScreen && wallCollider)
			{
				float wallDepth = GetColliderDepthAlongForward(wallCollider);
				if (wallDepth > 0f) thickness = Mathf.Max(thickness, wallDepth + screenPadding);
			}

			// Scale depth (z) and offset along LOCAL Z so the volume sits entirely on the camera side
			float depth = Mathf.Abs(thickness);
			screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, depth);
			float half = (camFacingSameDirAsPortal ? 0.5f : -0.5f) * depth;
			screenT.localPosition = new Vector3(screenT.localPosition.x, screenT.localPosition.y, half);

			return thickness;
		}

		// Estimates how thick a collider is along the portal's forward direction
		float GetColliderDepthAlongForward(Collider col)
		{
			Bounds b = col.bounds; // world-space AABB
			Vector3 c = b.center; Vector3 e = b.extents;
			Vector3 fwd = transform.forward; // projection axis
			float minDot = float.PositiveInfinity, maxDot = float.NegativeInfinity;
			for (int xi = -1; xi <= 1; xi += 2)
			for (int yi = -1; yi <= 1; yi += 2)
			for (int zi = -1; zi <= 1; zi += 2)
			{
				Vector3 corner = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
				float d = Vector3.Dot(corner, fwd);
				if (d < minDot) minDot = d;
				if (d > maxDot) maxDot = d;
			}
			return Mathf.Max(0f, maxDot - minDot);
		}

		void OnTriggerEnter(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller) {
				OnTravellerEnterPortal(traveller);
			}
		}

		void OnTriggerExit(Collider other) {
			var traveller = other.GetComponent<PortalTraveller>();
			if (traveller && _trackedTravellers.Contains(traveller)) {
				_trackedTravellers.Remove(traveller);

				// Re-enable collision between player and wall collider
				if (wallCollider) {
					Collider playerCollider = traveller.GetComponent<Collider>();
					if (playerCollider) {
						Physics.IgnoreCollision(playerCollider, wallCollider, false);
						Debug.Log($"{gameObject.name}: Re-enabled collision between {traveller.name} and wall");
					}
				}

				Debug.Log($"{gameObject.name}: Traveller {traveller.name} exited portal");
			}
		}
	}
}