// PortalRenderView.cs
// Manages portal camera and rendering operations

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal.Rendering {
	public class PortalRenderView {
		private readonly Camera _portalCamera;
		private readonly Camera _mainCamera;

		public PortalRenderView(Camera portalCamera, Camera mainCamera) {
			_portalCamera = portalCamera;
			_mainCamera = mainCamera;
			SetupCamera();
		}

		public void SetupCamera() {
			if (!_portalCamera) return;

			_portalCamera.enabled = false;
			_portalCamera.forceIntoRenderTexture = true;
			_portalCamera.allowHDR = false;
			_portalCamera.clearFlags = CameraClearFlags.SolidColor;
			_portalCamera.backgroundColor = Color.black;
			_portalCamera.useOcclusionCulling = false;

			var urpData = _portalCamera.GetUniversalAdditionalCameraData();
			if (urpData != null) {
				urpData.renderPostProcessing = false;
				urpData.antialiasing = AntialiasingMode.None;
				urpData.requiresColorOption = CameraOverrideOption.On;
				urpData.requiresDepthOption = CameraOverrideOption.On;
				urpData.SetRenderer(0);
			}
		}

		public void RenderLevel(Matrix4x4 worldMatrix, Vector3 exitPosition, Vector3 exitForward, RenderTexture targetTexture) {
			if (!_portalCamera || !_mainCamera) return;

			// Set camera transform
			Vector3 pos = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);
			_portalCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, up));

			// Ensure pixel rect matches render texture
			if (targetTexture != null) {
				_portalCamera.pixelRect = new Rect(0, 0, targetTexture.width, targetTexture.height);
			}

			// Calculate oblique clipping plane
			Vector3 planePoint = exitPosition + exitForward * 0.001f;
			Matrix4x4 worldToCamera = _portalCamera.worldToCameraMatrix;
			Vector3 normalCamera = Vector3.Normalize(-worldToCamera.MultiplyVector(exitForward));
			Vector3 pointCamera = worldToCamera.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(normalCamera.x, normalCamera.y, normalCamera.z, -Vector3.Dot(pointCamera, normalCamera));

			// Apply oblique projection and render
			_portalCamera.projectionMatrix = _mainCamera.CalculateObliqueMatrix(clipPlane);
			RenderPipeline.SubmitRenderRequest(_portalCamera, new UniversalRenderPipeline.SingleCameraRequest());
			_portalCamera.ResetProjectionMatrix();
		}

		public void SetVisible(bool visible) {
			if (_portalCamera) {
				_portalCamera.gameObject.SetActive(visible);
			}
		}
	}
}

