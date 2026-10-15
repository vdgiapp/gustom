using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    public static class MissingScriptsRemover
    {
        private static float _lastCallTime = 0f;

        [MenuItem("GameObject/Remove Missing Scripts")]
        private static void FindAndRemoveMissingInSelected()
        {
            if (Mathf.Approximately(Time.unscaledTime, _lastCallTime)) return;

            IEnumerable<GameObject> selected = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
            HashSet<Object> prefabs = new();
            List<GameObject> affectedGameObjects = new();

            int removedComponentsCount = 0;
            int removedGameObjectsCount = 0;

            foreach (GameObject gameObject in selected)
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (count == 0) continue;
                if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                {
                    RecursivePrefabSource(gameObject, prefabs, ref removedComponentsCount, ref removedGameObjectsCount);
                    count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    if (count == 0) continue;
                }

                Undo.RegisterCompleteObjectUndo(gameObject, "Remove missing scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);

                affectedGameObjects.Add(gameObject);
                removedComponentsCount += count;
                removedGameObjectsCount += 1;
            }

            if (removedGameObjectsCount == 0) Debug.LogWarning("There is no selected game objects with missing scripts.");
            else Debug.Log($"Removed {removedComponentsCount} missing scripts from {removedGameObjectsCount} game objects." + $"\nGame objects: {string.Join(", ", affectedGameObjects.Select(go => go.name))}.");

            _lastCallTime = Time.unscaledTime;
        }

        private static void RecursivePrefabSource(GameObject instance, HashSet<Object> prefabs, ref int removedComponentsCount, ref int removedGameObjectsCount)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);

            if (!source || !prefabs.Add(source)) return;
            RecursivePrefabSource(source, prefabs, ref removedComponentsCount, ref removedGameObjectsCount);

            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(source);
            if (count > 0)
            {
                Undo.RegisterCompleteObjectUndo(source, "Remove missing scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(source);
            
                removedComponentsCount += count;
                removedGameObjectsCount += 1;
            }
        }
    }
}