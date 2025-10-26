using UnityEngine;

public static class CursorUtility {
	public static void Apply(CursorLockMode mode, bool visible) {
		Cursor.lockState = mode;
		Cursor.visible = visible;
	}
}


