using UnityEngine;

namespace Portal {
	public class PortalOverlapGuard {
		readonly PortalManager _manager;
		readonly float _minSeparation;

		public PortalOverlapGuard(PortalManager manager, PortalSizeController sizeController, float clampSkin) {
			_manager = manager;
			// Use a fixed minimum separation distance instead of size-based
			_minSeparation = 0.1f;
		}

		public bool TryResolvePlacement(PortalId targetId, PortalPlacement placement, Vector2 initialLocalPos, out Vector2 resolvedLocalPos, out bool removeOtherPortal) {
			resolvedLocalPos = initialLocalPos;
			removeOtherPortal = false;

			if (_manager == null) {
				return true;
			}

			PortalId otherId = targetId.Other();
			if (!_manager.TryGetState(otherId, out PortalState otherState) || !otherState.IsPlaced) {
				return true;
			}

			if (otherState.Surface != placement.Surface || Vector3.Dot(placement.Normal, otherState.Normal) < 0.99f) {
				return true;
			}

			Vector3 otherOffset = otherState.Position - placement.SurfaceCenter;
			Vector2 otherLocalPos = new Vector2(Vector3.Dot(otherOffset, placement.Right), Vector3.Dot(otherOffset, placement.Up));

			Vector2 dist = resolvedLocalPos - otherLocalPos;
			float distance = dist.magnitude;

			if (distance < _minSeparation) {
				if (distance < 1e-3f) {
					return false; // Too close, placement blocked
				}

				// Push away from other portal
				Vector2 direction = dist.normalized;
				resolvedLocalPos = otherLocalPos + direction * _minSeparation;
				resolvedLocalPos.x = Mathf.Clamp(resolvedLocalPos.x, -placement.ClampRange.x, placement.ClampRange.x);
				resolvedLocalPos.y = Mathf.Clamp(resolvedLocalPos.y, -placement.ClampRange.y, placement.ClampRange.y);

				// Check if still too close after clamping
				dist = resolvedLocalPos - otherLocalPos;
				if (dist.magnitude < _minSeparation) {
					return false;
				}
			}

			return true;
		}
	}
}
