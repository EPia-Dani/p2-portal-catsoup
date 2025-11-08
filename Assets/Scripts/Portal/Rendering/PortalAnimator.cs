
// PortalAnimator.cs
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

		[Header("Border Renderer")]
		[SerializeField] private MeshRenderer borderRenderer;

		[Header("Mesh Scale Animation")]
		[SerializeField] private Transform meshTransform;

		private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");
		private static readonly int PortalOpenId = Shader.PropertyToID("_PortalOpen");

		private MeshRenderer _portalMeshRenderer;
		private MaterialPropertyBlock _propertyBlock;
		private Coroutine _openingCoroutine;
		private Coroutine _appearCoroutine;
		private float _portalOpenProgress;
		private float _currentCircleRadius;
		private Vector3 _meshBaseScale = Vector3.one;
		private float _targetScaleX;
		private float _targetScaleZ;

		public bool IsOpening => _openingCoroutine != null;
		public bool IsFullyOpen => _portalOpenProgress >= openThreshold;

		private void Awake() {
			_portalMeshRenderer = GetComponent<MeshRenderer>();
			if (_portalMeshRenderer == null) _portalMeshRenderer = GetComponentInChildren<MeshRenderer>();
			if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
			_currentCircleRadius = 0f;
			_portalOpenProgress = 0f;
			
			// Store base scale if mesh transform is assigned
			if (meshTransform != null) {
				_meshBaseScale = meshTransform.localScale;
			}
			
			ApplyToMaterial();
		}

		public void SetAnimationSettings(float openDuration, AnimationCurve openCurve, float appearDuration, float targetRadius, AnimationCurve appearCurve, float threshold) {
			portalOpenDuration = Mathf.Max(0.1f, openDuration);
			portalOpenCurve = openCurve != null ? openCurve : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			portalAppearDuration = Mathf.Max(0.1f, appearDuration);
			portalTargetRadius = Mathf.Max(0.1f, targetRadius);
			portalAppearCurve = appearCurve != null ? appearCurve : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			openThreshold = Mathf.Clamp01(threshold);
		}

		public void PlayAppear(float? targetScaleX = null, float? targetScaleZ = null) {
			if (_appearCoroutine != null) StopCoroutine(_appearCoroutine);
			
			// Use provided target scale, or try to read from mesh's current scale
			// This ensures the animation scales to whatever size the portal was placed at
			if (targetScaleX.HasValue && targetScaleZ.HasValue) {
				_targetScaleX = targetScaleX.Value;
				_targetScaleZ = targetScaleZ.Value;
			} else if (meshTransform != null) {
				_targetScaleX = meshTransform.localScale.x;
				_targetScaleZ = meshTransform.localScale.z;
			} else {
				_targetScaleX = 0f;
				_targetScaleZ = 0f;
			}
			
			_appearCoroutine = StartCoroutine(AppearRoutine());
		}

		public void SetMeshTransform(Transform mesh) {
			meshTransform = mesh;
			if (meshTransform != null) {
				_meshBaseScale = meshTransform.localScale;
			}
		}

		public void StartOpening() {
			if (_openingCoroutine != null) StopCoroutine(_openingCoroutine);
			_openingCoroutine = StartCoroutine(OpeningRoutine());
		}

		public void HideImmediate() {
			if (_openingCoroutine != null) { StopCoroutine(_openingCoroutine); _openingCoroutine = null; }
			if (_appearCoroutine != null) { StopCoroutine(_appearCoroutine); _appearCoroutine = null; }
			_portalOpenProgress = 0f;
			SetCircleRadius(0f);
			ApplyToMaterial();
			
			// Reset mesh scale
			if (meshTransform != null) {
				meshTransform.localScale = new Vector3(0f, _meshBaseScale.y, 0f);
			}
		}

		private IEnumerator AppearRoutine() {
			SetCircleRadius(0f);
			
			// Initialize mesh scale to 0 for X and Z (preserve Y scale)
			if (meshTransform != null) {
				meshTransform.localScale = new Vector3(0f, meshTransform.localScale.y, 0f);
			}
			
			float elapsed = 0f;
			while (elapsed < portalAppearDuration) {
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / portalAppearDuration);
				float curveValue = portalAppearCurve.Evaluate(t);
				
				// Animate circle radius
				SetCircleRadius(portalTargetRadius * curveValue);
				
				// Animate mesh scale (X and Z from 0 to target values based on portal scale)
				if (meshTransform != null) {
					float scaleX = _targetScaleX * curveValue;
					float scaleZ = _targetScaleZ * curveValue;
					meshTransform.localScale = new Vector3(scaleX, meshTransform.localScale.y, scaleZ);
				}
				
				yield return null;
			}
			
			SetCircleRadius(portalTargetRadius);
			
			// Set final mesh scale to the target values
			if (meshTransform != null) {
				meshTransform.localScale = new Vector3(_targetScaleX, meshTransform.localScale.y, _targetScaleZ);
			}
			
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
			// Use border renderer if assigned, otherwise fallback to portal renderer
			MeshRenderer targetRenderer = borderRenderer != null ? borderRenderer : _portalMeshRenderer;
			if (targetRenderer == null) return;

			_propertyBlock.SetFloat(CircleRadiusId, _currentCircleRadius);
			_propertyBlock.SetFloat(PortalOpenId, _portalOpenProgress);
			targetRenderer.SetPropertyBlock(_propertyBlock);
		}
	}
}
