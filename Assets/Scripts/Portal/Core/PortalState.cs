using UnityEngine;

namespace Portal {
	public struct PortalState {
		public bool IsPlaced;
		public Collider Surface;
		public Vector3 Position;
		public Vector3 Normal;
		public Vector3 Right;
		public Vector3 Up;
		public float Scale;

		public static PortalState Empty => new PortalState {
			IsPlaced = false,
			Surface = null,
			Position = Vector3.zero,
			Normal = Vector3.zero,
			Right = Vector3.zero,
			Up = Vector3.zero,
			Scale = 1f
		};
	}
}
