using UnityEngine;
using System.Text;
using Enemy;

public static class TurretBuildDiagnostics
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RunAfterSceneLoad()
    {
        try
        {
            var turrets = Object.FindObjectsByType<Turret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"TurretBuildDiagnostics: Found {turrets.Length} Turret(s) in scene.");

            foreach (var t in turrets)
            {
                if (t == null) continue;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"--- Turret '{t.name}' diagnostics ---");
                sb.AppendLine($" activeSelf={t.gameObject.activeSelf}, enabled={t.enabled}");
                sb.AppendLine($" detectionRadius={t.detectionRadius}, projectileSpeed={t.projectileSpeed}");
                sb.AppendLine($" firePointAssigned={(t.firePoint!=null)}");
                sb.AppendLine($" projectilePrefabAssigned={(t.projectilePrefab!=null)}");
                sb.AppendLine($" targetMask={t.targetMask.value} (LayerMask int)");

                // quick overlap check using the turret's targetMask
                var temp = new Collider[32];
                int found = Physics.OverlapSphereNonAlloc(t.transform.position, t.detectionRadius, temp, t.targetMask);
                sb.AppendLine($" OverlapSphere found {found} collider(s) using targetMask.");
                if (found > 0)
                {
                    for (int i = 0; i < found; i++)
                    {
                        var c = temp[i];
                        if (c != null) sb.AppendLine($"  - {c.name} (layer={LayerMask.LayerToName(c.gameObject.layer)})");
                    }
                }

                Debug.Log(sb.ToString());

                // Optionally fire a test shot in builds when PlayerPrefs "RunTurretFire" == 1 (set in Editor before build)
                if (PlayerPrefs.GetInt("RunTurretFire", 0) == 1)
                {
                    Debug.Log($"TurretBuildDiagnostics: Forced TestFire for turret '{t.name}'.");
                    t.TestFire();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("TurretBuildDiagnostics: Exception while running diagnostics: " + ex);
        }
    }
}

