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
		[SerializeField] private bool gateByThroughCamera = true; // keep this, cheap cull
		[SerializeField] private float frustumCullMargin = 3.0f;

		
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

		// Cylinder detection and caching
		private bool _isCylinder = false;
		private Vector3 _cylinderRadius = Vector3.one; // radius in local space (x, y, z)
		private float _cylinderDepth = 0.1f; // thickness along forward axis

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
				
				// The smallest dimension is likely the depth (thickness)
				_cylinderDepth = Mathf.Min(forwardSize, rightSize, upSize);
				
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
				_cylinderDepth = minDim;
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
						Debug.Log($"{gameObject.name}: Re-enabled collision between {t.name} and " + wallCollider.name + " (teleport exit)");
					}

					// Disable collision with destination portal's wall BEFORE teleporting
					// This prevents the player from colliding with the wall during teleport
					if (pair.wallCollider && playerCollider) {
						Physics.IgnoreCollision(playerCollider, pair.wallCollider, true);
						Debug.Log($"{pair.name}: Disabled collision between {t.name} and " + pair.wallCollider.name);
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
						Debug.Log($"{gameObject.name}: Disabled collision between {traveller.name} and " + wallCollider.name);
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
			
			if (_isCylinder) {
				return GetScreenSpaceCoverageCylinder();
			}
			
			// Original cube-based calculation
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
			
			// For cylinders, use a tighter bounds approximation
			Bounds expanded = pair.surfaceRenderer.bounds;
			if (pair._isCylinder) {
				// Shrink bounds slightly for cylinders to avoid over-conservative culling
				// The AABB is already larger than needed, so we don't expand as much
				expanded.Expand(frustumCullMargin * 0.5f);
			} else {
				expanded.Expand(frustumCullMargin);
			}
			
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
						Debug.Log($"{gameObject.name}: Re-enabled collision between {traveller.name} and " + wallCollider.name);
					}
				}
			}
		}
	}
}