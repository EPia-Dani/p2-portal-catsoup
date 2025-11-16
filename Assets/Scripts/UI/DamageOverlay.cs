using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class DamageOverlay : MonoBehaviour
{
    [Header("Flash Settings")]
    [Tooltip("Duration of the damage flash effect in seconds")]
    public float flashDuration = 0.3f;
    
    [Tooltip("Maximum alpha of the damage flash (0-1)")]
    [Range(0f, 1f)]
    public float flashAlpha = 0.5f;
    
    [Tooltip("Color of the damage overlay (typically red)")]
    public Color damageColor = new Color(1f, 0f, 0f, 0.5f); // Red with 50% alpha
    
    private Image overlayImage;
    private Coroutine flashCoroutine;
    
    private void Awake()
    {
        overlayImage = GetComponent<Image>();
        if (overlayImage == null)
        {
            Debug.LogError("DamageOverlay: Image component not found!");
            return;
        }
        
        // Set up the overlay to cover the entire screen
        RectTransform rectTransform = overlayImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        // Start transparent
        Color color = damageColor;
        color.a = 0f;
        overlayImage.color = color;
        overlayImage.raycastTarget = false; // Don't block input
    }
    
    /// <summary>
    /// Trigger a brief damage flash effect
    /// </summary>
    public void ShowDamageFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }
    
    /// <summary>
    /// Clear all overlays (used on death or heal)
    /// </summary>
    public void ClearOverlay()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        
        Color color = damageColor;
        color.a = 0f;
        overlayImage.color = color;
    }
    
    private IEnumerator DamageFlashCoroutine()
    {
        float elapsed = 0f;
        
        // Flash in quickly
        while (elapsed < flashDuration * 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration * 0.3f);
            
            Color color = damageColor;
            color.a = Mathf.Lerp(0f, flashAlpha, t);
            overlayImage.color = color;
            
            yield return null;
        }
        
        // Flash out more slowly
        elapsed = 0f;
        while (elapsed < flashDuration * 0.7f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration * 0.7f);
            
            Color color = damageColor;
            color.a = Mathf.Lerp(flashAlpha, 0f, t);
            overlayImage.color = color;
            
            yield return null;
        }
        
        // Ensure we end at fully transparent
        Color finalColor = damageColor;
        finalColor.a = 0f;
        overlayImage.color = finalColor;
        
        flashCoroutine = null;
    }
    
    private void Update()
    {
        // Ensure overlay is fully transparent when not flashing
        if (flashCoroutine == null && overlayImage.color.a > 0.001f)
        {
            Color color = damageColor;
            color.a = 0f;
            overlayImage.color = color;
        }
    }
}

