using UnityEngine;
using UnityEngine.UI;

namespace UI {
    public class Crosshair : MonoBehaviour {
        [Header("Two-State Crosshair (Void / Placed)")] 
        [Tooltip("Optional Image used for a single crosshair sprite")] public Image crosshairImage;
        [Tooltip("Sprite when no portal placed (void)")] public Sprite emptySprite;
        [Tooltip("Sprite when at least one portal placed")] public Sprite placedSprite;
        [Tooltip("Color when no portal placed if sprites are missing")] public Color emptyColor = new Color(1f,1f,1f,0.2f);
        [Tooltip("Color when placed if sprites are missing")] public Color placedColor = Color.white;

        public Color crosshairColor = Color.white; // maps to current state color

        void Awake() {
            if (!crosshairImage) crosshairImage = GetComponent<Image>();
        }
        public void SetPlaced(bool isPlaced) {
            if (crosshairImage) {
                crosshairImage.enabled = true;
                if (placedSprite && emptySprite) {
                    crosshairImage.sprite = isPlaced ? placedSprite : emptySprite;
                } else {
                    crosshairImage.color = isPlaced ? placedColor : emptyColor;
                }
            }
            crosshairColor = isPlaced ? placedColor : emptyColor;
        } 
        public void SetColor(Color color) {
            crosshairColor = color;
            if (crosshairImage && (!placedSprite || !emptySprite)) {
                crosshairImage.color = color;
            }
        } 
        public void SetVisible(bool visible) {
            if (crosshairImage) crosshairImage.enabled = visible;
        }
        public float gapFromCenter = 0f;
        public float targetGap = 0f;
        public void SetGap(float gap, bool immediate = false) { /* no-op in simplified version */ }
        public void Expand(float amount) { /* no-op */ }
        public void Contract(float amount) { /* no-op */ }
        public void ResetGap() { /* no-op */ }

        void OnValidate() {
            if (!crosshairImage) crosshairImage = GetComponent<Image>();
        }
    }
}