using UnityEngine;

namespace Portal {
	public static class PortalVisibility {
		private static readonly Plane[] Planes = new Plane[6];
		private static Camera _tempCamera;
		private static readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
		
		private const float BackfaceThreshold = 0.1f;
		private const float OcclusionRayDistance = 1000f;

		/// <summary>
		/// Visibility check for main camera: frustum culling, backface culling, and occlusion.
		/// </summary>
		/// <param name="excludeTransform">Transform to exclude from occlusion checks (e.g., pair portal)</param>
		public static bool IsVisible(Camera camera, Renderer renderer, Transform excludeTransform = null) {
			if (camera == null || renderer == null) return false;
			
			// 1. Frustum culling
			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			if (!GeometryUtility.TestPlanesAABB(Planes, renderer.bounds)) {
				return false;
			}
			
			// 2. Backface culling (portal forward points INTO surface)
			if (!IsFacingCamera(camera, renderer)) {
				return false;
			}
			
			// 3. Occlusion check (exclude pair portal from occlusion)
			if (IsOccluded(camera, renderer, excludeTransform)) {
				return false;
			}
			
			return true;
		}

		/// <summary>
		/// Visibility check from arbitrary position/orientation (for recursion levels).
		/// Skips backface culling because recursion uses mirrored coordinate systems.
		/// Skips occlusion for recursion to avoid false positives when looking through portals.
		/// </summary>
		public static bool IsVisibleFromPosition(
			Vector3 cameraPosition,
			Vector3 cameraForward,
			Vector3 cameraUp,
			Camera referenceCamera,
			Renderer renderer) {
			if (referenceCamera == null || renderer == null) return false;

			if (_tempCamera == null) {
				GameObject tempObj = new GameObject("TempVisibilityCamera") {
					hideFlags = HideFlags.HideAndDontSave
				};
				_tempCamera = tempObj.AddComponent<Camera>();
				_tempCamera.enabled = false;
			}

			_tempCamera.fieldOfView = referenceCamera.fieldOfView;
			_tempCamera.aspect = referenceCamera.aspect;
			_tempCamera.nearClipPlane = referenceCamera.nearClipPlane;
			_tempCamera.farClipPlane = referenceCamera.farClipPlane;
			_tempCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(cameraForward, cameraUp));

			// For recursion: frustum culling only (no backface, no occlusion)
			// Occlusion causes issues in recursion because we're looking through portals
			GeometryUtility.CalculateFrustumPlanes(_tempCamera, Planes);
			return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
		}
		
		/// <summary>
		/// Backface culling: returns false if portal back is facing camera.
		/// Uses plane-sidedness check: camera should be on the "front" side of the portal plane.
		/// Portal forward points INTO the surface, so we check if camera is on the opposite side.
		/// </summary>
		private static bool IsFacingCamera(Camera camera, Renderer renderer) {
			if (renderer.transform == null) return true;
			
			Transform portalTransform = renderer.transform;
			Vector3 cameraPos = camera.transform.position;
			Vector3 portalPos = portalTransform.position;
			Vector3 portalForward = portalTransform.forward;
			
			// Vector from portal to camera
			Vector3 toCamera = cameraPos - portalPos;
			
			// Portal forward points INTO surface, so if camera is in front (visible side),
			// toCamera and portalForward should point in opposite directions (negative dot)
			float dot = Vector3.Dot(portalForward, toCamera.normalized);
			
			// Negative dot means camera is on the visible side (opposite of forward direction)
			return dot < BackfaceThreshold;
		}
		
		/// <summary>
		/// Occlusion check using raycasts from camera to portal bounds.
		/// Checks center and corners to detect if geometry blocks the view.
		/// </summary>
		/// <param name="excludeTransform">Transform to exclude from occlusion (e.g., pair portal)</param>
		private static bool IsOccluded(Camera camera, Renderer renderer, Transform excludeTransform = null) {
			Vector3 cameraPos = camera.transform.position;
			Bounds bounds = renderer.bounds;
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			
			// Sample points: center + 4 corners
			Vector3[] targets = {
				center,
				center + new Vector3(extents.x, extents.y, extents.z),
				center + new Vector3(-extents.x, extents.y, extents.z),
				center + new Vector3(extents.x, -extents.y, extents.z),
				center + new Vector3(-extents.x, -extents.y, extents.z)
			};
			
			int visibleRays = 0;
			Transform rendererTransform = renderer.transform;
			
			foreach (Vector3 target in targets) {
				Vector3 direction = target - cameraPos;
				float distance = direction.magnitude;
				
				if (distance < 0.01f) {
					visibleRays++;
					continue;
				}
				
				direction.Normalize();
				
				// Raycast to check for occluders
				int hitCount = Physics.RaycastNonAlloc(
					cameraPos,
					direction,
					_hitBuffer,
					Mathf.Min(distance, OcclusionRayDistance),
					-1,
					QueryTriggerInteraction.Ignore
				);
				
				// Check if we hit the portal itself or excluded transform (that's not occlusion)
				bool hitExcluded = false;
				for (int i = 0; i < hitCount; i++) {
					Transform hitTransform = _hitBuffer[i].transform;
					if (hitTransform == rendererTransform || 
					    hitTransform.IsChildOf(rendererTransform) ||
					    (excludeTransform != null && (hitTransform == excludeTransform || hitTransform.IsChildOf(excludeTransform)))) {
						hitExcluded = true;
						break;
					}
				}
				
				if (hitExcluded) {
					// Hit excluded transform, consider visible
					visibleRays++;
				} else if (hitCount > 0) {
					// Check if any hit is closer than target (that's occlusion)
					float targetDistance = Vector3.Distance(cameraPos, target);
					bool occluded = false;
					for (int i = 0; i < hitCount; i++) {
						if (_hitBuffer[i].distance < targetDistance - 0.1f) {
							occluded = true;
							break;
						}
					}
					if (!occluded) {
						visibleRays++;
					}
				} else {
					// No hits, visible
					visibleRays++;
				}
			}
			
			// Consider visible if at least 50% of rays reach
			return visibleRays < (targets.Length / 2);
		}
	}
}