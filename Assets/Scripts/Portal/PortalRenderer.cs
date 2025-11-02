using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portal {
	[RequireComponent(typeof(PortalRenderView))]
	[RequireComponent(typeof(PortalAnimator))]
	public class PortalRenderer : MonoBehaviour {
		[SerializeField] public PortalRenderer pair;
		[SerializeField] private Camera mainCamera;
		[SerializeField] private int recursionLimit = 2;
		[SerializeField] private int frameSkipInterval = 1;

		private PortalRenderView _view;
		private PortalAnimator _animator;
		private Matrix4x4[] _recursionMatrices = Array.Empty<Matrix4x4>();
		private readonly Matrix4x4 _mirrorMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
		private bool _isVisible = true;

		public PortalRenderView RenderView => _view;

		/// <summary>
		/// Initializes components and sets up recursion matrices array.
		/// Falls back to Camera.main if mainCamera is not set in inspector.
		/// </summary>
		private void Awake() {
			_view = GetComponent<PortalRenderView>();
			_animator = GetComponent<PortalAnimator>();
			if (!mainCamera) mainCamera = Camera.main;
			if (!mainCamera) {
				Debug.LogError(
					"PortalRenderer: Main camera not found! Assign Camera.main or set mainCamera in inspector.", this);
			}

			_view.Initialize();
			_animator.Configure(_view.SurfaceRenderer);

			recursionLimit = Mathf.Max(1, recursionLimit);
			if (_recursionMatrices.Length != recursionLimit) {
				_recursionMatrices = new Matrix4x4[recursionLimit];
			}

		_isVisible = _view._isVisible;
	}

	/// <summary>
	/// Called by PortalManager to apply all settings at startup.
	/// </summary>
	public void ConfigurePortal(int textureWidth, int textureHeight, int recLimit, int frameskip)
	{
		_view.UpdateTextureResolution(textureWidth, textureHeight);
		SetRecursionLimit(recLimit);
		SetFrameSkipInterval(frameskip);
	}

	/// <summary>
	/// Updates recursion limit at runtime.
	/// </summary>
	public void SetRecursionLimit(int limit)
	{
		recursionLimit = Mathf.Max(1, limit);
		if (_recursionMatrices.Length != recursionLimit) {
			_recursionMatrices = new Matrix4x4[recursionLimit];
		}
	}

	/// <summary>
	/// Updates frame skip interval at runtime.
	/// </summary>
	public void SetFrameSkipInterval(int interval)
	{
		frameSkipInterval = Mathf.Max(1, interval);
	}

		/// <summary>
		/// Subscribes to render pipeline events when portal becomes active.
		/// </summary>
		private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

		/// <summary>
		/// Unsubscribes from render pipeline events when portal becomes inactive.
		/// </summary>
		private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

		/// <summary>
		/// Called when portal position changes. No longer needed since we access transform directly.
		/// Kept for backwards compatibility with PortalManager.
		/// </summary>
		public void InvalidateCachedTransform() {
			// No-op: transforms are accessed directly now
		}

		/// <summary>
		/// Starts the portal opening animation sequence.
		/// Makes portal visible if not already visible.
		/// </summary>
		public void StartOpening() {
			if (!_isVisible) SetVisible(true);
			_animator?.StartOpening();
		}

		/// <summary>
		/// Plays the portal appearance animation.
		/// Makes portal visible if not already visible.
		/// </summary>
		public void PlayAppear() {
			if (!_isVisible) SetVisible(true);
			_animator?.PlayAppear();
		}

		/// <summary>
		/// Sets portal visibility state and updates view/animation accordingly.
		/// Clears texture and hides animation immediately when hiding.
		/// </summary>
		/// <param name="visible">Whether the portal should be visible</param>
		public void SetVisible(bool visible) {
			if (_isVisible == visible) return;
			_isVisible = visible;
			_view?.SetVisible(visible);
			if (!visible) {
				_animator?.HideImmediate();
				_view?.ClearTexture();
			}
		}

		/// <summary>
		/// Called each frame by the render pipeline when main camera starts rendering.
		/// Checks frame skip, render conditions, visibility, and frustum culling before rendering.
		/// </summary>
		/// <param name="context">The scriptable render context</param>
		/// <param name="currentCamera">The camera currently being rendered</param>
		private void OnBeginCameraRendering(ScriptableRenderContext context, Camera currentCamera) {
			if (currentCamera != mainCamera) return;
			if ((Time.frameCount % frameSkipInterval) != 0) return;
			if (!ShouldRender()) return;
			if (!_view._isVisible) return;
			if (!PortalVisibility.IsVisible(mainCamera, _view.SurfaceRenderer)) return;

			RenderPortal(context);
		}

		/// <summary>
		/// Checks if both this portal and its pair are ready to render.
		/// Portals must be opening or fully open to render.
		/// </summary>
		/// <returns>True if both portals are ready to render, false otherwise</returns>
		private bool ShouldRender() {
			if (!pair || !_animator || !pair._animator) return false;
			
			bool thisReady = _animator.IsOpening || _animator.IsFullyOpen;
			bool pairReady = pair._animator.IsOpening || pair._animator.IsFullyOpen;
			return thisReady && pairReady;
		}

		/// <summary>
		/// Main rendering logic. Clears texture, calculates recursion matrices,
		/// and renders each recursion level from deepest to shallowest.
		/// Checks visibility of the pair portal before rendering each level to prevent unnecessary recursion.
		/// </summary>
		/// <param name="context">The scriptable render context</param>
		private void RenderPortal(ScriptableRenderContext context) {
			if (!mainCamera || !_view._isVisible || !pair) return;

			_view.ClearTexture();

			Matrix4x4 stepMatrix = BuildStepMatrix();
			Matrix4x4[] matrices = BuildRecursionMatrices(stepMatrix);
			Vector3 destinationForward = pair.transform.forward;
			Vector3 destinationPosition = pair.transform.position;

			// Find the deepest level where the pair portal is visible
			// We check each level and stop at the deepest one where pair is still visible
			int maxVisibleLevel = 0; // Always at least render level 0
			for (int i = matrices.Length - 1; i >= 0; i--) {
				if (CanSeePairPortal(matrices[i])) {
					maxVisibleLevel = i;
					break; // Found the deepest visible level, stop searching
				}
			}

			// Render from the deepest visible level down to level 0
			for (int i = maxVisibleLevel; i >= 0; i--) {
				_view.RenderLevel(context, mainCamera, matrices[i], destinationForward, destinationPosition);
			}
		}

		/// <summary>
		/// Builds the transformation matrix for a single portal traversal step.
		/// Combines source and destination portal transforms with mirror matrix.
		/// </summary>
		/// <returns>The step transformation matrix</returns>
		private Matrix4x4 BuildStepMatrix() {
			return PortalRecursionSolver.BuildStepMatrix(transform, pair.transform, _mirrorMatrix);
		}

		/// <summary>
		/// Checks if the pair portal is visible from a camera positioned at the given recursion level matrix.
		/// </summary>
		/// <param name="recursionLevelMatrix">The world matrix of the camera at this recursion level</param>
		/// <returns>True if the pair portal's renderer is visible from this recursion level, false otherwise</returns>
		private bool CanSeePairPortal(Matrix4x4 recursionLevelMatrix) {
			// Safety checks
			if (pair == null) return false;
			if (pair.RenderView == null) return false;
			var renderer = pair.RenderView.SurfaceRenderer;
			if (renderer == null) return false;

			// Extract camera position and rotation from the recursion level matrix
			Vector3 cameraPos = recursionLevelMatrix.MultiplyPoint(Vector3.zero);
			Vector3 cameraForward = recursionLevelMatrix.MultiplyVector(Vector3.forward).normalized;
			Vector3 cameraUp = recursionLevelMatrix.MultiplyVector(Vector3.up).normalized;

			// Check if pair portal is visible from this recursion level perspective
			return PortalVisibility.IsVisibleFromPosition(cameraPos, cameraForward, cameraUp, mainCamera, renderer);
		}

		/// <summary>
		/// Calculates all recursion level matrices from the starting camera position.
		/// Each matrix represents the camera transform at that recursion depth.
		/// </summary>
		/// <param name="stepMatrix">The transformation matrix for one portal traversal</param>
		/// <returns>Array of matrices for each recursion level</returns>
		private Matrix4x4[] BuildRecursionMatrices(Matrix4x4 stepMatrix) {
			Matrix4x4 startMatrix = mainCamera.transform.localToWorldMatrix;
			PortalRecursionSolver.Fill(_recursionMatrices, stepMatrix, startMatrix);
			return _recursionMatrices;
		}
	}
}