using UnityEngine;

/// <summary>
/// MonoBehaviour component that can be attached to GameObjects to load scenes
/// Useful for UI buttons that need to call scene loading
/// </summary>
public class SceneLoader : MonoBehaviour {
	[SerializeField] private string sceneToLoad;
	[SerializeField] private bool loadOnStart = false;

	private void Start() {
		if (loadOnStart && !string.IsNullOrEmpty(sceneToLoad)) {
			GameSceneManager.LoadScene(sceneToLoad);
		}
	}

	/// <summary>
	/// Loads the scene specified in sceneToLoad field
	/// Can be called from UI buttons
	/// </summary>
	public void LoadScene() {
		if (string.IsNullOrEmpty(sceneToLoad)) {
			Debug.LogError("SceneLoader: No scene name specified!");
			return;
		}
		GameSceneManager.LoadScene(sceneToLoad);
	}

	/// <summary>
	/// Loads a scene by name (can be called from UI buttons)
	/// </summary>
	public void LoadSceneByName(string sceneName) {
		GameSceneManager.LoadScene(sceneName);
	}

	/// <summary>
	/// Loads the main menu
	/// </summary>
	public void LoadMainMenu() {
		GameSceneManager.LoadMainMenu();
	}

	/// <summary>
	/// Reloads the current scene
	/// </summary>
	public void ReloadCurrentScene() {
		GameSceneManager.ReloadCurrentScene();
	}

	/// <summary>
	/// Quits the game
	/// </summary>
	public void QuitGame() {
		GameSceneManager.QuitGame();
	}
}

