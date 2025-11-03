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

				var toDest = pair.transform.localToWorldMatrix * transform.worldToLocalMatrix *
				             t.transform.localToWorldMatrix;

				Vector3 offset = t.transform.position - transform.position;
				int sidePrev = Math.Sign(Vector3.Dot(t.previousOffsetFromPortal, transform.forward));
				int sideNow = Math.Sign(Vector3.Dot(offset, transform.forward));

				if (sideNow > 0 && sidePrev < 0) {
					// front -> back: valid
					Vector3 newPos = toDest.GetColumn(3);
					Quaternion newRot = toDest.rotation * Quaternion.AngleAxis(180f, pair.transform.up);
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

			Vector3 pos = world.MultiplyPoint(Vector3.zero);
			Vector3 fwd = world.MultiplyVector(Vector3.forward);
			Vector3 up = world.MultiplyVector(Vector3.up);
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