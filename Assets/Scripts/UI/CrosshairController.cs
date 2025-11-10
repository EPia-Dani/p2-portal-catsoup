using UnityEngine;
using UnityEngine.UI; // for Image/Sprite
using Portal; // for PortalManager and PortalId

namespace UI
{
    public class CrosshairController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The procedural crosshair component to control (optional if using sprites)")]
        public Crosshair crosshair;
        
        [Tooltip("Camera to raycast from (usually MainCamera)")]
        public Camera cam;

        [Tooltip("Portal manager used to query portal placement state")] 
        public PortalManager portalManager;
        
        [Header("Sprite Crosshair (2-state)")]
        [Tooltip("Use sprite-based crosshair (set Image and Sprites below)")]
        public bool useSprites = true;
        [Tooltip("UI Image to display the crosshair sprite")] public Image crosshairImage;
        [Tooltip("Sprite when no portal placed (empty)")] public Sprite emptySprite;
        [Tooltip("Sprite when at least one portal is placed")] public Sprite placedSprite;
        // Legacy optional fields; if placedSprite is not set we can fallback to these if assigned
        [Tooltip("(Optional) Fallback sprite for blue if placedSprite not set")] public Sprite bluePortalSprite;
        [Tooltip("(Optional) Fallback sprite for orange if placedSprite not set")] public Sprite orangePortalSprite;

        [Header("Procedural Crosshair Fallback")] 
        [Tooltip("Color when no portal placed (only used if not using sprites)")] public Color noPortalColor = new Color(1f,1f,1f,0.2f);
        [Tooltip("Color when any portal placed (only used if not using sprites)")] public Color placedColor = Color.white;

        [Header("Expansion Settings")] 
        [Tooltip("Default gap when not looking at anything (procedural crosshair)")]
        public float defaultGap = 5f;
        
        [Tooltip("Expanded gap when looking at a grabbable object (procedural crosshair)")]
        public float expandedGap = 15f;
        
        [Tooltip("Max distance to detect grabbable objects")]
        public float detectionRange = 4f;
        
        [Tooltip("LayerMask for detection")]
        public LayerMask detectionMask = ~0;
        
        // Currently detected grabbable object (can be null)
        private PlayerPickup _currentGrabbable;
        
        public PlayerPickup CurrentGrabbable => _currentGrabbable;
        public bool IsLookingAtGrabbable => _currentGrabbable != null;
        
        void Awake()
        {
            if (crosshair == null)
                crosshair = GetComponent<Crosshair>();
            
            if (cam == null)
                cam = Camera.main;

            if (!portalManager)
                portalManager = FindObjectOfType<PortalManager>();
        }
        
        void Update()
        {
            CheckForGrabbableObject();
            UpdatePortalCrosshair();
        }
        
        void CheckForGrabbableObject()
        {
            if (cam == null)
            {
                _currentGrabbable = null;
                return;
            }
            
            // Raycast from center of screen
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, detectionMask))
            {
                var grabbable = hit.collider.GetComponent<PlayerPickup>();
                if (grabbable != null)
                {
                    _currentGrabbable = grabbable;
                    if (crosshair)
                        crosshair.SetGap(expandedGap);
                    return;
                }
            }
            
            _currentGrabbable = null;
            if (crosshair)
                crosshair.SetGap(defaultGap);
        }

        void UpdatePortalCrosshair()
        {
            bool bluePlaced = false;
            bool orangePlaced = false;

            if (portalManager)
            {
                bluePlaced = portalManager.TryGetState(PortalId.Blue, out var _);
                orangePlaced = portalManager.TryGetState(PortalId.Orange, out var _);
            }

            bool anyPlaced = bluePlaced || orangePlaced;

            if (useSprites && crosshairImage)
            {
                crosshairImage.enabled = true;
                crosshairImage.sprite = anyPlaced 
                    ? (placedSprite != null 
                        ? placedSprite 
                        : (bluePortalSprite != null ? bluePortalSprite : orangePortalSprite))
                    : emptySprite;
                if (crosshair) crosshair.SetVisible(false); // hide procedural lines when using sprites
                return;
            }

            // Procedural fallback
            if (crosshair)
            {
                crosshair.SetVisible(true);
                crosshair.SetColor(anyPlaced ? placedColor : noPortalColor);
            }
        }
    }
}
