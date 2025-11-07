using UnityEngine;

namespace Portal {
	public class PortalSizeController {
		readonly Vector2 _initialHalfSize;
		readonly float _minPortalSize;
		readonly float _maxPortalSize;
		readonly float _resizeSpeed;
		readonly float _aspectRatio;

		float BaseHeight => _initialHalfSize.y > 0.001f ? _initialHalfSize.y : _initialHalfSize.x;

		public float CurrentScale { get; private set; } = 1f;
		public Vector2 CurrentHalfSize { get; private set; }
		public Vector2 PlacementHalfSize => PortalSurfaceMath.GetEffectiveHalfSize(CurrentHalfSize, _initialHalfSize);
		public Vector2 InitialHalfSize => _initialHalfSize;

		public PortalSizeController(Vector2 initialHalfSize, float minPortalSize, float maxPortalSize, float resizeSpeed) {
			_initialHalfSize = initialHalfSize;
			_minPortalSize = minPortalSize;
			_maxPortalSize = Mathf.Max(maxPortalSize, minPortalSize);
			_resizeSpeed = resizeSpeed;
			_aspectRatio = initialHalfSize.y > 0.001f ? initialHalfSize.x / initialHalfSize.y : 1f;
			RecalculateHalfSize();
		}

		public bool TryUpdateScale(float scrollDelta) {
			if (Mathf.Abs(scrollDelta) <= 0.001f) return false;

			float minScale = _minPortalSize / BaseHeight;
			float maxScale = _maxPortalSize / BaseHeight;
			float newScale = Mathf.Clamp(CurrentScale + scrollDelta * _resizeSpeed, minScale, maxScale);
			if (Mathf.Approximately(newScale, CurrentScale)) {
				return false;
			}

			CurrentScale = newScale;
			RecalculateHalfSize();
			return true;
		}

		public Vector2 ResolveHalfSize(float scale) {
			float scaledHeight = _initialHalfSize.y * Mathf.Max(scale, 0.001f);
			float scaledWidth = scaledHeight * _aspectRatio;
			Vector2 target = new Vector2(scaledWidth, scaledHeight);
			return PortalSurfaceMath.GetEffectiveHalfSize(target, _initialHalfSize);
		}

		void RecalculateHalfSize() {
			float scaledHeight = _initialHalfSize.y * CurrentScale;
			float scaledWidth = scaledHeight * _aspectRatio;
			CurrentHalfSize = new Vector2(scaledWidth, scaledHeight);
		}
	}
}
