#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor
{
    public static class AddCheckpointComponents
    {
        [MenuItem("Tools/Checkpoint/Add Checkpoint Components to named objects and prefabs")]
        public static void AddComponents()
        {
            int sceneAdded = 0;
            int prefabAdded = 0;
            string[] names = new[] { "Checkpoint", "CheckpointAnchor" };

            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;

                bool sceneChanged = false;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        if (t == null) continue;
                        foreach (var n in names)
                        {
                            if (t.gameObject.name == n)
                            {
                                var go = t.gameObject;
                                if (go.GetComponent(typeof(Interact.Checkpoint)) == null)
                                {
                                    // Use the non-generic overload because a namespaced type can't be used as a generic argument here
                                    Undo.AddComponent(go, typeof(Interact.Checkpoint));
                                    sceneAdded++;
                                    sceneChanged = true;
                                    Debug.Log($"Added Checkpoint to scene object: {go.name} (Scene: {scene.name})");
                                }
                                break;
                            }
                        }
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool changed = false;
                var transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    if (t == null) continue;
                    foreach (var n in names)
                    {
                        if (t.gameObject.name == n)
                        {
                            var go = t.gameObject;
                            if (go.GetComponent(typeof(Interact.Checkpoint)) == null)
                            {
                                go.AddComponent(typeof(Interact.Checkpoint));
                                changed = true;
                                prefabAdded++;
                                Debug.Log($"Added Checkpoint to prefab: {path} -> {go.name}");
                            }
                            break;
                        }
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            EditorUtility.DisplayDialog("Add Checkpoint Components", $"Added {sceneAdded} components to scene objects and {prefabAdded} components to prefabs.", "OK");
        }
    }
}
#endif
