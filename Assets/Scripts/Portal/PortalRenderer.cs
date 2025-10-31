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

		// Cached transform references for performance
		private Transform _cachedTransform;
		private Transform _cachedPairTransform;
		private Transform _cachedMainCameraTransform;
		
		// Cached transform properties (updated when portals move)
		private Vector3 _cachedDestinationForward;
		private Vector3 _cachedDestinationPosition;

		// Cached component references to avoid repeated property access
		private MeshRenderer _cachedSurfaceRenderer;

		public bool IsFullyOpen => _animator != null && _animator.IsFullyOpen;
		public bool IsOpening => _animator != null && _animator.IsOpening;

		private void Awake()
		{
			_view = GetComponent<PortalRenderView>();
			_animator = GetComponent<PortalAnimator>();
			if (!mainCamera) mainCamera = Camera.main;
			if (!mainCamera)
			{
				Debug.LogError("PortalRenderer: Main camera not found! Assign Camera.main or set mainCamera in inspector.", this);
			}

			// Cache transform references
			_cachedTransform = transform;
			if (pair) _cachedPairTransform = pair.transform;
			if (mainCamera) _cachedMainCameraTransform = mainCamera.transform;

			_view.Initialize();
			_cachedSurfaceRenderer = _view.SurfaceRenderer;
			_animator.Configure(_cachedSurfaceRenderer);

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

		public void InvalidateCachedTransform()
		{
			// Update cached transform references when portals are moved
			if (pair) _cachedPairTransform = pair.transform;
		}

		public void StartOpening()
		{
			if (!_isVisible) SetVisible(true);
			if (_animator != null) _animator.StartOpening();
		}

		public void PlayAppear()
		{
			if (!_isVisible) SetVisible(true);
			if (_animator != null) _animator.PlayAppear();
		}

		public void SetVisible(bool visible)
		{
			if (_isVisible == visible) return;
			_isVisible = visible;
			if (_view != null) _view.SetVisible(visible);
			if (!visible && _animator != null)
			{
				_animator.HideImmediate();
			}
		}

		public void Show() => SetVisible(true);
		public void Hide() => SetVisible(false);

		private void OnBeginCameraRendering(ScriptableRenderContext context, Camera currentCamera)
		{
			if (!_view || !_animator) return;
			if (!mainCamera)
			{
				// Only fallback to Camera.main if mainCamera was destroyed/nullified
				mainCamera = Camera.main;
				if (mainCamera) _cachedMainCameraTransform = mainCamera.transform;
			}
			if (!mainCamera || currentCamera != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != renderOffset) return;
			if (!ShouldRender()) return;
			if (!_view.IsVisible) return;
			if (!PortalVisibility.IsVisible(mainCamera, _cachedSurfaceRenderer)) return;

			RenderPortal(context);
		}

		private bool ShouldRender()
		{
			if (pair == null || _animator == null) return false;
			bool thisReady = _animator.IsOpening || _animator.IsFullyOpen;
			bool pairReady = pair.IsOpening || pair.IsFullyOpen;
			return thisReady && pairReady;
		}

		private void RenderPortal(ScriptableRenderContext context)
		{
			if (!mainCamera || !_view.IsVisible || !pair) return;

			_view.EnsureRenderTexture();

			// Use cached transforms for better performance
			Transform source = _cachedTransform;
			Transform destination = _cachedPairTransform;
			
			// Cache pair transform if not already cached (avoid null coalescing overhead)
			if (destination == null)
			{
				destination = pair.transform;
				_cachedPairTransform = destination;
			}

			Matrix4x4 stepMatrix = PortalRecursionSolver.BuildStepMatrix(source, destination, _mirrorMatrix);
			
			// Cache main camera transform and matrix (avoid null coalescing overhead)
			Transform mainCamTransform = _cachedMainCameraTransform;
			if (mainCamTransform == null)
			{
				mainCamTransform = mainCamera.transform;
				_cachedMainCameraTransform = mainCamTransform;
			}
			Matrix4x4 startMatrix = mainCamTransform.localToWorldMatrix;

			PortalRecursionSolver.Fill(_recursionMatrices, stepMatrix, startMatrix);

			// Cache destination properties (accessed once per render, not per recursion level)
			_cachedDestinationForward = destination.forward;
			_cachedDestinationPosition = destination.position;

			int matrixCount = _recursionMatrices.Length;
			for (int i = matrixCount - 1; i >= 0; i--)
			{
				_view.RenderLevel(context, mainCamera, _recursionMatrices[i], _cachedDestinationForward, _cachedDestinationPosition);
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
