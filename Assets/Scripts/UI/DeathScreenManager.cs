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
    
    [Header("Settings")]
    [Tooltip("Message displayed when player dies.")]
    public string deathMessage = "You Died";
    
    [Tooltip("Should the death screen pause the game?")]
    public bool pauseOnDeath = true;
    
    private PlayerManager playerManager;
    private FPSController fpsController;
    private bool isShowing = false;
    
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
        }
    }
    
    /// <summary>
    /// Shows the death screen UI immediately.
    /// </summary>
    public void ShowDeathScreen()
    {
        if (isShowing)
        {
            return; // Already showing
        }
        
        isShowing = true;
        
        // Disable camera rotation
        if (fpsController != null)
        {
            fpsController.SetCameraRotationEnabled(false);
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
    }
    
    /// <summary>
    /// Hides the death screen UI immediately.
    /// </summary>
    public void HideDeathScreen()
    {
        if (!isShowing)
        {
            return;
        }
        
        isShowing = false;
        
        // Hide death screen panel
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Resume game
        if (pauseOnDeath)
        {
            Time.timeScale = 1f;
        }
        
        // Re-lock cursor
        CursorUtility.Apply(CursorLockMode.Locked, false);
        
        // Re-enable camera rotation
        if (fpsController != null)
        {
            fpsController.SetCameraRotationEnabled(true);
        }
    }
    
    /// <summary>
    /// Called when the Continue button is clicked.
    /// </summary>
    private void OnContinueClicked()
    {
        if (playerManager == null)
        {
            return;
        }
        
        // Set time scale FIRST - critical
        Time.timeScale = 1f;
        
        // Hide UI immediately
        isShowing = false;
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Use fade system for smooth respawn transition
        if (ScreenFadeManager.Instance != null)
        {
            ScreenFadeManager.Instance.FadeOutAndRespawn(() =>
            {
                // Lock cursor
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                
                // Enable camera rotation
                if (fpsController != null)
                {
                    fpsController.SetCameraRotationEnabled(true);
                }
                
                // Respawn player (happens during black screen)
                playerManager.RespawnPlayer();
            });
        }
        else
        {
            // Fallback if fade manager doesn't exist
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            if (fpsController != null)
            {
                fpsController.SetCameraRotationEnabled(true);
            }
            
            playerManager.RespawnPlayer();
        }
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


