using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	public class PortalRenderView : MonoBehaviour
	{
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;
		[SerializeField] private int textureWidth = 1024;
		[SerializeField] private int textureHeight = 1024;
		[SerializeField] private float clipPlaneOffset = 0.01f;

		private RenderTexture _renderTexture;
		private static CommandBuffer _sharedCommandBuffer;
		private bool _isVisible = true;

		public MeshRenderer SurfaceRenderer => surfaceRenderer;
		public bool IsVisible => _isVisible;

		public void Initialize()
		{
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			ConfigureCamera();
			EnsureRenderTexture();
		}

		public void ConfigureCamera()
		{
			if (!portalCamera) return;

			portalCamera.enabled = false;
			portalCamera.forceIntoRenderTexture = true;
			portalCamera.allowHDR = false;
			portalCamera.useOcclusionCulling = false;
			portalCamera.depthTextureMode = DepthTextureMode.None;
			portalCamera.clearFlags = CameraClearFlags.SolidColor;
			portalCamera.backgroundColor = Color.black;
			portalCamera.stereoTargetEye = StereoTargetEyeMask.None;

			var extra = portalCamera.GetUniversalAdditionalCameraData();
			if (extra != null)
			{
				extra.renderPostProcessing = false;
				extra.antialiasing = AntialiasingMode.None;
				extra.requiresColorOption = CameraOverrideOption.On;
				extra.requiresDepthOption = CameraOverrideOption.On;
				extra.SetRenderer(0);
			}
		}

		public void EnsureRenderTexture()
		{
			if (portalCamera == null) return;

			if (_renderTexture == null || _renderTexture.width != textureWidth || _renderTexture.height != textureHeight)
			{
				if (_renderTexture != null)
				{
					_renderTexture.Release();
					Destroy(_renderTexture);
				}

				_renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
				{
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Bilinear
				};
				_renderTexture.Create();
			}

			portalCamera.targetTexture = _renderTexture;
			if (surfaceRenderer)
			{
				surfaceRenderer.sharedMaterial.mainTexture = _renderTexture;
			}
		}

		public void SetVisible(bool visible)
		{
			_isVisible = visible;
			if (portalCamera)
			{
				var cameraObject = portalCamera.gameObject;
				if (cameraObject.activeSelf != visible)
				{
					cameraObject.SetActive(visible);
				}
			}

			if (surfaceRenderer)
			{
				surfaceRenderer.enabled = visible;
			}
		}

		public void RenderLevel(
			ScriptableRenderContext context,
			Camera mainCamera,
			Matrix4x4 worldMatrix,
			Vector3 destinationForward,
			Vector3 destinationPosition)
		{
			if (portalCamera == null || mainCamera == null || !_isVisible) return;

			EnsureRenderTexture();

			Vector3 position = worldMatrix.MultiplyPoint(Vector3.zero);
			Vector3 forward = worldMatrix.MultiplyVector(Vector3.forward);
			Vector3 up = worldMatrix.MultiplyVector(Vector3.up);

			portalCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

			Vector3 planePoint = destinationPosition + destinationForward * clipPlaneOffset;
			Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
			Vector3 cameraPlaneNormal = -worldToCamera.MultiplyVector(destinationForward).normalized;
			Vector3 cameraPlanePoint = worldToCamera.MultiplyPoint(planePoint);
			Vector4 clipPlane = new Vector4(
				cameraPlaneNormal.x,
				cameraPlaneNormal.y,
				cameraPlaneNormal.z,
				-Vector3.Dot(cameraPlanePoint, cameraPlaneNormal)
			);

			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlane);

			context.ExecuteCommandBuffer(GetCommandBuffer());
#pragma warning disable CS0618
			UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
#pragma warning restore CS0618
			portalCamera.ResetProjectionMatrix();
		}

		private static CommandBuffer GetCommandBuffer()
		{
			if (_sharedCommandBuffer == null)
			{
				_sharedCommandBuffer = new CommandBuffer { name = "PortalRenderView.Dummy" };
			}
			return _sharedCommandBuffer;
		}
	}
}
