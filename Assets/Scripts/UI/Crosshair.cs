using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class Crosshair : MonoBehaviour
    {
        [Header("Crosshair Style")]
        [Tooltip("Color of the crosshair lines")]
        public Color crosshairColor = Color.white;
        
        [Tooltip("Thickness of each line (in pixels)")]
        public float lineThickness = 2f;
        
        [Tooltip("Length of each line (in pixels)")]
        public float lineLength = 10f;
        
        [Header("Gap Settings")]
        [Tooltip("Gap from center (in pixels) - this can be animated")]
        public float gapFromCenter = 5f;
        
        [Tooltip("Target gap (for smooth transitions)")]
        public float targetGap = 5f;
        
        [Tooltip("Speed of gap transitions")]
        public float gapTransitionSpeed = 10f;
        
        [Header("Animation")]
        [Tooltip("Enable smooth gap transitions")]
        public bool smoothTransitions = true;
        
        // The 4 line images (top, bottom, left, right)
        private Image _topLine;
        private Image _bottomLine;
        private Image _leftLine;
        private Image _rightLine;
        
        private RectTransform _topRect;
        private RectTransform _bottomRect;
        private RectTransform _leftRect;
        private RectTransform _rightRect;
        
        private Canvas _canvas;
        
        void Awake()
        {
            CreateCrosshair();
        }
        
        void Start()
        {
            UpdateCrosshairAppearance();
        }
        
        void Update()
        {
            // Smooth transition to target gap
            if (smoothTransitions && Mathf.Abs(gapFromCenter - targetGap) > 0.01f)
            {
                gapFromCenter = Mathf.Lerp(gapFromCenter, targetGap, Time.deltaTime * gapTransitionSpeed);
                UpdateCrosshairPosition();
            }
        }
        
        /// <summary>
        /// Creates the 4 lines that make up the crosshair
        /// </summary>
        void CreateCrosshair()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("Crosshair must be a child of a Canvas!");
                return;
            }
            
            // Create container for crosshair lines
            GameObject topObj = new GameObject("Crosshair_Top");
            GameObject bottomObj = new GameObject("Crosshair_Bottom");
            GameObject leftObj = new GameObject("Crosshair_Left");
            GameObject rightObj = new GameObject("Crosshair_Right");
            
            // Parent to this transform
            topObj.transform.SetParent(transform, false);
            bottomObj.transform.SetParent(transform, false);
            leftObj.transform.SetParent(transform, false);
            rightObj.transform.SetParent(transform, false);
            
            // Add Image components
            _topLine = topObj.AddComponent<Image>();
            _bottomLine = bottomObj.AddComponent<Image>();
            _leftLine = leftObj.AddComponent<Image>();
            _rightLine = rightObj.AddComponent<Image>();
            
            // Get RectTransforms
            _topRect = topObj.GetComponent<RectTransform>();
            _bottomRect = bottomObj.GetComponent<RectTransform>();
            _leftRect = leftObj.GetComponent<RectTransform>();
            _rightRect = rightObj.GetComponent<RectTransform>();
            
            // Set pivot to appropriate positions for scaling from center
            _topRect.pivot = new Vector2(0.5f, 0f);      // Bottom center
            _bottomRect.pivot = new Vector2(0.5f, 1f);   // Top center
            _leftRect.pivot = new Vector2(1f, 0.5f);     // Right center
            _rightRect.pivot = new Vector2(0f, 0.5f);    // Left center
            
            UpdateCrosshairAppearance();
            UpdateCrosshairPosition();
        }
        
        /// <summary>
        /// Updates the size and color of the crosshair lines
        /// </summary>
        void UpdateCrosshairAppearance()
        {
            if (_topLine == null) return;
            
            // Set colors
            _topLine.color = crosshairColor;
            _bottomLine.color = crosshairColor;
            _leftLine.color = crosshairColor;
            _rightLine.color = crosshairColor;
            
            // Set sizes
            // Top and Bottom lines (vertical)
            _topRect.sizeDelta = new Vector2(lineThickness, lineLength);
            _bottomRect.sizeDelta = new Vector2(lineThickness, lineLength);
            
            // Left and Right lines (horizontal)
            _leftRect.sizeDelta = new Vector2(lineLength, lineThickness);
            _rightRect.sizeDelta = new Vector2(lineLength, lineThickness);
        }
        
        /// <summary>
        /// Updates the position of the crosshair lines based on the gap
        /// </summary>
        void UpdateCrosshairPosition()
        {
            if (_topRect == null) return;
            
            // Position lines with gap from center
            _topRect.anchoredPosition = new Vector2(0, gapFromCenter);
            _bottomRect.anchoredPosition = new Vector2(0, -gapFromCenter);
            _leftRect.anchoredPosition = new Vector2(-gapFromCenter, 0);
            _rightRect.anchoredPosition = new Vector2(gapFromCenter, 0);
        }
        
        /// <summary>
        /// Set the gap from center (useful for external scripts to control expansion)
        /// </summary>
        public void SetGap(float gap, bool immediate = false)
        {
            targetGap = gap;
            if (immediate)
            {
                gapFromCenter = gap;
                UpdateCrosshairPosition();
            }
        }
        
        /// <summary>
        /// Expand the crosshair (increase gap)
        /// </summary>
        public void Expand(float amount)
        {
            SetGap(gapFromCenter + amount);
        }
        
        /// <summary>
        /// Contract the crosshair (decrease gap)
        /// </summary>
        public void Contract(float amount)
        {
            SetGap(Mathf.Max(0, gapFromCenter - amount));
        }
        
        /// <summary>
        /// Reset to default gap
        /// </summary>
        public void ResetGap()
        {
            SetGap(5f);
        }
        
        /// <summary>
        /// Set crosshair color
        /// </summary>
        public void SetColor(Color color)
        {
            crosshairColor = color;
            UpdateCrosshairAppearance();
        }
        
        /// <summary>
        /// Set crosshair visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_topLine != null) _topLine.enabled = visible;
            if (_bottomLine != null) _bottomLine.enabled = visible;
            if (_leftLine != null) _leftLine.enabled = visible;
            if (_rightLine != null) _rightLine.enabled = visible;
        }
        
        // Validate changes in inspector
        void OnValidate()
        {
            if (Application.isPlaying && _topLine != null)
            {
                UpdateCrosshairAppearance();
                UpdateCrosshairPosition();
            }
        }
    }
}