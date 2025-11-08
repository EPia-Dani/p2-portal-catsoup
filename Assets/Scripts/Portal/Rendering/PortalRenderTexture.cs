// PortalRenderTexture.cs
// Manages render texture lifecycle for portal rendering

using UnityEngine;

namespace Portal.Rendering {
	public class PortalRenderTexture {
		private RenderTexture _texture;
		private Material _material;
		private Camera _portalCamera;
		
		private int _width;
		private int _height;
		
		// Dynamic sizing state
		private const float ResizeThreshold = 0.2f;
		private const int StableFrames = 6;
		private int _targetWidth;
		private int _targetHeight;
		private int _stableCounter;

		public RenderTexture Texture => _texture;
		public int Width => _width;
		public int Height => _height;

		public PortalRenderTexture(int width, int height, Material material, Camera portalCamera) {
			_width = width;
			_height = height;
			_material = material;
			_portalCamera = portalCamera;
			CreateTexture();
		}

		public void SetSize(int width, int height) {
			if (_width == width && _height == height) return;
			
			_width = width;
			_height = height;
			CreateTexture();
		}

		public void UpdateDynamicSize(Camera mainCamera, float screenCoverage, int recursionLevel) {
			if (mainCamera == null) return;

			float coverage = Mathf.Clamp01(screenCoverage);
			float coverageFactor = Mathf.Pow(coverage, 0.6f);
			float minQuality = 0.5f;
			float maxQuality = 1.5f;
			float qualityScale = Mathf.Lerp(minQuality, maxQuality, coverageFactor);

			float recursionBias = 1f + 0.1f * Mathf.Clamp(recursionLevel, 0, 5);
			float finalScale = Mathf.Clamp(qualityScale * recursionBias, minQuality, 2.5f);

			int targetW = GetClosestSizeTier(mainCamera.pixelWidth * finalScale);
			int targetH = GetClosestSizeTier(mainCamera.pixelHeight * finalScale);

			int minW = Mathf.Max(512, Mathf.RoundToInt(mainCamera.pixelWidth * minQuality));
			int minH = Mathf.Max(288, Mathf.RoundToInt(mainCamera.pixelHeight * minQuality));
			targetW = Mathf.Clamp(targetW, minW, 2048);
			targetH = Mathf.Clamp(targetH, minH, 2048);

			bool needsResize = (Mathf.Abs(targetW - _width) > _width * ResizeThreshold) ||
			                   (Mathf.Abs(targetH - _height) > _height * ResizeThreshold);

			if (!needsResize) {
				_targetWidth = _width;
				_targetHeight = _height;
				_stableCounter = 0;
				return;
			}

			if (_targetWidth != targetW || _targetHeight != targetH) {
				_targetWidth = targetW;
				_targetHeight = targetH;
				_stableCounter = 0;
			} else {
				_stableCounter++;
			}

			if (_stableCounter >= StableFrames) {
				SetSize(_targetWidth, _targetHeight);
				_stableCounter = 0;
			}
		}

		public void Clear() {
			if (_texture == null) return;
			
			var previousActive = RenderTexture.active;
			RenderTexture.active = _texture;
			GL.Clear(true, true, Color.clear);
			RenderTexture.active = previousActive;
		}

		public void Release() {
			if (_texture) {
				_texture.Release();
				Object.Destroy(_texture);
				_texture = null;
			}
		}

		private void CreateTexture() {
			Release();

			_texture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32) {
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear
			};
			_texture.Create();

			if (_portalCamera) {
				_portalCamera.targetTexture = _texture;
				_portalCamera.pixelRect = new Rect(0, 0, _width, _height);
			}

			if (_material) {
				_material.mainTexture = _texture;
			}
		}

		private int GetClosestSizeTier(float value) {
			int[] tiers = { 256, 384, 512, 640, 768, 896, 1024, 1280, 1536, 1792, 2048 };
			int best = tiers[0];
			float bestDistance = Mathf.Abs(value - best);

			for (int i = 1; i < tiers.Length; i++) {
				float distance = Mathf.Abs(value - tiers[i]);
				if (distance < bestDistance) {
					bestDistance = distance;
					best = tiers[i];
				}
			}

			return best;
		}
	}
}

