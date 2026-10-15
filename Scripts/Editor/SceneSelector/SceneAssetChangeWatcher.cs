using System;
using UnityEditor;

namespace Gustom.Editor
{
    public class SceneAssetChangeWatcher : AssetPostprocessor
    {
        public static event Action OnScenesChanged;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool sceneChanged = ContainsScene(importedAssets) || ContainsScene(deletedAssets) || ContainsScene(movedAssets) || ContainsScene(movedFromAssetPaths);
            if (!sceneChanged) return;

            OnScenesChanged?.Invoke();
        }

        private static bool ContainsScene(string[] paths)
        {
            if (paths == null) return false;
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] != null && paths[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}