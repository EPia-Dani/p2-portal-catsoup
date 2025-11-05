// PortalRenderer.cs  (URP recursion + oblique clip, gates optional and OFF by default)
// Updated to support both cube and cylinder meshes

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

		// Dynamic RT sizing hysteresis to avoid frequent reallocations
		const float RtResizeHysteresis = 0.2f; // 20%
		// Additional debounce to ensure stability (in frames)
		const int RtResizeStableFrames = 6;
		private int _rtTargetW;
		private int _rtTargetH;
		private int _rtStableCounter;

		// Cached arrays/vectors to avoid per-frame allocations in coverage
		private readonly Vector3[] _cubeCoveragePts = new Vector3[8];
		private Vector3 _ssMin;
		private Vector3 _ssMax;

		// Cache last computed max recursion to avoid recomputation in Render()
		private int _lastMaxRecursionLevel;

		// Cylinder detection and caching
		private bool _isCylinder = false;
		private Vector3 _cylinderRadius = Vector3.one; // radius in local space (x, y, z)

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

			DetectCylinderGeometry();
			SetupCamera();
			AllocRT();

			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursion.Length != recursionLimit) _recursion = new Matrix4x4[recursionLimit];

			_visible = surfaceRenderer && surfaceRenderer.enabled;
		}

		void DetectCylinderGeometry() {
			if (!surfaceRenderer) return;

			var meshFilter = surfaceRenderer.GetComponent<MeshFilter>();
			if (!meshFilter || !meshFilter.sharedMesh) {
				// Try to detect from bounds
				var localBounds = surfaceRenderer.localBounds;
				DetectCylinderFromBounds(localBounds);
				return;
			}

			var mesh = meshFilter.sharedMesh;
			var bounds = mesh.bounds;
			
			// Check if mesh is roughly cylindrical
			// Cylinder characteristic: two dimensions are similar (radius), one is different (height or depth)
			Vector3 size = bounds.size;
			float minDim = Mathf.Min(size.x, size.y, size.z);
			float maxDim = Mathf.Max(size.x, size.y, size.z);
			float midDim = size.x + size.y + size.z - minDim - maxDim;

			// If two dimensions are similar and one is different, likely a cylinder
			float ratio1 = midDim / minDim;
			float ratio2 = maxDim / midDim;
			
			// Typical cylinder: radius ~= radius, height/depth is different
			if (ratio1 < 1.3f && ratio2 > 1.5f) {
				_isCylinder = true;
				
				// Determine which axis is the cylinder's "forward" (depth)
				// Portal forward should be along transform.forward (usually Z)
				Vector3 forward = transform.forward;
				Vector3 right = transform.right;
				Vector3 up = transform.up;
				
				// Project bounds size onto portal axes
				float forwardSize = Mathf.Abs(Vector3.Dot(size, forward));
				float rightSize = Mathf.Abs(Vector3.Dot(size, right));
				float upSize = Mathf.Abs(Vector3.Dot(size, up));
				
				// The other two dimensions define the elliptical cross-section
				// Use the two larger dimensions
				if (forwardSize <= rightSize && forwardSize <= upSize) {
					// Forward is depth
					_cylinderRadius = new Vector3(rightSize * 0.5f, upSize * 0.5f, forwardSize * 0.5f);
				} else if (rightSize <= forwardSize && rightSize <= upSize) {
					// Right is depth
					_cylinderRadius = new Vector3(forwardSize * 0.5f, upSize * 0.5f, rightSize * 0.5f);
				} else {
					// Up is depth
					_cylinderRadius = new Vector3(rightSize * 0.5f, forwardSize * 0.5f, upSize * 0.5f);
				}
			} else {
				// Fallback: try bounds-based detection
				DetectCylinderFromBounds(bounds);
			}
		}

		void DetectCylinderFromBounds(Bounds bounds) {
			Vector3 size = bounds.size;
			float minDim = Mathf.Min(size.x, size.y, size.z);
			float maxDim = Mathf.Max(size.x, size.y, size.z);
			
			// If one dimension is much smaller, might be a cylinder viewed edge-on
			if (maxDim / minDim > 3.0f) {
			_isCylinder = true;
				// Approximate radius from the larger dimensions
				float avgRadius = (size.x + size.y + size.z - minDim) * 0.25f;
				_cylinderRadius = new Vector3(avgRadius, avgRadius, minDim * 0.5f);
			}
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
				// Set pixelRect to match render texture dimensions
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
                    //newPos += pair.transform.forward * -0.1f; // nudge forward to avoid immediate re-teleport

					Collider playerCollider = t.GetComponent<Collider>();
					
					// Re-enable collision with SOURCE portal's wall BEFORE teleporting
					// This is necessary because OnTriggerExit may not fire when teleporting instantly
					if (wallCollider && playerCollider) {
						Physics.IgnoreCollision(playerCollider, wallCollider, false);
					}

					// Disable collision with destination portal's wall BEFORE teleporting
					// This prevents the player from colliding with the wall during teleport
					if (pair.wallCollider && playerCollider) {
						Physics.IgnoreCollision(playerCollider, pair.wallCollider, true);
					}

					t.Teleport(transform, pair.transform, newPos, newRot);
					
					// Remove from source portal's tracked list
					_trackedTravellers.RemoveAt(i--);
					
					// Manually notify destination portal that traveller just teleported in
					// This ensures proper setup since OnTriggerEnter may not fire immediately
					pair.OnTravellerEnterPortal(t, justTeleported: true);
					
					continue;
				}

				// update history
				t.previousOffsetFromPortal = offset;
			}
		}


		public void OnTravellerEnterPortal(PortalTraveller traveller, bool justTeleported = false) {
			if (!_trackedTravellers.Contains(traveller)) {
				// Calculate offset from portal
				Vector3 offsetFromPortal = traveller.transform.position - transform.position;

				// If just teleported, ensure previousOffset indicates we're on the BACK side (safe side)
				// to prevent immediate re-teleportation
				if (justTeleported) {
					// Make sure the previous offset is on the negative side (behind the portal)
					float currentDot = Vector3.Dot(offsetFromPortal, transform.forward);
					if (currentDot >= 0) {
						// We're in front, so set previous to be behind
						traveller.previousOffsetFromPortal = offsetFromPortal - transform.forward * (currentDot + 0.1f);
					}
					else {
						// Already behind, use actual position
						traveller.previousOffsetFromPortal = offsetFromPortal;
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
					}
				}
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

			// Adjust RT size dynamically before rendering
			_lastMaxRecursionLevel = GetMaxRecursionLevelForPair();
			EnsureDynamicRtSize(_lastMaxRecursionLevel);

			Render(ctx, _lastMaxRecursionLevel);
		}

		// Compute desired RT size from coverage and recursion and reallocate if needed
		void EnsureDynamicRtSize(int maxLevel) {
			if (mainCamera == null) return;

			float coverage = Mathf.Clamp01(GetScreenSpaceCoverage());
			
			// Use a gentler scaling curve that maintains quality at distance
			// Instead of sqrt, use a power curve that's less aggressive (coverage^0.6)
			// This maintains better quality when coverage is small
			float coveragePower = Mathf.Pow(coverage, 0.6f);
			
			// Apply a minimum quality floor (50% of full resolution) to prevent excessive blur
			// and scale up from there based on coverage
			float minQualityFloor = 0.5f; // Never go below 50% resolution
			float maxQualityScale = 1.5f;  // Can go up to 150% for close portals
			
			// Interpolate between min floor and max scale based on coverage
			// Higher coverage -> higher resolution, but never below the floor
			float scaleFromCoverage = Mathf.Lerp(minQualityFloor, maxQualityScale, coveragePower);
			
			// Recursion bias: +10% per level (cap 5 levels of bias)
			float recursionBias = 1f + 0.1f * Mathf.Clamp(maxLevel, 0, 5);
			float targetScale = Mathf.Clamp(scaleFromCoverage * recursionBias, minQualityFloor, 2.5f);

			int targetW = ClosestTier(mainCamera.pixelWidth * targetScale);
			int targetH = ClosestTier(mainCamera.pixelHeight * targetScale);

			// Clamp to reasonable bounds - ensure minimum quality is maintained
			// Minimum is now based on maintaining 50% of screen resolution
			int minWidth = Mathf.Max(512, Mathf.RoundToInt(mainCamera.pixelWidth * minQualityFloor));
			int minHeight = Mathf.Max(288, Mathf.RoundToInt(mainCamera.pixelHeight * minQualityFloor));
			targetW = Mathf.Clamp(targetW, minWidth, 2048);
			targetH = Mathf.Clamp(targetH, minHeight, 2048);

			bool bigDelta = (Mathf.Abs(targetW - textureWidth) > textureWidth * RtResizeHysteresis) ||
			               (Mathf.Abs(targetH - textureHeight) > textureHeight * RtResizeHysteresis);

			// Debounce: only resize after target has remained stable for N frames
			if (!bigDelta) {
				// Reset target tracking if we're already close
				_rtTargetW = textureWidth;
				_rtTargetH = textureHeight;
				_rtStableCounter = 0;
				return;
			}

			if (_rtTargetW != targetW || _rtTargetH != targetH) {
				_rtTargetW = targetW;
				_rtTargetH = targetH;
				_rtStableCounter = 0;
			} else {
				_rtStableCounter++;
			}

			if (_rtStableCounter >= RtResizeStableFrames) {
				textureWidth = _rtTargetW;
				textureHeight = _rtTargetH;
				_rtStableCounter = 0;
				AllocRT();
			}
		}

		static int ClosestTier(float v) {
			int[] tiers = { 256, 384, 512, 640, 768, 896, 1024, 1280, 1536, 1792, 2048 };
			int best = tiers[0];
			float bestD = Mathf.Abs(v - best);
			for (int i = 1; i < tiers.Length; i++) {
				float d = Mathf.Abs(v - tiers[i]);
				if (d < bestD) { bestD = d; best = tiers[i]; }
			}
			return best;
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
			
			if (_isCylinder) {
				return GetScreenSpaceCoverageCylinder();
			}
			
			// Cube-based calculation without allocations
			var b = surfaceRenderer.bounds;
			Vector3 c = b.center, e = b.extents;
			_cubeCoveragePts[0] = c + new Vector3(-e.x, -e.y, -e.z);
			_cubeCoveragePts[1] = c + new Vector3(-e.x, -e.y,  e.z);
			_cubeCoveragePts[2] = c + new Vector3(-e.x,  e.y, -e.z);
			_cubeCoveragePts[3] = c + new Vector3(-e.x,  e.y,  e.z);
			_cubeCoveragePts[4] = c + new Vector3( e.x, -e.y, -e.z);
			_cubeCoveragePts[5] = c + new Vector3( e.x, -e.y,  e.z);
			_cubeCoveragePts[6] = c + new Vector3( e.x,  e.y, -e.z);
			_cubeCoveragePts[7] = c + new Vector3( e.x,  e.y,  e.z);

			_ssMin.x = float.MaxValue; _ssMin.y = float.MaxValue;
			_ssMax.x = float.MinValue; _ssMax.y = float.MinValue;
			int on = 0;
			for (int i = 0; i < 8; i++) {
				var sp = mainCamera.WorldToScreenPoint(_cubeCoveragePts[i]);
				if (sp.z <= 0) continue;
				on++;
				if (sp.x < _ssMin.x) _ssMin.x = sp.x;
				if (sp.y < _ssMin.y) _ssMin.y = sp.y;
				if (sp.x > _ssMax.x) _ssMax.x = sp.x;
				if (sp.y > _ssMax.y) _ssMax.y = sp.y;
			}

			if (on == 0) return 0f;
			float area = (_ssMax.x - _ssMin.x) * (_ssMax.y - _ssMin.y);
			return area / (mainCamera.pixelWidth * mainCamera.pixelHeight);
		}

		float GetScreenSpaceCoverageCylinder() {
			// Sample points around the cylinder's elliptical perimeter
			// Use the portal's local right/up axes for the ellipse
			Vector3 center = surfaceRenderer.bounds.center;
			Vector3 right = transform.right;
			Vector3 up = transform.up;
			
			// Get radius in local space (assuming ellipse in right-up plane)
			float radiusX = _cylinderRadius.x; // right axis
			float radiusY = _cylinderRadius.y; // up axis
			
			// Sample points around the ellipse perimeter
			int sampleCount = 16; // Good balance between accuracy and performance
			Vector3 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
			int on = 0;
			
			for (int i = 0; i < sampleCount; i++) {
				float angle = (i / (float)sampleCount) * Mathf.PI * 2f;
				// Ellipse parameterization
				Vector3 point = center + right * (Mathf.Cos(angle) * radiusX) + up * (Mathf.Sin(angle) * radiusY);
				
				var sp = mainCamera.WorldToScreenPoint(point);
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

		void Render(ScriptableRenderContext ctx, int maxRecursionLevel) {
			ClearTexture();

			Matrix4x4 step = pair.transform.localToWorldMatrix * _mirror * transform.worldToLocalMatrix;
			Matrix4x4 cur = mainCamera.transform.localToWorldMatrix;
			for (int i = 0; i < _recursion.Length; i++) {
				cur = step * cur;
				_recursion[i] = cur;
			}

			Vector3 exitPos = pair.transform.position;
			Vector3 exitFwd = pair.transform.forward;

			int start = Mathf.Min(maxRecursionLevel, _recursion.Length - 1);

			for (int i = start; i >= 0; i--) {
				RenderLevel(_recursion[i], exitPos, exitFwd);
			}
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

			// Ensure pixelRect matches render texture dimensions
			if (portalCamera.targetTexture != null) {
				portalCamera.pixelRect = new Rect(0, 0, portalCamera.targetTexture.width, portalCamera.targetTexture.height);
			}

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
					}
				}
			}
		}
	}
}