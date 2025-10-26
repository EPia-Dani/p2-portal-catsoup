using UnityEngine;

public class GameBootstrap : MonoBehaviour {
	[SerializeField] private CursorLockMode lockMode = CursorLockMode.Locked;
	[SerializeField] private bool cursorVisible = false;

	private void Awake() {
		CursorUtility.Apply(lockMode, cursorVisible);
	}
}


