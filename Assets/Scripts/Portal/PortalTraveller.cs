using UnityEngine;

namespace Portal {
	/// <summary>
	/// Base component that marks an object as capable of traveling through portals.
	/// Handles collision ignoring and provides virtual methods for portal traversal logic.
	/// </summary>
	public abstract class PortalTraveller : MonoBehaviour {
		public Vector3 _lastPosition;
		protected Collider _collider;

		protected virtual void Awake() {
			_collider = GetComponent<Collider>();
		}

		/// <summary>
		/// Called when entering a portal trigger.
		/// </summary>
		public virtual void OnPortalEnter(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			_lastPosition = transform.position;
		}

		/// <summary>
		/// Called each frame while inside a portal trigger.
		/// </summary>
		public virtual void OnPortalStay(PortalTeleporter sourcePortal, PortalTeleporter destPortal) {
			// Detect crossing and handle teleportation
		}

		/// <summary>
		/// Called when exiting a portal trigger.
		/// </summary>
		public virtual void OnPortalExit(PortalTeleporter sourcePortal) {
			_lastPosition = Vector3.zero;
		}

		/// <summary>
		/// Temporarily ignores collisions with the specified collider.
		/// </summary>
		public void IgnoreCollisionWith(Collider collider, bool ignore) {
			if (_collider != null) {
				Physics.IgnoreCollision(_collider, collider, ignore);
			}
		}

		/// <summary>
		/// Check if this traveller has crossed the portal plane.
		/// </summary>
		protected bool HasCrossedPortalPlane(Vector3 planePoint, Vector3 planeNormal) {
			if (_lastPosition == Vector3.zero) {
				return false;
			}

			float prevDot = Vector3.Dot(_lastPosition - planePoint, planeNormal);
			float currDot = Vector3.Dot(transform.position - planePoint, planeNormal);

			return prevDot <= 0.01f && currDot > 0.01f;
		}

		/// <summary>
		/// Transforms position and rotation through a portal.
		/// </summary>
		protected void TransformThroughPortal(PortalRenderer sourceRenderer, PortalRenderer destRenderer, out Vector3 newPos, out Quaternion newRot) {
			Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, -1f));
			Matrix4x4 step = destRenderer.transform.localToWorldMatrix * mirror * sourceRenderer.transform.worldToLocalMatrix;
			Matrix4x4 worldMatrix = step * transform.localToWorldMatrix;

			newPos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			newRot = Quaternion.LookRotation(forward, up);
		}
	}
}

