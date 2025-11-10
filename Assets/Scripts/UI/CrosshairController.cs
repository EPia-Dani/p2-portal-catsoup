using UnityEngine;
using UnityEngine.UI;
using Portal;

namespace UI {
    public class CrosshairController : MonoBehaviour {
        [Header("Portal Crosshair (2-State)")] 
        [Tooltip("Portal manager to query portal placement state")] public PortalManager portalManager;
        [Tooltip("UI Image displaying the crosshair sprite")] public Image crosshairImage;
        [Tooltip("Sprite when no portal placed (void)")] public Sprite emptySprite;
        [Tooltip("Sprite when at least one portal placed")] public Sprite placedSprite;
        [Tooltip("(Optional) Procedural Crosshair component to sync state with")] public Crosshair crosshair;

        void Awake() {
            if (!portalManager) {
                #if UNITY_2023_1_OR_NEWER
                portalManager = FindFirstObjectByType<PortalManager>();
                #else
                portalManager = FindObjectOfType<PortalManager>();
                #endif
            }
            if (!crosshairImage) crosshairImage = GetComponent<Image>();
            if (!crosshair) crosshair = GetComponent<Crosshair>();
        }

        void Update() {
            UpdatePortalState();
        }

        void UpdatePortalState() {
            if (!crosshairImage) return;
            bool anyPlaced = false;
            if (portalManager) {
                anyPlaced = portalManager.TryGetState(PortalId.Blue, out var _) || portalManager.TryGetState(PortalId.Orange, out var _);
            }
            crosshairImage.enabled = true; // Always visible in this simplified version
            crosshairImage.sprite = anyPlaced ? placedSprite : emptySprite;
            if (crosshair) crosshair.SetPlaced(anyPlaced); // keep Crosshair component in sync
        }
    }
}
