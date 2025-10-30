using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PortalRenderer : MonoBehaviour {
	[SerializeField] public PortalRenderer pair;
	[SerializeField] private Camera cam;
	[SerializeField] private Camera mainCam;
	private int recursionLimit = 2;
	[SerializeField] private MeshRenderer portalMeshRenderer;
	[SerializeField] private float portalOpenDuration = 1f;
	[SerializeField] private AnimationCurve portalOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
	[SerializeField] private float portalAppearDuration = 0.3f;
	[SerializeField] private float portalTargetRadius = 0.4f;
	[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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

	private void Awake() {
		if (!cam) cam = GetComponentInChildren<Camera>(true);
		if (!portalMeshRenderer) portalMeshRenderer = GetComponentInChildren<MeshRenderer>(true);
		propertyBlock = new MaterialPropertyBlock();
		matrices = new Stack<Matrix4x4>(recursionLimit);
		scaleMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
	}

	/// <summary>
	/// Plays the appearance animation (growing from 0 to target radius).
	/// </summary>
	public void PlayAppear()
	{
		// Must activate before starting coroutine on inactive GameObject
		gameObject.SetActive(true);
		
		if (appearCoroutine != null) StopCoroutine(appearCoroutine);
		appearCoroutine = StartCoroutine(AppearCoroutine());
	}

	public void SetCircleRadius(float radius) {
		if (!portalMeshRenderer) return;
		
		// Only update if values changed (avoids redundant SetPropertyBlock calls)
		if (Mathf.Approximately(cachedCircleRadius, radius) && Mathf.Approximately(cachedPortalOpen, portalOpenProgress)) {
			return;
		}
		
		cachedCircleRadius = radius;
		cachedPortalOpen = portalOpenProgress;
		
		propertyBlock.SetFloat(CircleRadiusId, radius);
		propertyBlock.SetFloat(PortalOpenId, portalOpenProgress);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);
	}

	/// <summary>
	/// Invalidates cached transform data. Call this when portal is placed/moved.
	/// </summary>
	public void InvalidateCachedTransform()
	{
		cachedTransformDirty = true;
	}

	/// <summary>
	/// Sets the recursion limit for portal rendering (managed by PortalManager).
	/// </summary>
	public void SetRecursionLimit(int limit)
	{
		recursionLimit = Mathf.Max(1, limit);
		// Resize the matrices stack if needed
		matrices = new Stack<Matrix4x4>(recursionLimit);
	}

	/// <summary>
	/// Sets the frame skip interval for temporal portal rendering (managed by PortalManager).
	/// 1 = every frame, 2 = every other frame, 3 = every 3rd frame, etc.
	/// </summary>
	public void SetFrameSkipInterval(int interval)
	{
		frameSkipInterval = Mathf.Max(1, interval);
	}

	/// <summary>
	/// Sets the portal refresh rate as a percentage of main camera FPS (managed by PortalManager).
	/// 100% = render at same FPS as main camera
	/// 50% = render at half the FPS of main camera
	/// 25% = render at quarter the FPS of main camera
	/// Dynamically calculates frame skip based on actual game FPS.
	/// </summary>
	public void SetPortalRefreshRatePercent(int percent)
	{
		portalRefreshRatePercent = Mathf.Clamp(percent, 10, 100);
	}

	/// <summary>
	/// Sets render frame offset for staggering portal renders (managed by PortalManager).
	/// Prevents both portals from rendering on the same frames.
	/// </summary>
	public void SetRenderOffset(int offset)
	{
		renderOffset = offset;
	}

	public void StartOpening() {
		if (openingCoroutine != null) StopCoroutine(openingCoroutine);
		openingCoroutine = StartCoroutine(OpeningAnimation());
	}

	public bool IsFullyOpen => portalOpenProgress >= 0.8f;
	public bool IsOpening => isOpening;

	private void LateUpdate() {
		if (!mainCam || !cam) return;
		
		// Calculate frame skip interval dynamically based on main camera FPS and portal refresh rate percent
		// Example: at 144 FPS main camera with 50% portal refresh:
		//   Target portal FPS = 144 * 0.5 = 72 FPS
		//   frameSkipInterval = 144 / 72 = 2
		if (Time.deltaTime > 0) {
			float mainCameraFPS = 1f / Time.deltaTime;
			float targetPortalFPS = mainCameraFPS * (portalRefreshRatePercent / 100f);
			frameSkipInterval = Mathf.Max(1, Mathf.RoundToInt(mainCameraFPS / targetPortalFPS));
		}
		
		// Temporal rendering: skip frames based on calculated interval
		// Motion blur on portal cameras makes this imperceptible to players
		if ((Time.frameCount % frameSkipInterval) == 0) {
			if (ShouldRender()) {
				// Only render if portal quad is visible in main camera's frustum
				if (IsVisibleInMainCamera()) {
					RenderPortal();
				}
			}
		}
	}

	private IEnumerator AppearCoroutine()
	{
		SetCircleRadius(0f);

		float t = 0f;
		while (t < portalAppearDuration)
		{
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

	private bool IsVisibleInMainCamera() {
		if (!portalMeshRenderer) return false;
		
		// Get main camera frustum planes
		Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCam);
		
		// Test if portal's bounds intersect with frustum
		return GeometryUtility.TestPlanesAABB(frustumPlanes, portalMeshRenderer.bounds);
	}

	private void RenderPortal() {
		BuildMatrices();
		
		// Update cache if dirty
		if (cachedTransformDirty && pair) {
			cachedPairForward = pair.transform.forward;
			cachedPairPosition = pair.transform.position;
			cachedTransformDirty = false;
		}

		while (matrices.Count > 0) {
			RenderLevel(matrices.Pop(), cachedPairForward, cachedPairPosition);
		}
	}

	private void BuildMatrices() {
		matrices.Clear();
		Matrix4x4 pairLocalToWorld = pair.transform.localToWorldMatrix;
		Matrix4x4 thisWorldToLocal = transform.worldToLocalMatrix;
		Matrix4x4 localToWorldMatrix = mainCam.transform.localToWorldMatrix;

		for (var i = 0; i < recursionLimit; i++) {
			localToWorldMatrix = pairLocalToWorld * scaleMatrix * thisWorldToLocal * localToWorldMatrix;
			matrices.Push(localToWorldMatrix);
		}
	}

	private void RenderLevel(Matrix4x4 matrix, Vector3 pairForward, Vector3 pairPosition) {
		cam.transform.SetPositionAndRotation(
			matrix.GetPosition(),
			Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1))
		);

		var worldToCameraMatrix = cam.worldToCameraMatrix;
		Vector3 normal = -worldToCameraMatrix.MultiplyVector(pairForward).normalized;
		Vector4 clipPlane = new Vector4(
			normal.x, normal.y, normal.z,
			-Vector3.Dot(worldToCameraMatrix.MultiplyPoint(pairPosition), normal)
		);

		cam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlane);
		cam.Render();
	}
}
