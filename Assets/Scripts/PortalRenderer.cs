using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PortalRenderer : MonoBehaviour {
	[SerializeField] public PortalRenderer pair;
	[SerializeField] private Camera cam, mainCam;
	private int recursionLimit = 2;
	[SerializeField] private MeshRenderer portalMeshRenderer;
	[SerializeField] private float portalOpenDuration = 1f;
	[SerializeField] private AnimationCurve portalOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	[SerializeField] private float portalAppearDuration = 0.3f;
	[SerializeField] private float portalTargetRadius = 0.4f;
	[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


	// Handedness/flip fixes you can toggle at runtime
	[SerializeField] bool flipViewForward = true; // start true
	[SerializeField] bool invertClipPlane = false; // start false
	[SerializeField] bool flipScaleZ = false; // start false: X-only flip

	private MaterialPropertyBlock propertyBlock;
	private Stack<Matrix4x4> matrices;
	private Matrix4x4 scaleMatrix;

	private float portalOpenProgress = 0f;
	private bool isOpening = false;
	private Coroutine openingCoroutine;
	private Coroutine appearCoroutine;

	// Cached pair portal transform data (updated only when portal is placed)
	private Vector3 cachedPairForward = Vector3.forward;
	private Vector3 cachedPairPosition = Vector3.zero;
	private bool cachedTransformDirty = true;

	private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");
	private static readonly int PortalOpenId = Shader.PropertyToID("_PortalOpen");

	// Cached shader property values (updated only when they change)
	private float cachedCircleRadius = -1f;
	private float cachedPortalOpen = -1f;

	// Temporal rendering: frame skip interval (directly set by PortalManager)
	private int frameSkipInterval = 1;
	private int renderOffset = 0; // Stagger portal renders to avoid both rendering same frame
	private int portalRefreshRatePercent = 50; // Percentage of main camera FPS

	// ===== OPTIMIZATION ADDITIONS =====
	// Frustum plane caching
	private Plane[] cachedFrustumPlanes = new Plane[6];
	private int lastFrustumUpdateFrame = -1;

	// Bounding sphere for faster culling
	private Bounds portalBounds;
	private Vector3 cachedPortalCenter = Vector3.zero;
	private float cachedPortalRadius = 0f;

	// Matrix caching
	private Matrix4x4 cachedPairLocalToWorld = Matrix4x4.identity;
	private Matrix4x4 cachedThisWorldToLocal = Matrix4x4.identity;
	private Matrix4x4 cachedMainCamLocalToWorld = Matrix4x4.identity;
	private int lastMatrixCacheFrame = -1;


	RenderTexture rt;


	void OnEnable() => RenderPipelineManager.beginCameraRendering += PreCull;
	void OnDisable() => RenderPipelineManager.beginCameraRendering -= PreCull;


	private void PreCull(ScriptableRenderContext ctx, Camera c) {
		if (c != mainCam) return; // <-- required
		if (!mainCam || !cam) return;
		if ((Time.frameCount % frameSkipInterval) != renderOffset) return;
		if (!ShouldRender() || !IsVisibleInMainCameraOptimized()) return;

		RenderPortal(ctx);
	}

	void Awake() {
		if (!cam) cam = GetComponentInChildren<Camera>(true);
		if (!portalMeshRenderer) portalMeshRenderer = GetComponentInChildren<MeshRenderer>(true);


		scaleMatrix = Matrix4x4.Scale(flipScaleZ
			? new Vector3(-1f, 1f, -1f)
			: new Vector3(-1f, 1f, 1f));


		cam.enabled = false;
		cam.forceIntoRenderTexture = true;
		cam.allowHDR = false;
		cam.useOcclusionCulling = false;
		cam.depthTextureMode = DepthTextureMode.None;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = Color.black;
		cam.stereoTargetEye = StereoTargetEyeMask.None;

		var desc = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.ARGB32, 24) {
			msaaSamples = 1, useMipMap = false, autoGenerateMips = false,
			sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear)
		};
		rt = new RenderTexture(desc) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
		rt.Create();
		cam.targetTexture = rt;
		if (portalMeshRenderer) portalMeshRenderer.sharedMaterial.mainTexture = rt;

		propertyBlock = new MaterialPropertyBlock();
		matrices = new Stack<Matrix4x4>(recursionLimit);
		scaleMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
		cachedFrustumPlanes = new Plane[6];
	}

	void OnDestroy() {
		if (rt) {
			rt.Release();
			Destroy(rt);
		}
	}

	/// <summary>
	/// Plays the appearance animation (growing from 0 to target radius).
	/// </summary>
	public void PlayAppear() {
		// Must activate before starting coroutine on inactive GameObject
		gameObject.SetActive(true);

		if (appearCoroutine != null) StopCoroutine(appearCoroutine);
		appearCoroutine = StartCoroutine(AppearCoroutine());
	}

	public void SetCircleRadius(float radius) {
		if (!portalMeshRenderer) return;

		// Only update if values changed (avoids redundant SetPropertyBlock calls)
		if (Mathf.Approximately(cachedCircleRadius, radius) &&
		    Mathf.Approximately(cachedPortalOpen, portalOpenProgress)) {
			return;
		}

		cachedCircleRadius = radius;
		cachedPortalOpen = portalOpenProgress;

		propertyBlock.SetFloat(CircleRadiusId, radius);
		propertyBlock.SetFloat(PortalOpenId, portalOpenProgress);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);

		// Update cached portal bounds for visibility culling
		cachedPortalRadius = radius;
	}

	/// <summary>
	/// Invalidates cached transform data. Call this when portal is placed/moved.
	/// </summary>
	public void InvalidateCachedTransform() {
		cachedTransformDirty = true;
		lastMatrixCacheFrame = -1; // Force matrix cache refresh
	}

	/// <summary>
	/// Sets the recursion limit for portal rendering (managed by PortalManager).
	/// </summary>
	public void SetRecursionLimit(int limit) {
		recursionLimit = Mathf.Max(1, limit);
		// Resize the matrices stack if needed
		matrices = new Stack<Matrix4x4>(recursionLimit);
	}

	/// <summary>
	/// Sets the frame skip interval for temporal portal rendering (managed by PortalManager).
	/// 1 = every frame, 2 = every other frame, 3 = every 3rd frame, etc.
	/// </summary>
	public void SetFrameSkipInterval(int interval) { frameSkipInterval = Mathf.Max(1, interval); }

	/// <summary>
	/// Sets the portal refresh rate as a percentage of main camera FPS (managed by PortalManager).
	/// 100% = render at same FPS as main camera
	/// 50% = render at half the FPS of main camera
	/// 25% = render at quarter the FPS of main camera
	/// Dynamically calculates frame skip based on actual game FPS.
	/// </summary>
	public void SetPortalRefreshRatePercent(int percent) { portalRefreshRatePercent = Mathf.Clamp(percent, 10, 100); }

	/// <summary>
	/// Sets render frame offset for staggering portal renders (managed by PortalManager).
	/// Prevents both portals from rendering on the same frames.
	/// </summary>
	public void SetRenderOffset(int offset) { renderOffset = offset; }

	public void StartOpening() {
		if (openingCoroutine != null) StopCoroutine(openingCoroutine);
		openingCoroutine = StartCoroutine(OpeningAnimation());
	}

	public bool IsFullyOpen => portalOpenProgress >= 0.8f;
	public bool IsOpening => isOpening;


	private IEnumerator AppearCoroutine() {
		SetCircleRadius(0f);

		float t = 0f;
		while (t < portalAppearDuration) {
			t += Time.deltaTime;
			float a = Mathf.Clamp01(t / portalAppearDuration);
			SetCircleRadius(portalTargetRadius * portalAppearCurve.Evaluate(a));
			yield return null;
		}

		SetCircleRadius(portalTargetRadius);
		appearCoroutine = null;
	}

	private IEnumerator OpeningAnimation() {
		isOpening = true;
		portalOpenProgress = 0f;
		float elapsed = 0f;
		const float targetProgress = 0.8f;

		while (elapsed < portalOpenDuration) {
			elapsed += Time.deltaTime;
			float normalizedTime = elapsed / portalOpenDuration;
			float curvedTime = portalOpenCurve.Evaluate(normalizedTime);
			portalOpenProgress = Mathf.Clamp01(curvedTime * targetProgress);

			UpdatePortalShader();
			yield return null;
		}

		portalOpenProgress = targetProgress;
		isOpening = false;
		openingCoroutine = null;
	}

	private void UpdatePortalShader() {
		if (!portalMeshRenderer) return;

		// Only update if value changed (avoids redundant SetPropertyBlock calls)
		if (Mathf.Approximately(cachedPortalOpen, portalOpenProgress)) {
			return;
		}

		cachedPortalOpen = portalOpenProgress;
		propertyBlock.SetFloat(PortalOpenId, portalOpenProgress);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);
	}

	private bool ShouldRender() {
		if (!pair) return false;
		bool thisCanRender = isOpening || IsFullyOpen;
		bool pairCanRender = pair.IsOpening || pair.IsFullyOpen;
		return thisCanRender && pairCanRender;
	}

	// ===== OPTIMIZED VISIBILITY CHECK =====
	private bool IsVisibleInMainCameraOptimized() {
		if (!portalMeshRenderer) return false;

		// Update frustum planes only once per frame (not per portal)
		if (lastFrustumUpdateFrame != Time.frameCount) {
			GeometryUtility.CalculateFrustumPlanes(mainCam, cachedFrustumPlanes);
			lastFrustumUpdateFrame = Time.frameCount;
		}

		// Use bounds intersection test - faster than AABB test
		return GeometryUtility.TestPlanesAABB(cachedFrustumPlanes, portalMeshRenderer.bounds);
	}

	// Fallback to original method if needed for compatibility
	private bool IsVisibleInMainCameraOld() {
		if (!portalMeshRenderer) return false;

		// Get main camera frustum planes
		Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCam);

		// Test if portal's bounds intersect with frustum
		return GeometryUtility.TestPlanesAABB(frustumPlanes, portalMeshRenderer.bounds);
	}

	private void RenderPortal(ScriptableRenderContext ctx) {
		// Update transform cache only if dirty
		if (cachedTransformDirty && pair) {
			cachedPairForward = pair.transform.forward;
			cachedPairPosition = pair.transform.position;
			cachedTransformDirty = false;
		}

		BuildMatrices();

		while (matrices.Count > 0) {
			RenderLevel(ctx, matrices.Pop(), cachedPairForward, cachedPairPosition);
		}
	}

	private void BuildMatrices() {
		matrices.Clear();

		// Cache matrices only if frame changed or they're dirty
		if (lastMatrixCacheFrame != Time.frameCount || cachedTransformDirty) {
			cachedPairLocalToWorld = pair.transform.localToWorldMatrix;
			cachedThisWorldToLocal = transform.worldToLocalMatrix;
			cachedMainCamLocalToWorld = mainCam.transform.localToWorldMatrix;
			lastMatrixCacheFrame = Time.frameCount;
		}

		for (var i = 0; i < recursionLimit; i++) {
			cachedMainCamLocalToWorld = cachedPairLocalToWorld * scaleMatrix * cachedThisWorldToLocal *
			                            cachedMainCamLocalToWorld;
			matrices.Push(cachedMainCamLocalToWorld);
		}
	}

	private void RenderLevel(ScriptableRenderContext ctx,
		Matrix4x4 matrix,
		Vector3 pairForward,
		Vector3 pairPosition)
	{
		// pose
		Vector3 camPos     = matrix.GetPosition();
		Vector3 camForward = matrix.GetColumn(2);
		Vector3 camUp      = matrix.GetColumn(1);
		cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(camForward, camUp));

		// clip plane
		var w2c = cam.worldToCameraMatrix;
		Vector3 n = -w2c.MultiplyVector(pairForward).normalized;
		Vector4 clipPlane = new Vector4(
			n.x, n.y, n.z,
			-Vector3.Dot(w2c.MultiplyPoint(pairPosition), n)
		);
		cam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);

		// make sure camera + RT are valid
		if (cam.targetTexture == null || !cam.targetTexture.IsCreated())
			return;

		// ensure URP renders normally
		var uacd = cam.GetUniversalAdditionalCameraData();
		uacd.renderPostProcessing = false;
		uacd.antialiasing = AntialiasingMode.None;
		uacd.requiresColorOption = CameraOverrideOption.On;
		uacd.requiresDepthOption = CameraOverrideOption.On;
		uacd.SetRenderer(0);

		// draw via current pipeline (works in Editor and Player)
		ctx.ExecuteCommandBuffer(new UnityEngine.Rendering.CommandBuffer()); // dummy to keep context valid
#pragma warning disable CS0618
		UniversalRenderPipeline.RenderSingleCamera(ctx, cam);
#pragma warning restore CS0618

		cam.ResetProjectionMatrix();
	}


}

