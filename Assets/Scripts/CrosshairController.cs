using UnityEngine;


namespace UI
{
    public class CrosshairController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The crosshair component to control")]
        public Crosshair crosshair;
        
        [Tooltip("Camera to raycast from (usually MainCamera)")]
        public Camera cam;
        
        [Header("Expansion Settings")]
        [Tooltip("Default gap when not looking at anything")]
        public float defaultGap = 5f;
        
        [Tooltip("Expanded gap when looking at a grabbable object")]
        public float expandedGap = 15f;
        
        [Tooltip("Max distance to detect grabbable objects")]
        public float detectionRange = 4f;
        
        [Tooltip("LayerMask for detection")]
        public LayerMask detectionMask = ~0;
        
        // Currently detected grabbable object (can be null)
        private PlayerPickup _currentGrabbable;
        
        // Public property for other scripts to check what's being looked at
        public PlayerPickup CurrentGrabbable => _currentGrabbable;
        
        // Public property to check if looking at a grabbable
        public bool IsLookingAtGrabbable => _currentGrabbable != null;
        
        void Awake()
        {
            if (crosshair == null)
                crosshair = GetComponent<Crosshair>();
            
            if (cam == null)
                cam = Camera.main;
        }
        
        void Update()
        {
            CheckForGrabbableObject();
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
                // Check if hit object has GrabableObject component
                var grabbable = hit.collider.GetComponent<PlayerPickup>();
                if (grabbable != null)
                {
                    // Store the detected object
                    _currentGrabbable = grabbable;
                    
                    // Expand crosshair
                    crosshair.SetGap(expandedGap);
                    return;
                }
            }
            
            // No grabbable object - contract to default
            _currentGrabbable = null;
            crosshair.SetGap(defaultGap);
        }
    }
}

