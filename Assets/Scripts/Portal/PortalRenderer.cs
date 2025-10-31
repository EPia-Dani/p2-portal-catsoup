using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portal {
	[RequireComponent(typeof(PortalRenderView))]
	[RequireComponent(typeof(PortalAnimator))]
	public class PortalRenderer : MonoBehaviour
	{
		[SerializeField] public PortalRenderer pair;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;
		[SerializeField] private int renderOffset;

		private PortalRenderView _view;
		private PortalAnimator _animator;
		private Matrix4x4[] _recursionMatrices = Array.Empty<Matrix4x4>();
		private Matrix4x4 _mirrorMatrix;
		private bool _isVisible = true;

		public bool IsFullyOpen => _animator && _animator.IsFullyOpen;
		public bool IsOpening => _animator && _animator.IsOpening;

		private void Awake()
		{
			_view = GetComponent<PortalRenderView>();
			_animator = GetComponent<PortalAnimator>();
			if (!mainCamera) mainCamera = Camera.main;

			_view.Initialize();
			_animator.Configure(_view.SurfaceRenderer);

			UpdateMirrorMatrix();
			EnsureRecursionArray();
			_isVisible = _view.IsVisible;
		}

		private void OnValidate()
		{
			UpdateMirrorMatrix();
			EnsureRecursionArray();
		}

		private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

		public void SetRecursionLimit(int limit)
		{
			recursionLimit = Mathf.Max(1, limit);
			EnsureRecursionArray();
		}

		public void SetRenderOffset(int offset) => renderOffset = offset;

		public void InvalidateCachedTransform() { }

		public void StartOpening()
		{
			if (!_isVisible) SetVisible(true);
			_animator?.StartOpening();
		}

		public void PlayAppear()
		{
			if (!_isVisible) SetVisible(true);
			_animator?.PlayAppear();
		}

		public void SetVisible(bool visible)
		{
			if (_isVisible == visible) return;
			_isVisible = visible;
			_view?.SetVisible(visible);
			if (!visible)
			{
				_animator?.HideImmediate();
			}
		}

		public void Show() => SetVisible(true);
		public void Hide() => SetVisible(false);

		private void OnBeginCameraRendering(ScriptableRenderContext context, Camera currentCamera)
		{
			if (!_view || !_animator) return;
			if (!mainCamera) mainCamera = Camera.main;
			if (!mainCamera || currentCamera != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != renderOffset) return;
			if (!ShouldRender()) return;
			if (!_view.IsVisible) return;
			if (!PortalVisibility.IsVisible(mainCamera, _view.SurfaceRenderer)) return;

			RenderPortal(context);
		}

		private bool ShouldRender()
		{
			if (!pair) return false;
			bool thisReady = _animator && (_animator.IsOpening || _animator.IsFullyOpen);
			bool pairReady = pair.IsOpening || pair.IsFullyOpen;
			return thisReady && pairReady;
		}

		private void RenderPortal(ScriptableRenderContext context)
		{
			if (!mainCamera || !_view.IsVisible) return;

			_view.EnsureRenderTexture();

			var source = transform;
			var destination = pair.transform;
			var stepMatrix = PortalRecursionSolver.BuildStepMatrix(source, destination, _mirrorMatrix);
			var startMatrix = mainCamera.transform.localToWorldMatrix;

			PortalRecursionSolver.Fill(_recursionMatrices, stepMatrix, startMatrix);

			var destinationForward = destination.forward;
			var destinationPosition = destination.position;

			for (int i = _recursionMatrices.Length - 1; i >= 0; i--)
			{
				_view.RenderLevel(context, mainCamera, _recursionMatrices[i], destinationForward, destinationPosition);
			}
		}

		private void EnsureRecursionArray()
		{
			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursionMatrices == null || _recursionMatrices.Length != recursionLimit)
			{
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}
		}

		private void UpdateMirrorMatrix()
		{
			_mirrorMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
		}
	}
}
