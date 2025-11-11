using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the death screen UI, allowing players to continue (respawn) or return to main menu.
/// </summary>
public class DeathScreenManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The death screen panel/canvas. Should be disabled by default.")]
    public GameObject deathScreenPanel;
    
    [Tooltip("Text displaying 'You Died' or similar message.")]
    public TMP_Text deathMessageText;
    
    [Tooltip("Button to continue/respawn at checkpoint.")]
    public Button continueButton;
    
    [Tooltip("Button to return to main menu.")]
    public Button restartButton;
    
    [Tooltip("Screen fade component for fade transitions. If null, will search for one.")]
    public ScreenFade screenFade;
    
    [Header("Settings")]
    [Tooltip("Message displayed when player dies.")]
    public string deathMessage = "You Died";
    
    [Tooltip("Should the death screen pause the game?")]
    public bool pauseOnDeath = true;
    
    [Tooltip("Duration of fade to black before showing death screen (in seconds)")]
    public float fadeOutDuration = 0.5f;
    
    [Tooltip("Duration of fade in after respawning (in seconds)")]
    public float fadeInDuration = 0.5f;
    
    [Tooltip("Duration of text fade in animation (in seconds)")]
    public float textFadeDuration = 0.5f;
    
    [Tooltip("Delay before buttons start fading in (in seconds)")]
    public float buttonFadeDelay = 0.3f;
    
    [Tooltip("Duration of button fade in animation (in seconds)")]
    public float buttonFadeDuration = 0.5f;
    
    private PlayerManager playerManager;
    private FPSController fpsController;
    private bool isShowing = false;
    
    // Store original colors for fade animations
    private Color originalTextColor;
    private Color originalContinueButtonColor;
    private Color originalRestartButtonColor;
    
    private void Start()
    {
        // Find PlayerManager
        playerManager = FindFirstObjectByType<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogError("DeathScreenManager: PlayerManager not found in scene!");
        }
        
        // Find FPSController to disable camera rotation
        fpsController = FindFirstObjectByType<FPSController>();
        if (fpsController == null)
        {
            Debug.LogWarning("DeathScreenManager: FPSController not found! Camera rotation will not be disabled on death screen.");
        }
        
        // Find ScreenFade if not assigned
        if (screenFade == null)
        {
            screenFade = FindFirstObjectByType<ScreenFade>();
            if (screenFade == null)
            {
                Debug.LogWarning("DeathScreenManager: ScreenFade not found! Fade transitions will not work. Please add a ScreenFade component to your Canvas.");
            }
        }
        
        // Set fade duration if ScreenFade is found
        if (screenFade != null)
        {
            screenFade.fadeDuration = fadeOutDuration;
        }
        
        // Hide death screen by default
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Setup button listeners
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        
        // Set death message text
        if (deathMessageText != null)
        {
            deathMessageText.text = deathMessage;
            // Store original color and set to transparent
            originalTextColor = deathMessageText.color;
            deathMessageText.color = new Color(originalTextColor.r, originalTextColor.g, originalTextColor.b, 0f);
        }
        
        // Store original button colors and set to transparent
        if (continueButton != null)
        {
            var continueImage = continueButton.GetComponent<Image>();
            if (continueImage != null)
            {
                originalContinueButtonColor = continueImage.color;
                continueImage.color = new Color(originalContinueButtonColor.r, originalContinueButtonColor.g, originalContinueButtonColor.b, 0f);
            }
            
            // Also fade button text if it has one
            var continueText = continueButton.GetComponentInChildren<TMP_Text>();
            if (continueText != null)
            {
                var textColor = continueText.color;
                continueText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
            }
        }
        
        if (restartButton != null)
        {
            var restartImage = restartButton.GetComponent<Image>();
            if (restartImage != null)
            {
                originalRestartButtonColor = restartImage.color;
                restartImage.color = new Color(originalRestartButtonColor.r, originalRestartButtonColor.g, originalRestartButtonColor.b, 0f);
            }
            
            // Also fade button text if it has one
            var restartText = restartButton.GetComponentInChildren<TMP_Text>();
            if (restartText != null)
            {
                var textColor = restartText.color;
                restartText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
            }
        }
    }
    
    /// <summary>
    /// Shows the death screen UI with fade to black transition.
    /// </summary>
    public void ShowDeathScreen()
    {
        if (isShowing)
        {
            return; // Already showing
        }
        
        // Start fade to black, then show death screen
        StartCoroutine(ShowDeathScreenCoroutine());
    }
    
    /// <summary>
    /// Coroutine that handles fade to black then shows death screen.
    /// </summary>
    private IEnumerator ShowDeathScreenCoroutine()
    {
        isShowing = true;
        
        // Disable camera rotation
        if (fpsController != null)
        {
            fpsController.SetCameraRotationEnabled(false);
        }
        
        // Fade to black
        if (screenFade != null)
        {
            screenFade.fadeDuration = fadeOutDuration;
            screenFade.FadeOut();
            
            // Wait for fade to complete
            yield return new WaitForSecondsRealtime(fadeOutDuration);
        }
        
        // Show death screen panel
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(true);
        }
        
        // Pause game if enabled
        if (pauseOnDeath)
        {
            Time.timeScale = 0f;
        }
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Start fade-in animations for text and buttons
        StartCoroutine(FadeInUIElements());
    }
    
    /// <summary>
    /// Coroutine that fades in the text first, then buttons after a delay.
    /// </summary>
    private IEnumerator FadeInUIElements()
    {
        // Fade in text first
        if (deathMessageText != null)
        {
            yield return StartCoroutine(FadeTextIn());
        }
        
        // Wait for button delay
        yield return new WaitForSecondsRealtime(buttonFadeDelay);
        
        // Fade in buttons
        StartCoroutine(FadeButtonsIn());
    }
    
    /// <summary>
    /// Coroutine that fades in the death message text.
    /// </summary>
    private IEnumerator FadeTextIn()
    {
        float elapsed = 0f;
        
        while (elapsed < textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0f, originalTextColor.a, elapsed / textFadeDuration);
            deathMessageText.color = new Color(originalTextColor.r, originalTextColor.g, originalTextColor.b, alpha);
            yield return null;
        }
        
        // Ensure final alpha
        deathMessageText.color = originalTextColor;
    }
    
    /// <summary>
    /// Coroutine that fades in both buttons simultaneously.
    /// </summary>
    private IEnumerator FadeButtonsIn()
    {
        float elapsed = 0f;
        
        while (elapsed < buttonFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / buttonFadeDuration);
            
            // Fade continue button
            if (continueButton != null)
            {
                var continueImage = continueButton.GetComponent<Image>();
                if (continueImage != null)
                {
                    continueImage.color = new Color(originalContinueButtonColor.r, originalContinueButtonColor.g, originalContinueButtonColor.b, alpha * originalContinueButtonColor.a);
                }
                
                var continueText = continueButton.GetComponentInChildren<TMP_Text>();
                if (continueText != null)
                {
                    var textColor = continueText.color;
                    continueText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
                }
            }
            
            // Fade restart button
            if (restartButton != null)
            {
                var restartImage = restartButton.GetComponent<Image>();
                if (restartImage != null)
                {
                    restartImage.color = new Color(originalRestartButtonColor.r, originalRestartButtonColor.g, originalRestartButtonColor.b, alpha * originalRestartButtonColor.a);
                }
                
                var restartText = restartButton.GetComponentInChildren<TMP_Text>();
                if (restartText != null)
                {
                    var textColor = restartText.color;
                    restartText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
                }
            }
            
            yield return null;
        }
        
        // Ensure final colors
        if (continueButton != null)
        {
            var continueImage = continueButton.GetComponent<Image>();
            if (continueImage != null)
            {
                continueImage.color = originalContinueButtonColor;
            }
            
            var continueText = continueButton.GetComponentInChildren<TMP_Text>();
            if (continueText != null)
            {
                var textColor = continueText.color;
                continueText.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
            }
        }
        
        if (restartButton != null)
        {
            var restartImage = restartButton.GetComponent<Image>();
            if (restartImage != null)
            {
                restartImage.color = originalRestartButtonColor;
            }
            
            var restartText = restartButton.GetComponentInChildren<TMP_Text>();
            if (restartText != null)
            {
                var textColor = restartText.color;
                restartText.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
            }
        }
    }
    
    /// <summary>
    /// Hides the death screen UI and fades in.
    /// </summary>
    public void HideDeathScreen()
    {
        if (!isShowing)
        {
            return;
        }
        
        // Start fade in coroutine
        StartCoroutine(HideDeathScreenCoroutine());
    }
    
    /// <summary>
    /// Coroutine that hides death screen and fades in.
    /// </summary>
    private IEnumerator HideDeathScreenCoroutine()
    {
        // Hide death screen panel first
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Resume game before fading
        if (pauseOnDeath)
        {
            Time.timeScale = 1f;
        }
        
        // Re-lock and hide cursor before we return control to the player
        CursorUtility.Apply(CursorLockMode.Locked, false);
        
        // Re-enable camera rotation
        if (fpsController != null)
        {
            fpsController.SetCameraRotationEnabled(true);
        }
        
        // Fade in
        if (screenFade != null)
        {
            screenFade.fadeDuration = fadeInDuration;
            screenFade.FadeIn();
            
            // Wait for fade to complete
            yield return new WaitForSecondsRealtime(fadeInDuration);
        }
        
        isShowing = false;
    }
    
    /// <summary>
    /// Called when the Continue button is clicked.
    /// </summary>
    private void OnContinueClicked()
    {
        if (playerManager != null)
        {
            // Hide death screen (will fade in), then respawn
            StartCoroutine(RespawnCoroutine());
        }
        else
        {
            Debug.LogError("DeathScreenManager: Cannot respawn - PlayerManager not found!");
        }
    }
    
    /// <summary>
    /// Coroutine that handles respawn with fade transition.
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        // Hide death screen (fades in)
        HideDeathScreen();
        
        // Wait for fade to complete
        yield return new WaitForSecondsRealtime(fadeInDuration);
        
        // Respawn player
        playerManager.RespawnPlayer();
    }
    
    /// <summary>
    /// Called when the Restart button is clicked. Returns to main menu.
    /// </summary>
    private void OnRestartClicked()
    {
        // Resume time before loading menu
        if (pauseOnDeath)
        {
            Time.timeScale = 1f;
        }
        
        // Load main menu
        GameSceneManager.LoadMainMenu();
    }
    
    private void OnDestroy()
    {
        // Ensure time scale is reset when destroyed
        if (pauseOnDeath)
        {
            Time.timeScale = 1f;
        }
    }
}


