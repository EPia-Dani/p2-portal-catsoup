// PortalCloneSystem.cs - Simple visual clone system for portal preview
using UnityEngine;

namespace Portal {
	public class PortalCloneSystem : MonoBehaviour {
		private GameObject _clone;
		private PortalRenderer _currentPortal;
		private PortalRenderer _currentDestination;
		private Vector3 _previousOffsetFromPortal;

		void Update() {
			CheckForPortalContact();
			if (_clone && _currentPortal && _currentDestination) {
				UpdateCloneTransform();
				CheckForPortalCrossing();
			}
		}

		void CheckForPortalContact() {
			Collider[] nearby = Physics.OverlapSphere(transform.position, 0.5f);
			
			PortalRenderer touchingPortal = null;
			foreach (var col in nearby) {
				var portal = col.GetComponent<PortalRenderer>();
				if (portal != null && portal.pair != null) {
					touchingPortal = portal;
					break;
				}
			}
			
			if (touchingPortal != null && _clone == null) {
				CreateClone(touchingPortal);
			} else if (touchingPortal == null && _clone != null) {
				DestroyClone();
			}
		}

		void CheckForPortalCrossing() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			if (PlayerPickup.IsObjectHeld(gameObject)) return;

			Vector3 offsetFromPortal = transform.position - _currentPortal.transform.position;
			float sidePrev = Mathf.Sign(Vector3.Dot(_previousOffsetFromPortal, _currentPortal.transform.forward));
			float sideNow = Mathf.Sign(Vector3.Dot(offsetFromPortal, _currentPortal.transform.forward));

			if (sideNow > 0 && sidePrev < 0) {
				SwapWithClone();
			} else {
				_previousOffsetFromPortal = offsetFromPortal;
			}
		}

		void CreateClone(PortalRenderer portal) {
			if (!portal.pair || _clone != null) return;
			
			_currentPortal = portal;
			_currentDestination = portal.pair;
			
			_previousOffsetFromPortal = transform.position - portal.transform.position;

			_clone = new GameObject($"{gameObject.name}_Clone");
			_clone.transform.SetParent(null);
			
			CopyMesh(gameObject, _clone);
			UpdateCloneTransform();
		}

		void UpdateCloneTransform() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			float scaleRatio = _currentDestination.PortalScale / _currentPortal.PortalScale;
			
			PortalTransformUtility.TransformThroughPortal(_currentPortal, _currentDestination,
				transform.position, transform.rotation, scaleRatio,
				out Vector3 newPos, out Quaternion newRot);
			
			_clone.transform.SetPositionAndRotation(newPos, newRot);
			_clone.transform.localScale = transform.localScale * scaleRatio;
		}

		void CopyMesh(GameObject source, GameObject target) {
			var renderers = source.GetComponentsInChildren<Renderer>();
			foreach (var renderer in renderers) {
				if (renderer is MeshRenderer meshRenderer) {
					var filter = renderer.GetComponent<MeshFilter>();
					if (filter && filter.sharedMesh) {
						var cloneObj = new GameObject(renderer.name);
						cloneObj.transform.SetParent(target.transform);
						cloneObj.transform.localPosition = renderer.transform.localPosition;
						cloneObj.transform.localRotation = renderer.transform.localRotation;
						cloneObj.transform.localScale = renderer.transform.localScale;
						
						var cloneFilter = cloneObj.AddComponent<MeshFilter>();
						cloneFilter.sharedMesh = filter.sharedMesh;
						
						var cloneRenderer = cloneObj.AddComponent<MeshRenderer>();
						cloneRenderer.sharedMaterials = meshRenderer.sharedMaterials;
					}
				}
			}
		}

		/// <summary>
		/// Called when object is dropped - swap with clone if object is on the other side of portal
		/// </summary>
		public void OnDropped() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			
			// Check which side of the portal we're on
			Vector3 offsetFromPortal = transform.position - _currentPortal.transform.position;
			float dot = Vector3.Dot(offsetFromPortal, _currentPortal.transform.forward);
			
			// If dot > 0, we're on the "exiting" side (where clone is) - swap with clone
			if (dot > 0.01f) {
				SwapWithClone();
			}
		}

		/// <summary>
		/// Called when player teleports while holding this object - swap with clone if it exists
		/// </summary>
		public void OnPlayerTeleport() {
			if (!_clone || !_currentPortal || !_currentDestination) return;
			SwapWithClone();
		}

		void SwapWithClone() {
			if (!_clone || !_currentPortal || !_currentDestination) return;

			float scaleRatio = _currentDestination.PortalScale / _currentPortal.PortalScale;
			var traveller = GetComponent<PortalTraveller>();
			var handler = _currentPortal.GetComponent<PortalTravellerHandler>();

			if (handler && traveller) {
				handler.TeleportTraveller(traveller, _currentDestination, scaleRatio);
			} else {
				Vector3 clonePos = _clone.transform.position;
				Quaternion cloneRot = _clone.transform.rotation;

				if (traveller) {
					traveller.Teleport(_currentPortal.transform, _currentDestination.transform, clonePos, cloneRot, scaleRatio);
				} else {
					transform.SetPositionAndRotation(clonePos, cloneRot);
					transform.localScale *= scaleRatio;
				}
			}

			// Destroy clone - swap complete
			DestroyClone();
		}

		/// <summary>
		/// Called when object teleports via PortalTravellerHandler - destroy clone since teleportation happened
		/// </summary>
		public void OnObjectTeleported(Transform fromPortal, Transform toPortal) {
			DestroyClone();
		}

		void DestroyClone() {
			if (_clone) {
				Destroy(_clone);
				_clone = null;
			}
			_currentPortal = null;
			_currentDestination = null;
			_previousOffsetFromPortal = Vector3.zero;
		}

		void OnDestroy() {
			DestroyClone();
		}
	}
}
