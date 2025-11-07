using UnityEngine;

namespace Portal {
	public class PortalOverlapGuard {
		readonly PortalManager _manager;
		readonly PortalSizeController _sizeController;
		readonly float _clampSkin;

		public PortalOverlapGuard(PortalManager manager, PortalSizeController sizeController, float clampSkin) {
			_manager = manager;
			_sizeController = sizeController;
			_clampSkin = clampSkin;
		}

		public bool TryResolvePlacement(PortalId targetId, PortalPlacement placement, Vector2 initialLocalPos, out Vector2 resolvedLocalPos, out bool removeOtherPortal) {
			resolvedLocalPos = initialLocalPos;
			removeOtherPortal = false;

			if (_manager == null || _sizeController == null) {
				return true;
			}

			PortalId otherId = targetId.Other();
			if (!_manager.TryGetState(otherId, out PortalState otherState) || !otherState.IsPlaced) {
				return true;
			}

			if (otherState.Surface != placement.Surface || Vector3.Dot(placement.Normal, otherState.Normal) < 0.99f) {
				return true;
			}

			float otherScale = otherState.Scale <= 0f ? 1f : otherState.Scale;
			Vector2 otherHalfSize = _sizeController.ResolveHalfSize(otherScale);
			Vector2 currentHalfSize = _sizeController.CurrentHalfSize;

			Vector3 desiredPosition = placement.GetWorldPosition(resolvedLocalPos);
			if (!PortalSurfaceMath.PortalFitsOnSurface(placement.Surface, desiredPosition, placement.Normal, placement.Right, placement.Up, otherHalfSize, _sizeController.InitialHalfSize, _clampSkin)) {
				removeOtherPortal = true;
			}

			if (!removeOtherPortal) {
				if (!PortalSurfaceMath.PortalFitsOnSurface(otherState.Surface, otherState.Position, otherState.Normal, otherState.Right.normalized, otherState.Up.normalized, currentHalfSize, _sizeController.InitialHalfSize, _clampSkin)) {
					removeOtherPortal = true;
				}
			}

			Vector3 otherOffset = otherState.Position - placement.SurfaceCenter;
			Vector2 otherLocalPos = new Vector2(Vector3.Dot(otherOffset, placement.Right), Vector3.Dot(otherOffset, placement.Up));

			Vector2 minSeparation = new Vector2(
				Mathf.Max(currentHalfSize.x + otherHalfSize.x, 1e-3f),
				Mathf.Max(currentHalfSize.y + otherHalfSize.y, 1e-3f)
			);

			Vector2 dist = resolvedLocalPos - otherLocalPos;
			Vector2 distNormalized = new Vector2(
				dist.x / minSeparation.x,
				dist.y / minSeparation.y
			);

			float magnitude = distNormalized.magnitude;
			if (magnitude < 1.05f) {
				float k = magnitude > 1e-3f ? 1.05f / magnitude : 1f;
				resolvedLocalPos = otherLocalPos + new Vector2(distNormalized.x * minSeparation.x * k, distNormalized.y * minSeparation.y * k);
				resolvedLocalPos.x = Mathf.Clamp(resolvedLocalPos.x, -placement.ClampRange.x, placement.ClampRange.x);
				resolvedLocalPos.y = Mathf.Clamp(resolvedLocalPos.y, -placement.ClampRange.y, placement.ClampRange.y);

				dist = resolvedLocalPos - otherLocalPos;
				distNormalized = new Vector2(dist.x / minSeparation.x, dist.y / minSeparation.y);
				if (distNormalized.magnitude < 1.05f) {
					return false;
				}
			}

			return true;
		}
	}
}
