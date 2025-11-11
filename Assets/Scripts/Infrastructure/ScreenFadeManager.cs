using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ScreenFadeManager : MonoBehaviour
{
    public float fadeOutDuration = 1f;
    public float fadeInDuration = 1f;
    public Color fadeColor = Color.black;
    
    private Canvas canvas;
    private Image image;
    private static ScreenFadeManager instance;
    private bool shouldFadeIn = false;
    
    public static ScreenFadeManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ScreenFadeManager");
                instance = obj.AddComponent<ScreenFadeManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Create simple Canvas overlay
        GameObject canvasObj = new GameObject("FadeCanvas");
        DontDestroyOnLoad(canvasObj);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasObj.AddComponent<CanvasScaler>();
        
        // Create fullscreen Image
        GameObject imgObj = new GameObject("FadeImage");
        imgObj.transform.SetParent(canvasObj.transform, false);
        image = imgObj.AddComponent<Image>();
        image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        image.raycastTarget = false;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ensure Canvas is active and on top
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.sortingOrder = 9999;
        }
        if (image != null)
        {
            image.gameObject.SetActive(true);
            image.enabled = true;
        }
        
        StartCoroutine(OnSceneLoadedDelayed());
    }
    
    private IEnumerator OnSceneLoadedDelayed()
    {
        yield return null; // Wait one frame
        
        if (shouldFadeIn && image != null)
        {
            image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
            StartCoroutine(FadeIn());
            shouldFadeIn = false;
        }
        else if (image != null)
        {
            image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        }
    }
    
    public void FadeOutAndLoadScene(string sceneName)
    {
        StartCoroutine(FadeOut(() => SceneManager.LoadScene(sceneName)));
    }
    
    public void FadeOutAndLoadScene(int sceneIndex)
    {
        StartCoroutine(FadeOut(() => SceneManager.LoadScene(sceneIndex)));
    }
    
    public void FadeOutAndReloadScene()
    {
        StartCoroutine(FadeOut(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)));
    }
    
    public void FadeOutAndRespawn(System.Action onComplete)
    {
        StartCoroutine(FadeOut(() => { onComplete?.Invoke(); StartCoroutine(FadeIn()); }));
    }
    
    private IEnumerator FadeOut(System.Action onComplete)
    {
        // Ensure Canvas is active
        if (canvas != null) canvas.gameObject.SetActive(true);
        if (image != null) image.gameObject.SetActive(true);
        
        shouldFadeIn = true; // Mark that we should fade in after scene loads
        
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
            if (image != null) image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        if (image != null) image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        onComplete?.Invoke();
    }
    
    private IEnumerator FadeIn()
    {
        // Ensure Canvas is active
        if (canvas != null) canvas.gameObject.SetActive(true);
        if (image != null) image.gameObject.SetActive(true);
        
        float elapsed = 0f;
        float startAlpha = image != null ? image.color.a : 1f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeInDuration);
            if (image != null) image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        if (image != null) image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
    }
}
