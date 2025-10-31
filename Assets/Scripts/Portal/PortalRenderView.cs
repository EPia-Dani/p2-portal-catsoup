using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portal {
	public class PortalRenderView : MonoBehaviour
	{
		[SerializeField] private Camera portalCamera;
		[SerializeField] private MeshRenderer surfaceRenderer;
		private int textureWidth = 1024;
		private int textureHeight = 1024;
		private float clipPlaneOffset = 0.01f;

		private RenderTexture _renderTexture;
		private static CommandBuffer _sharedCommandBuffer;
		private bool _isVisible = true;
		
		// Cached objects to avoid per-frame allocations
		private Vector4 _cachedClipPlane;
		private Vector3 _cachedPosition;
		private Vector3 _cachedForward;
		private Vector3 _cachedUp;
		private Vector3 _cachedPlanePoint;
		private Vector3 _cachedCameraPlaneNormal;
		private Vector3 _cachedCameraPlanePoint;
		
		// Cached GameObject reference to avoid property access
		private GameObject _cachedCameraGameObject;

		public MeshRenderer SurfaceRenderer => surfaceRenderer;
		public bool IsVisible => _isVisible;

		public void Initialize()
		{
			if (!portalCamera) portalCamera = GetComponentInChildren<Camera>(true);
			if (!surfaceRenderer) surfaceRenderer = GetComponentInChildren<MeshRenderer>(true);

			if (!portalCamera)
			{
				Debug.LogError("PortalRenderView: Portal camera not found! Add a Camera component as a child or assign in inspector.", this);
			}
			if (!surfaceRenderer)
			{
				Debug.LogError("PortalRenderView: Surface renderer not found! Add a MeshRenderer component as a child or assign in inspector.", this);
			}

			// Cache camera GameObject reference
			if (portalCamera != null) _cachedCameraGameObject = portalCamera.gameObject;

			ConfigureCamera();
			EnsureRenderTexture();
		}

		/// <summary>
		/// Sets the texture size for the portal render texture
		/// </summary>
		public void SetTextureSize(int width, int height)
		{
			textureWidth = Mathf.Max(64, width);
			textureHeight = Mathf.Max(64, height);
			// Force render texture recreation with new settings
			if (_renderTexture != null)
			{
				_renderTexture.Release();
				Destroy(_renderTexture);
				_renderTexture = null;
			}
			EnsureRenderTexture();
		}

		/// <summary>
		/// Sets the clip plane offset for portal rendering
		/// </summary>
		public void SetClipPlaneOffset(float offset)
		{
			clipPlaneOffset = Mathf.Max(0.001f, offset);
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
			portalCamera.backgroundColor = Color.clear;
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

		public void ClearTexture()
		{
			if (_renderTexture == null) return;
			var prev = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = prev;
		}

		public void SetVisible(bool visible)
		{
			_isVisible = visible;
			if (_cachedCameraGameObject != null)
			{
				if (_cachedCameraGameObject.activeSelf != visible)
				{
					_cachedCameraGameObject.SetActive(visible);
				}
			}

			if (surfaceRenderer != null)
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

			// Use cached Vector3 fields to avoid allocations
			_cachedPosition = worldMatrix.MultiplyPoint(Vector3.zero);
			_cachedForward = worldMatrix.MultiplyVector(Vector3.forward);
			_cachedUp = worldMatrix.MultiplyVector(Vector3.up);

			portalCamera.transform.SetPositionAndRotation(_cachedPosition, Quaternion.LookRotation(_cachedForward, _cachedUp));

			// Calculate plane point using cached vector
			float offset = clipPlaneOffset;
			_cachedPlanePoint.x = destinationPosition.x + destinationForward.x * offset;
			_cachedPlanePoint.y = destinationPosition.y + destinationForward.y * offset;
			_cachedPlanePoint.z = destinationPosition.z + destinationForward.z * offset;
			
			Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
			
			// Calculate normal using cached vector (avoid temporary allocation from normalized)
			_cachedCameraPlaneNormal = worldToCamera.MultiplyVector(destinationForward);
			float normalMag = Mathf.Sqrt(_cachedCameraPlaneNormal.x * _cachedCameraPlaneNormal.x + 
			                             _cachedCameraPlaneNormal.y * _cachedCameraPlaneNormal.y + 
			                             _cachedCameraPlaneNormal.z * _cachedCameraPlaneNormal.z);
			if (normalMag > 1e-6f)
			{
				float invMag = -1f / normalMag;
				_cachedCameraPlaneNormal.x *= invMag;
				_cachedCameraPlaneNormal.y *= invMag;
				_cachedCameraPlaneNormal.z *= invMag;
			}
			
			_cachedCameraPlanePoint = worldToCamera.MultiplyPoint(_cachedPlanePoint);
			
			// Reuse cached Vector4 instead of allocating new one
			_cachedClipPlane.x = _cachedCameraPlaneNormal.x;
			_cachedClipPlane.y = _cachedCameraPlaneNormal.y;
			_cachedClipPlane.z = _cachedCameraPlaneNormal.z;
			_cachedClipPlane.w = -(_cachedCameraPlanePoint.x * _cachedCameraPlaneNormal.x + 
			                        _cachedCameraPlanePoint.y * _cachedCameraPlaneNormal.y + 
			                        _cachedCameraPlanePoint.z * _cachedCameraPlaneNormal.z);

			portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(_cachedClipPlane);

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
