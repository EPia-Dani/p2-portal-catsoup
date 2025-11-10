using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles screen fade in/out transitions using a UI Image overlay.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFade : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Duration of fade in seconds")]
    public float fadeDuration = 1f;
    
    [Tooltip("Color to fade to (usually black). The alpha value determines how opaque the fade will be when faded out.")]
    public Color fadeColor = Color.black;
    
    private Image fadeImage;
    private Coroutine currentFadeCoroutine;
    
    private void Awake()
    {
        fadeImage = GetComponent<Image>();
        if (fadeImage == null)
        {
            Debug.LogError("ScreenFade: Image component not found!");
            return;
        }
        
        // Set up the fade image to cover the entire screen
        RectTransform rectTransform = fadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        // Start with transparent
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = false; // Don't block input when transparent
    }
    
    /// <summary>
    /// Fades the screen to the fade color (fade out).
    /// Uses the alpha value from fadeColor set in the editor.
    /// </summary>
    public void FadeOut(System.Action onComplete = null)
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        
        fadeImage.raycastTarget = true; // Block input during fade
        float currentAlpha = fadeImage.color.a;
        float targetAlpha = fadeColor.a;
        currentFadeCoroutine = StartCoroutine(FadeCoroutine(currentAlpha, targetAlpha, onComplete));
    }
    
    /// <summary>
    /// Fades the screen from the fade color to transparent (fade in).
    /// Starts from the alpha value of fadeColor set in the editor.
    /// </summary>
    public void FadeIn(System.Action onComplete = null)
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        
        float currentAlpha = fadeImage.color.a;
        currentFadeCoroutine = StartCoroutine(FadeCoroutine(currentAlpha, 0f, () =>
        {
            fadeImage.raycastTarget = false; // Allow input when transparent
            onComplete?.Invoke();
        }));
    }
    
    /// <summary>
    /// Instantly sets the fade to the fade color alpha (uses alpha from fadeColor set in editor).
    /// </summary>
    public void SetFadeOut()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = null;
        }
        
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeColor.a);
        fadeImage.raycastTarget = true;
    }
    
    /// <summary>
    /// Instantly sets the fade to fully transparent (no fade).
    /// </summary>
    public void SetFadeIn()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = null;
        }
        
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = false;
    }
    
    /// <summary>
    /// Coroutine that handles the fade animation.
    /// </summary>
    private IEnumerator FadeCoroutine(float startAlpha, float endAlpha, System.Action onComplete)
    {
        float elapsed = 0f;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time so it works even when game is paused
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        
        // Ensure we end at the exact target alpha
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, endAlpha);
        
        currentFadeCoroutine = null;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Checks if a fade is currently in progress.
    /// </summary>
    public bool IsFading => currentFadeCoroutine != null;
}

