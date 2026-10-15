using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gustom.Editor
{
    internal enum SceneSelectorTab
    {
        All,
        Builds,
        Pinned
    }

    internal enum SceneDisplaySize
    {
        Compact,
        Large
    }

    internal sealed class SceneSelectorStore
    {
        public static SceneSelectorStore Shared { get; } = new();

        private const string PinnedKey = "Gustom.SceneSelector.Favorites";
        private const string ShowSceneIconKey = "Gustom.SceneSelector.ShowSceneIcon";
        private const string ShowBuildIndexKey = "Gustom.SceneSelector.ShowBuildIndex";
        private const string ShowSceneStateKey = "Gustom.SceneSelector.ShowSceneState";

        private readonly HashSet<string> _pinnedGuids = new();
        private const string DisplaySizeKey = "Gustom.SceneSelector.DisplaySize";
        private const string StripedRowsKey = "Gustom.SceneSelector.StripedRows";

        public readonly List<SceneEntry> AllScenes = new();
        public bool ShowStripedRows { get; private set; } = false;
        public bool ShowSceneIcon { get; private set; } = true;
        public bool ShowBuildIndex { get; private set; } = true;
        public bool ShowSceneState { get; private set; } = true;
        public SceneDisplaySize DisplaySize { get; private set; }

        public event Action Changed;

        public SceneSelectorStore()
        {
            _pinnedGuids.Clear();
            foreach (string guid in EditorPrefs.GetString(PinnedKey, string.Empty).Split('|'))
            {
                if (!string.IsNullOrEmpty(guid)) _pinnedGuids.Add(guid);
            }

            ShowStripedRows = EditorPrefs.GetBool(StripedRowsKey, false);
            ShowSceneIcon = EditorPrefs.GetBool(ShowSceneIconKey, true);
            ShowBuildIndex = EditorPrefs.GetBool(ShowBuildIndexKey, true);
            ShowSceneState = EditorPrefs.GetBool(ShowSceneStateKey, true);
            int displaySize = EditorPrefs.GetInt(DisplaySizeKey, (int)SceneDisplaySize.Compact);
            DisplaySize = Enum.IsDefined(typeof(SceneDisplaySize), displaySize)
                ? (SceneDisplaySize)displaySize
                : SceneDisplaySize.Compact;
        }

        public void Refresh()
        {
            AllScenes.Clear();

            Dictionary<string, EditorBuildSettingsScene> buildSettingsByPath = new();
            Dictionary<string, int> buildOrderByPath = new();
            EditorBuildSettingsScene[] buildSettings = EditorBuildSettings.scenes;
            for (int index = 0; index < buildSettings.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildSettings[index];
                if (buildSettingsByPath.TryAdd(buildScene.path, buildScene))
                    buildOrderByPath.Add(buildScene.path, index);
            }

            Dictionary<string, int> buildIndexByPath = new();
            int nextBuildIndex = 0;
            for (int index = 0; index < buildSettings.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildSettings[index];
                if (buildScene.enabled) buildIndexByPath[buildScene.path] = nextBuildIndex++;
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)) continue;

                string folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
                if (folder.StartsWith("Assets/", StringComparison.Ordinal)) folder = folder.Substring(7);
                else if (folder == "Assets") folder = string.Empty;

                bool isInBuildSettings = buildSettingsByPath.TryGetValue(path, out EditorBuildSettingsScene buildScene);
                AllScenes.Add(new SceneEntry
                {
                    Guid = guid,
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    Folder = folder,
                    IsPinned = _pinnedGuids.Contains(guid),
                    InBuildSettings = isInBuildSettings,
                    Enabled = isInBuildSettings && buildScene.enabled,
                    BuildOrder = isInBuildSettings ? buildOrderByPath[path] : int.MaxValue,
                    BuildIndex = buildIndexByPath.GetValueOrDefault(path, -1)
                });
            }

            AllScenes.Sort(CompareScenes);
            Changed?.Invoke();
        }

        public void TogglePinned(SceneEntry scene)
        {
            if (scene == null || string.IsNullOrEmpty(scene.Guid)) return;

            scene.IsPinned = !scene.IsPinned;
            if (scene.IsPinned) _pinnedGuids.Add(scene.Guid);
            else _pinnedGuids.Remove(scene.Guid);

            EditorPrefs.SetString(PinnedKey, string.Join("|", _pinnedGuids));
            Changed?.Invoke();
        }

        public void SetDisplayOptions(bool showSceneIcon, bool showBuildIndex, bool showSceneState, SceneDisplaySize displaySize, bool showStripedRows)
        {
            ShowSceneIcon = showSceneIcon;
            ShowBuildIndex = showBuildIndex;
            ShowSceneState = showSceneState;
            DisplaySize = displaySize;
            ShowStripedRows = showStripedRows;
            EditorPrefs.SetBool(ShowSceneIconKey, ShowSceneIcon);
            EditorPrefs.SetBool(ShowBuildIndexKey, ShowBuildIndex);
            EditorPrefs.SetBool(ShowSceneStateKey, ShowSceneState);
            EditorPrefs.SetInt(DisplaySizeKey, (int)DisplaySize);
            EditorPrefs.SetBool(StripedRowsKey, ShowStripedRows);
            Changed?.Invoke();
        }

        private static int CompareScenes(SceneEntry left, SceneEntry right)
        {
            int buildMembership = right.InBuildSettings.CompareTo(left.InBuildSettings);
            if (buildMembership != 0) return buildMembership;

            if (left.InBuildSettings)
            {
                int buildOrder = left.BuildOrder.CompareTo(right.BuildOrder);
                if (buildOrder != 0) return buildOrder;
            }
            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        }

        public void ReorderBuildScene(List<SceneEntry> visibleScenes, int oldVisibleIndex, int newVisibleIndex)
        {
            int visibleBuildSceneCount = visibleScenes.Count(s => s.InBuildSettings);
            if (oldVisibleIndex < 0 || newVisibleIndex < 0 ||
                oldVisibleIndex >= visibleScenes.Count || newVisibleIndex >= visibleScenes.Count ||
                !visibleScenes[oldVisibleIndex].InBuildSettings || !visibleScenes[newVisibleIndex].InBuildSettings ||
                oldVisibleIndex == newVisibleIndex) return;

            if (visibleBuildSceneCount == 0) return;

            SceneEntry scene = visibleScenes[oldVisibleIndex];
            visibleScenes.RemoveAt(oldVisibleIndex);
            visibleScenes.Insert(newVisibleIndex, scene);
            ApplyVisibleReorderToBuildSettings(visibleScenes);
        }

        private void ApplyVisibleReorderToBuildSettings(List<SceneEntry> visibleScenes)
        {
            List<SceneEntry> reorderedVisibleBuildScenes = visibleScenes.Where(s => s.InBuildSettings).ToList();
            HashSet<SceneEntry> reorderedVisibleSet = new(reorderedVisibleBuildScenes);
            List<int> masterBuildSlots = new();
            for (int index = 0; index < AllScenes.Count; index++)
                if (reorderedVisibleSet.Contains(AllScenes[index])) masterBuildSlots.Add(index);

            for (int index = 0; index < masterBuildSlots.Count; index++)
                AllScenes[masterBuildSlots[index]] = reorderedVisibleBuildScenes[index];

            ApplyBuildSceneOrder();
            EditorApplication.delayCall += Refresh;
        }

        private void ApplyBuildSceneOrder()
        {
            EditorBuildSettingsScene[] currentBuildScenes = EditorBuildSettings.scenes;
            HashSet<string> managedScenePaths = new(AllScenes.Select(s => s.Path));
            List<SceneEntry> orderedManagedBuildScenes = AllScenes.Where(s => s.InBuildSettings).ToList();
            List<EditorBuildSettingsScene> updatedBuildScenes = new();
            int nextManagedScene = 0;

            foreach (EditorBuildSettingsScene currentBuildScene in currentBuildScenes)
            {
                if (!managedScenePaths.Contains(currentBuildScene.path))
                {
                    updatedBuildScenes.Add(currentBuildScene);
                    continue;
                }

                if (nextManagedScene < orderedManagedBuildScenes.Count)
                {
                    SceneEntry scene = orderedManagedBuildScenes[nextManagedScene++];
                    updatedBuildScenes.Add(new EditorBuildSettingsScene(scene.Path, scene.Enabled));
                }
            }

            while (nextManagedScene < orderedManagedBuildScenes.Count)
            {
                SceneEntry scene = orderedManagedBuildScenes[nextManagedScene++];
                updatedBuildScenes.Add(new EditorBuildSettingsScene(scene.Path, scene.Enabled));
            }

            EditorBuildSettings.scenes = updatedBuildScenes.ToArray();
        }

        public void ToggleBuildStatus(SceneEntry scene)
        {
            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            if (scene.InBuildSettings) buildScenes.RemoveAll(b => b.path == scene.Path);
            else buildScenes.Add(new EditorBuildSettingsScene(scene.Path, true));

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Refresh();
        }

        public static bool OpenScene(string path, bool additive)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot open scenes from the selector while the Editor is in Play mode.");
                return false;
            }

            try
            {
                if (additive)
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    return true;
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Scene Selector: could not open '" + path + "'.\n" + exception.Message);
                return false;
            }
        }
    }
}
