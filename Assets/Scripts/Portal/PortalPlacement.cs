using UnityEngine;

namespace Portal {
	public struct PortalPlacement {
		public Collider Surface;
		public Vector3 Normal;
		public Vector3 Up;
		public Vector3 Right;
		public Vector3 SurfaceCenter;
		public Vector2 ClampRange;
		public Vector2 LocalPosition;

		public bool IsValid => Surface != null;
		public Vector3 Position => GetWorldPosition(LocalPosition);

		public Vector3 GetWorldPosition(Vector2 local) {
			return SurfaceCenter + Right * local.x + Up * local.y;
		}
	}
}
