using UnityEngine;
using UnityEngine.UI;

namespace UI {
    public class Crosshair : MonoBehaviour {
        [Header("Crosshair Sprites")]
        [Tooltip("Sprite when no portal can be placed")] public Sprite emptySprite;
        [Tooltip("Sprite when only blue portal can be placed")] public Sprite blueOnlySprite;
        [Tooltip("Sprite when only orange portal can be placed")] public Sprite orangeOnlySprite;
        [Tooltip("Sprite when both portals can be placed")] public Sprite bothSprite;

        Image crosshairImage;

        void Awake() {
            crosshairImage = GetComponent<Image>();
        }

        public void SetState(bool canPlaceBlue, bool canPlaceOrange) {
            if (crosshairImage == null) crosshairImage = GetComponent<Image>();
            if (crosshairImage == null) return;

            Sprite spriteToShow = emptySprite;

            if (canPlaceBlue && canPlaceOrange) {
                spriteToShow = bothSprite;
            } else if (canPlaceBlue) {
                spriteToShow = blueOnlySprite;
            } else if (canPlaceOrange) {
                spriteToShow = orangeOnlySprite;
            }

            if (spriteToShow != null) {
                crosshairImage.enabled = true;
                crosshairImage.sprite = spriteToShow;
            } else {
                crosshairImage.enabled = false;
            }
        }

        void OnValidate() {
            if (crosshairImage == null) crosshairImage = GetComponent<Image>();
        }
    }
}
