#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProjectEditorUtilities
{
    // Editor utility to ensure LaserEmitter components detect intruders and trigger colliders.
    public static class SetLaserEmitterDefaults
    {
        [MenuItem("Tools/Fix LaserEmitters: Enable Intruder & Triggers")]
        public static void FixAllLaserEmitters()
        {
            int modifiedSceneCount = 0;
            int modifiedPrefabCount = 0;
            int totalModified = 0;

            // Fix instances in open scenes
            var sceneEmitters = Object.FindObjectsByType<RefractionCubes.LaserEmitter>(FindObjectsSortMode.None);
            foreach (var emitter in sceneEmitters)
            {
                if (emitter == null) continue;
                bool changed = false;
                if (!emitter.checkForIntruders)
                {
                    emitter.checkForIntruders = true;
                    changed = true;
                }
                if (!emitter.includeTriggerColliders)
                {
                    emitter.includeTriggerColliders = true;
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(emitter);
                    modifiedSceneCount++;
                    totalModified++;
                }
            }

            // Fix prefabs in project
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                bool prefabChanged = false;
                var emitters = prefab.GetComponentsInChildren<RefractionCubes.LaserEmitter>(true);
                if (emitters != null && emitters.Length > 0)
                {
                    foreach (var emitter in emitters)
                    {
                        if (emitter == null) continue;
                        SerializedObject so = new SerializedObject(emitter);
                        var propCheck = so.FindProperty("checkForIntruders");
                        var propTrigger = so.FindProperty("includeTriggerColliders");
                        if (propCheck != null && !propCheck.boolValue)
                        {
                            propCheck.boolValue = true;
                            prefabChanged = true;
                        }
                        if (propTrigger != null && !propTrigger.boolValue)
                        {
                            propTrigger.boolValue = true;
                            prefabChanged = true;
                        }
                        if (prefabChanged)
                        {
                            so.ApplyModifiedProperties();
                        }
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                        modifiedPrefabCount++;
                        totalModified++;
                    }
                }
            }

            // Mark scenes dirty if we modified scene instances
            if (modifiedSceneCount > 0)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (scene.isLoaded)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }

            EditorUtility.DisplayDialog("Fix LaserEmitters", $"Modified {modifiedSceneCount} scene instances and {modifiedPrefabCount} prefabs. Total objects modified: {totalModified}.", "OK");
        }
    }
}
#endif
