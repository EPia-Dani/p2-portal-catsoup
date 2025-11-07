using System.Collections.Generic;
using UnityEngine;

namespace Portal {
	public class PortalManager : MonoBehaviour {
		[Header("Portals")]
		[SerializeField] PortalRenderer bluePortal;
		[SerializeField] PortalRenderer orangePortal;
		[SerializeField] Transform bluePortalMesh;
		[SerializeField] Transform orangePortalMesh;

		[Header("Settings")]
		[SerializeField] int textureWidth = 1024;
		[SerializeField] int textureHeight = 1024;
		[SerializeField] int recursionLimit = 2;
		[SerializeField] int frameSkipInterval = 1;

		readonly Dictionary<PortalId, PortalSlot> _slots = new Dictionary<PortalId, PortalSlot>(2);

		public PortalRenderer BluePortal => bluePortal;
		public PortalRenderer OrangePortal => orangePortal;

		void Awake() {
			InitializeSlot(PortalId.Blue, bluePortal, bluePortalMesh);
			InitializeSlot(PortalId.Orange, orangePortal, orangePortalMesh);

			if (_slots.TryGetValue(PortalId.Blue, out PortalSlot blueSlot) && _slots.TryGetValue(PortalId.Orange, out PortalSlot orangeSlot)) {
				blueSlot.LinkPair(orangeSlot.Renderer);
				orangeSlot.LinkPair(blueSlot.Renderer);
			}
		}

		void Start() {
			if (PlayerPrefs.HasKey("PortalRecursion")) {
				recursionLimit = Mathf.Max(1, PlayerPrefs.GetInt("PortalRecursion"));
			}
			if (PlayerPrefs.HasKey("PortalFrameSkip")) {
				frameSkipInterval = Mathf.Max(1, PlayerPrefs.GetInt("PortalFrameSkip"));
			}

			ApplySettings();
		}

		void OnValidate() {
			recursionLimit = Mathf.Max(1, recursionLimit);
			frameSkipInterval = Mathf.Max(1, frameSkipInterval);
			if (Application.isPlaying && _slots.Count > 0) {
				ApplySettings();
			}
		}

		void Update() {
			UpdateRenderReadiness();
		}

		public void PlacePortal(PortalId id, Vector3 position, Vector3 normal, Vector3 right, Vector3 up, Collider surface, float scale = 1f) {
			if (!_slots.TryGetValue(id, out PortalSlot slot) || slot.Renderer == null) return;

			PortalState state = new PortalState {
				IsPlaced = true,
				Surface = surface,
				Position = position,
				Normal = normal.normalized,
				Right = right.normalized,
				Up = up.normalized,
				Scale = scale
			};

			slot.ApplyState(state);
			UpdatePortalVisualStates();
		}

		public void RemovePortal(PortalId id) {
			if (!_slots.TryGetValue(id, out PortalSlot slot)) return;

			slot.Clear();
			UpdatePortalVisualStates();
		}

		public bool TryGetState(PortalId id, out PortalState state) {
			if (_slots.TryGetValue(id, out PortalSlot slot) && slot.State.IsPlaced) {
				state = slot.State;
				return true;
			}

			state = PortalState.Empty;
			return false;
		}

		public PortalState GetState(PortalId id) {
			return _slots.TryGetValue(id, out PortalSlot slot) ? slot.State : PortalState.Empty;
		}

		public void SetRecursionLimit(int value) {
			recursionLimit = Mathf.Max(1, value);
			PlayerPrefs.SetInt("PortalRecursion", recursionLimit);
			ApplySettings();
		}

		public void SetFrameSkipInterval(int value) {
			frameSkipInterval = Mathf.Max(1, value);
			PlayerPrefs.SetInt("PortalFrameSkip", frameSkipInterval);
			ApplySettings();
		}

		void InitializeSlot(PortalId id, PortalRenderer renderer, Transform mesh) {
			if (renderer == null) return;
			_slots[id] = new PortalSlot(id, renderer, mesh);
		}

		void ApplySettings() {
			foreach (PortalSlot slot in _slots.Values) {
				slot?.Configure(textureWidth, textureHeight, recursionLimit, frameSkipInterval);
			}
		}

		void UpdateRenderReadiness() {
			foreach (PortalSlot slot in _slots.Values) {
				if (slot?.Animator == null || slot.Renderer == null) continue;
				bool ready = slot.Animator.IsOpening || slot.Animator.IsFullyOpen;
				slot.SetReadyToRender(ready);
			}
		}

		void UpdatePortalVisualStates() {
			bool bluePlaced = _slots.TryGetValue(PortalId.Blue, out PortalSlot blueSlot) && blueSlot.State.IsPlaced;
			bool orangePlaced = _slots.TryGetValue(PortalId.Orange, out PortalSlot orangeSlot) && orangeSlot.State.IsPlaced;
			bool bothPlaced = bluePlaced && orangePlaced;

			ApplyPortalState(PortalId.Blue, bluePlaced, bothPlaced);
			ApplyPortalState(PortalId.Orange, orangePlaced, bothPlaced);
		}

		void ApplyPortalState(PortalId id, bool placed, bool bothPlaced) {
			if (!_slots.TryGetValue(id, out PortalSlot slot)) return;

			slot.SetMeshActive(placed);

			bool shouldRender = placed && bothPlaced;
			slot.SetVisible(shouldRender);
			slot.UpdateAnimatorState(placed, bothPlaced);
		}
	}
}
