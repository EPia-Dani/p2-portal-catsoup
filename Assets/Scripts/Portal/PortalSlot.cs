using UnityEngine;

namespace Portal {
	public class PortalSlot {
		public PortalId Id { get; }
		public PortalRenderer Renderer { get; }
		public PortalAnimator Animator { get; }
		public Transform Mesh { get; }
		public PortalState State { get; private set; }

		readonly Vector3 _baseScale = Vector3.one;
		readonly Vector3 _meshBaseScale = Vector3.one;

		public PortalSlot(PortalId id, PortalRenderer renderer, Transform mesh) {
			Id = id;
			Renderer = renderer;
			Mesh = mesh;

			if (Renderer != null) {
				Animator = Renderer.GetComponent<PortalAnimator>() ?? Renderer.GetComponentInChildren<PortalAnimator>();
				Renderer.IsReadyToRender = false;
				_baseScale = Renderer.transform.localScale;
			}

			if (Mesh != null) {
				_meshBaseScale = Mesh.localScale;
				Mesh.gameObject.SetActive(false);
			}

			State = PortalState.Empty;
		}

		public void LinkPair(PortalRenderer pair) {
			if (Renderer != null) {
				Renderer.pair = pair;
			}
		}

		public void Configure(int textureWidth, int textureHeight, int recursionLimit, int frameSkipInterval) {
			Renderer?.ConfigurePortal(textureWidth, textureHeight, recursionLimit, frameSkipInterval);
		}

		public void ApplyState(PortalState state) {
			State = state;
			if (!state.IsPlaced || Renderer == null) return;

			Renderer.SetVisible(true);
			Renderer.transform.SetPositionAndRotation(state.Position, Quaternion.LookRotation(-state.Normal, state.Up));
			Renderer.transform.localScale = _baseScale;
			Renderer.SetWallCollider(state.Surface);

			if (Mesh != null) {
				Mesh.gameObject.SetActive(true);
				Mesh.localScale = new Vector3(_meshBaseScale.x * state.Scale, _meshBaseScale.y, _meshBaseScale.z * state.Scale);
			}
		}

		public void Clear() {
			State = PortalState.Empty;
			if (Renderer != null) {
				Renderer.SetVisible(false);
				Renderer.IsReadyToRender = false;
			}
			if (Animator != null) {
				Animator.HideImmediate();
			}
			if (Mesh != null) {
				Mesh.gameObject.SetActive(false);
			}
		}

		public void SetMeshActive(bool active) {
			if (Mesh != null) {
				Mesh.gameObject.SetActive(active);
			}
		}

		public void SetVisible(bool visible) {
			Renderer?.SetVisible(visible);
			if (!visible && Renderer != null) {
				Renderer.IsReadyToRender = false;
			}
		}

		public void UpdateAnimatorState(bool shouldPlay, bool shouldOpen) {
			if (Animator == null) return;

			Animator.HideImmediate();
			if (shouldPlay) {
				Animator.PlayAppear();
				if (shouldOpen) {
					Animator.StartOpening();
				}
			}
		}

		public void SetReadyToRender(bool ready) {
			if (Renderer != null) {
				Renderer.IsReadyToRender = ready;
			}
		}
	}
}
