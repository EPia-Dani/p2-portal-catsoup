#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TurretDiagnosticsMenu
{
    [MenuItem("Diagnostics/Set RunTurretFire -> 1 (enable)")]
    public static void EnableRunTurretFire()
    {
        PlayerPrefs.SetInt("RunTurretFire", 1);
        PlayerPrefs.Save();
        Debug.Log("TurretDiagnosticsMenu: Set PlayerPrefs 'RunTurretFire' = 1");
    }

    [MenuItem("Diagnostics/Set RunTurretFire -> 0 (disable)")]
    public static void DisableRunTurretFire()
    {
        PlayerPrefs.SetInt("RunTurretFire", 0);
        PlayerPrefs.Save();
        Debug.Log("TurretDiagnosticsMenu: Set PlayerPrefs 'RunTurretFire' = 0");
    }
}
#endif
