using UnityEngine;

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

		public void Configure(int width, int height) {
			if (width <= 0 || height <= 0) return;
			if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return;

			Release();

			_renderTexture = new RenderTexture(width, height, depthBufferBits, textureFormat) {
				filterMode = filterMode,
				wrapMode = wrapMode
			};
			_renderTexture.Create();

			RebindTargets();
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

		void RebindTargets() {
			if (_boundCamera) {
				BindCamera(_boundCamera);
			}

			if (_boundMaterial) {
				BindMaterial(_boundMaterial);
			}
		}
	}
}
