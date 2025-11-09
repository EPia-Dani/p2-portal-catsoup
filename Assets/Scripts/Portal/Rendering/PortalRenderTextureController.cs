using UnityEngine;
using UnityEngine.Rendering;

namespace Portal {
	[DisallowMultipleComponent]
	public class PortalRenderTextureController : MonoBehaviour {
		[SerializeField] private RenderTextureFormat textureFormat = RenderTextureFormat.ARGB32;
		[SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
		[SerializeField] private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
		[SerializeField] private int depthBufferBits = 24;

		private RenderTexture _renderTexture;
		private Camera _boundCamera;
		private Material _boundMaterial;

		public RenderTexture Texture => _renderTexture;
		public RenderTextureFormat TextureFormat => textureFormat;
		public FilterMode TextureFilterMode => filterMode;
		public TextureWrapMode TextureWrapMode => wrapMode;
		public int DepthBufferBits => depthBufferBits;

		public void Configure(int width, int height) {
			if (width <= 0 || height <= 0) return;
			if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;

			if (_renderTexture != null) {
				if (_boundCamera && _boundCamera.targetTexture == _renderTexture) {
					_boundCamera.targetTexture = null;
				}
				_renderTexture.Release();
				Destroy(_renderTexture);
			}

			_renderTexture = new RenderTexture(width, height, depthBufferBits, textureFormat);
			_renderTexture.filterMode = filterMode;
			_renderTexture.wrapMode = wrapMode;
			_renderTexture.Create();

			if (_boundCamera) {
				_boundCamera.targetTexture = _renderTexture;
				_boundCamera.pixelRect = new Rect(0, 0, width, height);
			}
			if (_boundMaterial) {
				_boundMaterial.mainTexture = _renderTexture;
			}
		}

		public void BindCamera(Camera portalCamera) {
			_boundCamera = portalCamera;
			if (_boundCamera && _renderTexture) {
				_boundCamera.targetTexture = _renderTexture;
				_boundCamera.pixelRect = new Rect(0, 0, _renderTexture.width, _renderTexture.height);
			}
		}

		public void BindMaterial(Material material) {
			_boundMaterial = material;
			if (_boundMaterial && _renderTexture) {
				_boundMaterial.mainTexture = _renderTexture;
			}
		}

		public void Clear(Color color) {
			if (_renderTexture == null) return;
			var previous = RenderTexture.active;
			RenderTexture.active = _renderTexture;
			GL.Clear(true, true, color);
			RenderTexture.active = previous;
		}

		public RenderTextureDescriptor CreateDescriptor(int width, int height) {
			var descriptor = new RenderTextureDescriptor(width, height, textureFormat, depthBufferBits) {
				msaaSamples = 1,
				useMipMap = false,
				autoGenerateMips = false,
				sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
			};
			return descriptor;
		}

		public void ApplySettings(RenderTexture texture) {
			if (!texture) return;
			texture.filterMode = filterMode;
			texture.wrapMode = wrapMode;
		}

		public void Release() {
			if (_renderTexture != null) {
				if (_boundCamera && _boundCamera.targetTexture == _renderTexture) {
					_boundCamera.targetTexture = null;
				}
				_renderTexture.Release();
				Destroy(_renderTexture);
				_renderTexture = null;
			}
		}

		void OnDestroy() {
			Release();
		}
	}
}
