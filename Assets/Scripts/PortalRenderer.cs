using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PortalRenderer : MonoBehaviour {
	[SerializeField] public PortalRenderer pair;
	[SerializeField] private Camera cam;
	[SerializeField] private Camera mainCam;
	[SerializeField] private int recursionLimit = 5;
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

	private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");
	private static readonly int PortalOpenId = Shader.PropertyToID("_PortalOpen");

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
		portalMeshRenderer.GetPropertyBlock(propertyBlock);
		propertyBlock.SetFloat(CircleRadiusId, radius);
		propertyBlock.SetFloat(PortalOpenId, portalOpenProgress);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);
	}

	public void StartOpening() {
		if (openingCoroutine != null) StopCoroutine(openingCoroutine);
		openingCoroutine = StartCoroutine(OpeningAnimation());
	}

	public bool IsFullyOpen => portalOpenProgress >= 0.8f;
	public bool IsOpening => isOpening;

	private void LateUpdate() {
		if (!mainCam || !cam) return;
		if (ShouldRender()) {
			RenderPortal();
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
		portalMeshRenderer.GetPropertyBlock(propertyBlock);
		propertyBlock.SetFloat(PortalOpenId, portalOpenProgress);
		portalMeshRenderer.SetPropertyBlock(propertyBlock);
	}

	private bool ShouldRender() {
		if (!pair) return false;
		bool thisCanRender = isOpening || IsFullyOpen;
		bool pairCanRender = pair.IsOpening || pair.IsFullyOpen;
		return thisCanRender && pairCanRender;
	}

	private void RenderPortal() {
		BuildMatrices();
		Vector3 pairForward = pair.transform.forward;
		Vector3 pairPosition = pair.transform.position;

		while (matrices.Count > 0) {
			RenderLevel(matrices.Pop(), pairForward, pairPosition);
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
