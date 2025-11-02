using System.Collections;
using UnityEngine;

namespace Portal {
	public class PortalAnimator : MonoBehaviour {
		[SerializeField] private float portalOpenDuration = 1f;
		[SerializeField] private AnimationCurve portalOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
		[SerializeField] private float portalAppearDuration = 0.3f;
		[SerializeField] private float portalTargetRadius = 0.4f;
		[SerializeField] private AnimationCurve portalAppearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
		[SerializeField] private float openThreshold = 0.8f;

		private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");
		private static readonly int PortalOpenId = Shader.PropertyToID("_PortalOpen");

	private MeshRenderer _portalMeshRenderer;
	private MaterialPropertyBlock _propertyBlock;
	private Coroutine _openingCoroutine;
	private Coroutine _appearCoroutine;
	private float _portalOpenProgress;
	private float _currentCircleRadius;

	public bool IsOpening => _openingCoroutine != null;
	public bool IsFullyOpen => _portalOpenProgress >= openThreshold;

	public void Configure(MeshRenderer renderer) {
		if (renderer != null) {
			_portalMeshRenderer = renderer;
		}
	}

	private void Awake() {
		_portalMeshRenderer = GetComponent<MeshRenderer>();
		if (_portalMeshRenderer == null) {
			_portalMeshRenderer = GetComponentInChildren<MeshRenderer>();
		}
		if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
		_currentCircleRadius = 0f;
		_portalOpenProgress = 0f;
		ApplyToMaterial();
	}

		public void SetAnimationSettings(
			float openDuration,
			AnimationCurve openCurve,
			float appearDuration,
			float targetRadius,
			AnimationCurve appearCurve,
			float threshold) {
			portalOpenDuration = Mathf.Max(0.1f, openDuration);
			portalOpenCurve = openCurve != null ? openCurve : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			portalAppearDuration = Mathf.Max(0.1f, appearDuration);
			portalTargetRadius = Mathf.Max(0.1f, targetRadius);
			portalAppearCurve = appearCurve != null ? appearCurve : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			openThreshold = Mathf.Clamp01(threshold);
		}

		public void PlayAppear() {
			if (!_portalMeshRenderer) return;
			if (_appearCoroutine != null) StopCoroutine(_appearCoroutine);
			_appearCoroutine = StartCoroutine(AppearRoutine());
		}

		public void StartOpening() {
			if (!_portalMeshRenderer) return;
			if (_openingCoroutine != null) StopCoroutine(_openingCoroutine);
			_openingCoroutine = StartCoroutine(OpeningRoutine());
		}

		public void HideImmediate() {
			if (_openingCoroutine != null) {
				StopCoroutine(_openingCoroutine);
				_openingCoroutine = null;
			}

			if (_appearCoroutine != null) {
				StopCoroutine(_appearCoroutine);
				_appearCoroutine = null;
			}

			_portalOpenProgress = 0f;
			SetCircleRadius(0f);
			ApplyToMaterial();
		}

		private IEnumerator AppearRoutine() {
			SetCircleRadius(0f);

			float elapsed = 0f;
			while (elapsed < portalAppearDuration) {
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / portalAppearDuration);
				SetCircleRadius(portalTargetRadius * portalAppearCurve.Evaluate(t));
				yield return null;
			}

			SetCircleRadius(portalTargetRadius);
			_appearCoroutine = null;
		}

	private IEnumerator OpeningRoutine() {
		_portalOpenProgress = 0f;
		float elapsed = 0f;

		while (elapsed < portalOpenDuration) {
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / portalOpenDuration);
			_portalOpenProgress = portalOpenCurve.Evaluate(t) * openThreshold;
			ApplyToMaterial();
			yield return null;
		}

		_portalOpenProgress = openThreshold;
		ApplyToMaterial();
		_openingCoroutine = null;
	}

		private void SetCircleRadius(float radius) {
			_currentCircleRadius = radius;
			ApplyToMaterial();
		}

		private void ApplyToMaterial() {
			if (!_portalMeshRenderer) return;
			_propertyBlock.SetFloat(CircleRadiusId, _currentCircleRadius);
			_propertyBlock.SetFloat(PortalOpenId, _portalOpenProgress);
			_portalMeshRenderer.SetPropertyBlock(_propertyBlock);
		}
	}
}
