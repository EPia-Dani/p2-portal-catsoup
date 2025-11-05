using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple scene management system for loading menu and level scenes
/// </summary>
public static class GameSceneManager {
	// Scene names - update these to match your actual scene names
	public const string MAIN_MENU_SCENE = "MainMenu";
	public const string LEVEL_SCENE_PREFIX = "Level";
	public const string PORTAL_SCENE = "Portal"; // Existing scene
	public const string SAMPLE_SCENE = "SampleScene"; // Existing scene

	/// <summary>
	/// Loads a scene by name
	/// </summary>
	public static void LoadScene(string sceneName) {
		if (string.IsNullOrEmpty(sceneName)) {
			Debug.LogError("SceneManager: Cannot load scene with null or empty name");
			return;
		}

		Debug.Log($"SceneManager: Loading scene '{sceneName}'");
		SceneManager.LoadScene(sceneName);
	}

	/// <summary>
	/// Loads a scene by build index
	/// </summary>
	public static void LoadScene(int buildIndex) {
		if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings) {
			Debug.LogError($"SceneManager: Invalid scene index {buildIndex}");
			return;
		}

		Debug.Log($"SceneManager: Loading scene at index {buildIndex}");
		SceneManager.LoadScene(buildIndex);
	}

	/// <summary>
	/// Loads the main menu scene
	/// </summary>
	public static void LoadMainMenu() {
		LoadScene(MAIN_MENU_SCENE);
	}

	/// <summary>
	/// Loads a level scene by index (1-based)
	/// </summary>
	public static void LoadLevel(int levelNumber) {
		LoadScene(LEVEL_SCENE_PREFIX + levelNumber);
	}

	/// <summary>
	/// Reloads the current active scene
	/// </summary>
	public static void ReloadCurrentScene() {
		Scene currentScene = SceneManager.GetActiveScene();
		Debug.Log($"SceneManager: Reloading scene '{currentScene.name}'");
		SceneManager.LoadScene(currentScene.buildIndex);
	}

	/// <summary>
	/// Quits the game (works in build, shows message in editor)
	/// </summary>
	public static void QuitGame() {
		Debug.Log("SceneManager: Quitting game");
		
		#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
		#else
		Application.Quit();
		#endif
	}

	/// <summary>
	/// Gets the name of the currently active scene
	/// </summary>
	public static string GetCurrentSceneName() {
		return SceneManager.GetActiveScene().name;
	}
}

