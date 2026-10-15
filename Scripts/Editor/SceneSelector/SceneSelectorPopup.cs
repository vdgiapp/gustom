using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gustom.Editor
{
    public class SceneSelectorPopup : PopupWindowContent
    {
        private sealed class SceneEntry
        {
            public string Path;
            public string Name;
            public string Folder;
            public bool InBuildSettings;
            public bool Enabled;
            public int BuildOrder;
            public int BuildIndex;
        }

        private readonly List<SceneEntry> allScenes = new();
        private readonly List<SceneEntry> visibleScenes = new();
        private Rect[] sceneRowRects = Array.Empty<Rect>();
        private ReorderableList reorderableList;
        private Vector2 scrollPosition;
        private string search = string.Empty;
        private int highlightedIndex;
        private bool focusSearch = true;
        private Texture sceneIcon;
        private int pressedSceneIndex = -1;
        private int dragControlId;
        private int dropTargetIndex = -1;
        private int dropInsertionIndex = -1;
        private Vector2 mouseDownPosition;
        private Rect pressedSceneRect;
        private string pressedScenePath;
        private bool isDraggingScene;
        private EditorWindow hostWindow;
        private bool isInitialized;

        private static GUIStyle rightInfoStyle;
        private static GUIStyle leftIndexStyle;
        private static GUIStyle searchIconStyle;

        public override Vector2 GetWindowSize()
        {
            return new Vector2(340f, 420f);
        }

        public override void OnOpen()
        {
            if (!isInitialized) 
            {
                Initialize();
            }
        }
        
        public override void OnClose()
        {
            base.OnClose();
            Unsubscribe();
        }

        public void InitializeForWindow(EditorWindow window)
        {
            hostWindow = window;
            Initialize();
        }

        private EditorWindow HostWindow => hostWindow ? hostWindow : editorWindow;

        private void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;
            if (HostWindow) HostWindow.wantsMouseMove = true;
            sceneIcon = AssetPreview.GetMiniTypeThumbnail(typeof(SceneAsset));
            if (!sceneIcon) sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image;
            
            EditorApplication.delayCall += () => 
            {
                RefreshSceneData();
                FocusSearchField();
            };

            SceneAssetChangeWatcher.OnScenesChanged += HandleScenesChanged;
        }
        
        private void HandleScenesChanged()
        {
            EditorApplication.delayCall += RefreshAndRepaint;
        }

        private void RefreshAndRepaint()
        {
            RefreshSceneData();
            HostWindow?.Repaint();
        }

        public void Unsubscribe()
        {
            SceneAssetChangeWatcher.OnScenesChanged -= HandleScenesChanged;
        }

        public override void OnGUI(Rect rect)
        {
            DrawSelectorGUI(rect);
        }

        public void DrawSelectorGUI(Rect rect)
        {
            if (Event.current.type == EventType.MouseMove && rect.Contains(Event.current.mousePosition)) 
            {
                HostWindow?.Repaint();
            }
            HandleKeyboard();
            DrawSearchToolbar();

            if (visibleScenes.Count == 0)
            {
                GUILayout.Space(18f);
                GUILayout.Label(allScenes.Count == 0 ? "No scenes in this project" : "No scenes match your search", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
                reorderableList.DoLayoutList();
                EditorGUILayout.EndScrollView();
            }

            Rect separatorRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(separatorRect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
            GUILayout.Label("Click/Enter: Open  ·  Ctrl+Open: Additive  ·  Right-click: Options", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(16f));
        }

        private void FocusSearchField()
        {
            if (!HostWindow) return;
            EditorGUI.FocusTextInControl(SceneSelectorEditor.SearchControlName);
            HostWindow.Repaint();
        }

        private void DrawSearchToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName(SceneSelectorEditor.SearchControlName);
            string nextSearch = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
            {
                search = nextSearch;
                highlightedIndex = 0;
                RebuildVisibleScenes();
            }

            if (focusSearch)
            {
                EditorGUI.FocusTextInControl(SceneSelectorEditor.SearchControlName);
                focusSearch = false;
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
            {
                RefreshSceneData();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void HandleKeyboard()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown)
            {
                if (currentEvent.keyCode == KeyCode.DownArrow)
                {
                    if (visibleScenes.Count > 0)
                    {
                        highlightedIndex = Mathf.Clamp(highlightedIndex + 1, 0, visibleScenes.Count - 1);
                        ScrollToHighlight();
                    }
                    currentEvent.Use();
                }
                else if (currentEvent.keyCode == KeyCode.UpArrow)
                {
                    if (visibleScenes.Count > 0)
                    {
                        highlightedIndex = Mathf.Clamp(highlightedIndex - 1, 0, visibleScenes.Count - 1);
                        ScrollToHighlight();
                    }
                    currentEvent.Use();
                }
                else if (currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter && visibleScenes.Count > 0)
                {
                    SceneEntry selectedScene = visibleScenes[Mathf.Clamp(highlightedIndex, 0, visibleScenes.Count - 1)];
                    if (OpenScene(selectedScene.Path, currentEvent.control || currentEvent.command))
                    {
                        if (!hostWindow) HostWindow?.Close();
                    }
                    currentEvent.Use();
                }
                else if (currentEvent.keyCode == KeyCode.Escape)
                {
                    HostWindow?.Close();
                    currentEvent.Use();
                }
            }
        }

        private void ScrollToHighlight()
        {
            scrollPosition.y = highlightedIndex * SceneSelectorEditor.RowHeight;
            HostWindow?.Repaint();
        }

        private void RefreshSceneData()
        {
            allScenes.Clear();

            Dictionary<string, EditorBuildSettingsScene> buildSettingsByPath = new Dictionary<string, EditorBuildSettingsScene>();
            Dictionary<string, int> buildOrderByPath = new Dictionary<string, int>();
            EditorBuildSettingsScene[] buildSettings = EditorBuildSettings.scenes;
            for (int index = 0; index < buildSettings.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildSettings[index];
                if (buildSettingsByPath.TryAdd(buildScene.path, buildScene))
                {
                    buildOrderByPath.Add(buildScene.path, index);
                }
            }

            Dictionary<string, int> buildIndexByPath = new Dictionary<string, int>();
            int nextBuildIndex = 0;
            for (int index = 0; index < buildSettings.Length; index++)
            {
                EditorBuildSettingsScene buildScene = buildSettings[index];
                if (buildScene.enabled)
                {
                    buildIndexByPath[buildScene.path] = nextBuildIndex;
                    nextBuildIndex++;
                }
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
                allScenes.Add(new SceneEntry
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    Folder = folder,
                    InBuildSettings = isInBuildSettings,
                    Enabled = isInBuildSettings && buildScene.enabled,
                    BuildOrder = isInBuildSettings ? buildOrderByPath[path] : int.MaxValue,
                    BuildIndex = buildIndexByPath.GetValueOrDefault(path, -1)
                });
            }

            allScenes.Sort(CompareScenes);
            RebuildVisibleScenes();
            CreateReorderableList();
            highlightedIndex = visibleScenes.Count == 0 ? 0 : Mathf.Clamp(highlightedIndex, 0, visibleScenes.Count - 1);
            HostWindow?.Repaint();
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

        private void RebuildVisibleScenes()
        {
            visibleScenes.Clear();
            foreach (SceneEntry scene in allScenes)
            {
                if (string.IsNullOrEmpty(search) || scene.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || scene.Folder.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visibleScenes.Add(scene);
                }
            }

            sceneRowRects = new Rect[visibleScenes.Count];
            if (reorderableList != null)
            {
                reorderableList.list = visibleScenes;
            }
        }

        private void CreateReorderableList()
        {
            reorderableList = new ReorderableList(visibleScenes, typeof(SceneEntry), false, false, false, false)
            {
                elementHeight = SceneSelectorEditor.RowHeight,
                drawElementCallback = DrawSceneElement,
                onReorderCallbackWithDetails = OnScenesReordered
            };
        }

        private void DrawSceneElement(Rect rowRect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= visibleScenes.Count) return;

            SceneEntry scene = visibleScenes[index];
            Event currentEvent = Event.current;
            int rowControlId = GUIUtility.GetControlID(FocusType.Passive, rowRect);
            if (currentEvent.type == EventType.Repaint) sceneRowRects[index] = rowRect;
            Rect rowVisualRect = new Rect(rowRect.x - 4f, rowRect.y, rowRect.width + 8f, rowRect.height);
            bool isHighlighted = index == highlightedIndex;
            bool isActiveScene = SceneManager.GetActiveScene().path == scene.Path;
            bool isLoaded = EditorSceneManager.GetSceneByPath(scene.Path).isLoaded;

            if (currentEvent.type == EventType.Repaint)
            {
                if (isActiveScene)
                {
                    EditorGUI.DrawRect(rowVisualRect, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.42f, 0.24f, 0.76f) : new Color(0.24f, 0.62f, 0.34f, 0.28f));
                    EditorGUI.DrawRect(new Rect(rowVisualRect.x, rowVisualRect.y, 2f, rowVisualRect.height), new Color(0.30f, 0.78f, 0.42f, 1f));
                }
                else if (rowVisualRect.Contains(currentEvent.mousePosition) || isHighlighted)
                {
                    EditorGUI.DrawRect(rowVisualRect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, isHighlighted ? 0.09f : 0.045f) : new Color(0f, 0f, 0f, isHighlighted ? 0.08f : 0.04f));
                }

                if (isDraggingScene && index == pressedSceneIndex)
                {
                    EditorGUI.DrawRect(rowVisualRect, EditorGUIUtility.isProSkin ? new Color(0.35f, 0.65f, 1f, 0.16f) : new Color(0.1f, 0.35f, 0.8f, 0.12f));
                }

                if (isDraggingScene && (index == dropInsertionIndex || (index == visibleScenes.Count - 1 && dropInsertionIndex == visibleScenes.Count)))
                {
                    float markerY = dropInsertionIndex == visibleScenes.Count ? rowRect.yMax - 2f : rowRect.y;
                    EditorGUI.DrawRect(new Rect(rowVisualRect.x, markerY, rowVisualRect.width, 2f), EditorGUIUtility.isProSkin ? new Color(0.35f, 0.65f, 1f, 0.9f) : new Color(0.1f, 0.35f, 0.8f, 0.9f));
                }
            }

            Rect iconRect = new Rect(rowRect.x + 6f, rowRect.y + 4f, 16f, 16f);
            Rect pingRect = new Rect(rowRect.xMax - 21f, rowRect.y + (rowRect.height - 16f) * 0.5f, 18f, 18f);
            bool hover = rowRect.Contains(currentEvent.mousePosition);
            float infoRight = hover ? pingRect.x - 3f : rowRect.xMax - 6f;
            Rect infoRect = new Rect(rowRect.xMax - 104f, rowRect.y, Mathf.Max(0f, infoRight - (rowRect.xMax - 104f)), rowRect.height);
            float contentLeft = iconRect.xMax + 4f;
            float nameLeft = contentLeft;

            if (scene.InBuildSettings)
            {
                string buildIndexText = scene.Enabled ? scene.BuildIndex.ToString() : "–";
                GUIContent buildIndexContent = new GUIContent(buildIndexText, "Build index. Right-click for Build Settings options.");
                GUIStyle indexStyle = GetLeftIndexStyle();
                float indexWidth = indexStyle.CalcSize(buildIndexContent).x;
                Rect indexRect = new Rect(contentLeft, rowRect.y, indexWidth, rowRect.height);
                GUI.Label(indexRect, buildIndexContent, indexStyle);
                nameLeft = indexRect.xMax + 4f;
            }

            Rect nameRect = new Rect(nameLeft, rowRect.y, Mathf.Max(0f, infoRect.x - nameLeft - 5f), rowRect.height);
            if (sceneIcon) GUI.DrawTexture(iconRect, sceneIcon, ScaleMode.ScaleToFit, true);

            GUIStyle nameStyle = isActiveScene ? EditorStyles.boldLabel : EditorStyles.label;
            GUIContent sceneContent = new GUIContent(scene.Name, scene.Path);
            GUI.Label(nameRect, sceneContent, nameStyle);

            string sceneInfo = isActiveScene ? "[active]" : isLoaded ? "[loaded]" : scene.Folder;
            if (!string.IsNullOrEmpty(sceneInfo)) GUI.Label(infoRect, sceneInfo, GetRightInfoStyle());

            // searchIconStyle ??= new GUIStyle(EditorStyles.iconButton)
            // {
            //     alignment = TextAnchor.MiddleCenter,
            // };
            //
            // if (hover && GUI.Button(pingRect, EditorGUIUtility.IconContent("Search Icon"), searchIconStyle))
            // {
            //     EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path));
            // }
            
            if (hover)
            {
                GUIContent searchIcon = EditorGUIUtility.IconContent("Search Icon");
                GUI.DrawTexture(pingRect, searchIcon.image, ScaleMode.ScaleToFit);

                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && pingRect.Contains(currentEvent.mousePosition))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path));
                    currentEvent.Use();
                }
            }

            if (currentEvent.type == EventType.ContextClick && rowRect.Contains(currentEvent.mousePosition))
            {
                ShowSceneContextMenu(scene);
                currentEvent.Use();
            }
            else
            {
                HandleSceneRowInput(rowRect, pingRect, index, scene.Path, rowControlId);
            }
        }

        private void HandleSceneRowInput(Rect rowRect, Rect pingRect, int index, string scenePath, int rowControlId)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition) && !pingRect.Contains(currentEvent.mousePosition))
            {
                pressedSceneIndex = index;
                dragControlId = rowControlId;
                dropTargetIndex = index;
                dropInsertionIndex = index;
                mouseDownPosition = currentEvent.mousePosition;
                pressedSceneRect = rowRect;
                pressedScenePath = scenePath;
                isDraggingScene = false;
                GUIUtility.hotControl = rowControlId;
                currentEvent.Use();
                return;
            }

            if (pressedSceneIndex < 0) return;

            switch (currentEvent.type)
            {
                case EventType.MouseDrag:
                {
                    if (!isDraggingScene && (currentEvent.mousePosition - mouseDownPosition).sqrMagnitude >= 16f)
                    {
                        isDraggingScene = true;
                    }

                    if (isDraggingScene)
                    {
                        UpdateDropTarget(currentEvent.mousePosition);
                        HostWindow?.Repaint();
                    }

                    currentEvent.Use();
                    break;
                }
                case EventType.MouseUp when currentEvent.button == 0:
                {
                    if (!isDraggingScene && (currentEvent.mousePosition - mouseDownPosition).sqrMagnitude >= 16f)
                    {
                        isDraggingScene = true;
                    }

                    bool wasDragging = isDraggingScene;
                    int sourceIndex = pressedSceneIndex;
                    bool additiveOpen = currentEvent.control || currentEvent.command;
                    if (wasDragging) UpdateDropTarget(currentEvent.mousePosition);
                    int targetIndex = dropTargetIndex;
                    bool openScene = !wasDragging
                                     && pressedSceneRect.Contains(currentEvent.mousePosition)
                                     && !GetPingRect(pressedSceneRect).Contains(currentEvent.mousePosition);
                    string openScenePath = pressedScenePath;
                    int controlId = dragControlId;

                    ResetSceneRowDrag();
                    if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
                    currentEvent.Use();

                    if (wasDragging)
                    {
                        ReorderBuildScene(sourceIndex, targetIndex);
                    }
                    else if (openScene && OpenScene(openScenePath, additiveOpen))
                    {
                        HostWindow?.Repaint();
                    }

                    break;
                }
            }
        }

        private void UpdateDropTarget(Vector2 mousePosition)
        {
            dropTargetIndex = -1;
            dropInsertionIndex = -1;
            for (int index = 0; index < sceneRowRects.Length; index++)
            {
                Rect rowRect = sceneRowRects[index];
                if (rowRect.Contains(mousePosition))
                {
                    int insertionIndex = index + (mousePosition.y >= rowRect.center.y ? 1 : 0);
                    dropInsertionIndex = insertionIndex;
                    if (insertionIndex > pressedSceneIndex) insertionIndex--;
                    dropTargetIndex = Mathf.Clamp(insertionIndex, 0, visibleScenes.Count - 1);
                    return;
                }
            }
        }

        private static Rect GetPingRect(Rect rowRect)
        {
            return new Rect(rowRect.xMax - 21f, rowRect.y + (rowRect.height - 16f) * 0.5f, 18f, 18f);
        }

        private void ReorderBuildScene(int oldIndex, int newIndex)
        {
            int visibleBuildSceneCount = visibleScenes.Count(scene => scene.InBuildSettings);
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= visibleBuildSceneCount || newIndex >= visibleBuildSceneCount || oldIndex == newIndex) return;

            SceneEntry scene = visibleScenes[oldIndex];
            visibleScenes.RemoveAt(oldIndex);
            visibleScenes.Insert(newIndex, scene);
            OnScenesReordered(reorderableList, oldIndex, newIndex);
        }

        private void ResetSceneRowDrag()
        {
            pressedSceneIndex = -1;
            dragControlId = 0;
            dropTargetIndex = -1;
            dropInsertionIndex = -1;
            isDraggingScene = false;
            pressedSceneRect = default;
            pressedScenePath = null;
        }

        private static GUIStyle GetRightInfoStyle()
        {
            rightInfoStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };

            return rightInfoStyle;
        }

        private static GUIStyle GetLeftIndexStyle()
        {
            leftIndexStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            return leftIndexStyle;
        }

        private void ShowSceneContextMenu(SceneEntry scene)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Single (close others)"), false, () => OpenScene(scene.Path, false));
            menu.AddItem(new GUIContent("Open Additively"), false, () => OpenScene(scene.Path, true));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(scene.InBuildSettings ? "Remove from Build Settings" : "Add to Build Settings"), scene.InBuildSettings, () => ToggleBuildStatus(scene));
            menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path)));
            menu.ShowAsContext();
        }

        private void ToggleBuildStatus(SceneEntry scene)
        {
            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            if (scene.InBuildSettings)
            {
                buildScenes.RemoveAll(buildScene => buildScene.path == scene.Path);
            }
            else
            {
                buildScenes.Add(new EditorBuildSettingsScene(scene.Path, true));
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            RefreshSceneData();
        }

        private void OnScenesReordered(ReorderableList list, int oldIndex, int newIndex)
        {
            int visibleBuildSceneCount = visibleScenes.Count(scene => scene.InBuildSettings);
            if (oldIndex < 0 || newIndex < 0 || oldIndex >= visibleBuildSceneCount || newIndex >= visibleBuildSceneCount)
            {
                EditorApplication.delayCall += RefreshSceneData;
                return;
            }

            List<SceneEntry> reorderedVisibleBuildScenes = visibleScenes.Where(scene => scene.InBuildSettings).ToList();
            HashSet<SceneEntry> reorderedVisibleSet = new HashSet<SceneEntry>(reorderedVisibleBuildScenes);
            List<int> masterBuildSlots = new List<int>();
            for (int index = 0; index < allScenes.Count; index++)
            {
                if (reorderedVisibleSet.Contains(allScenes[index])) masterBuildSlots.Add(index);
            }

            for (int index = 0; index < masterBuildSlots.Count; index++)
            {
                allScenes[masterBuildSlots[index]] = reorderedVisibleBuildScenes[index];
            }

            ApplyBuildSceneOrder();
            EditorApplication.delayCall += RefreshSceneData;
        }

        private void ApplyBuildSceneOrder()
        {
            EditorBuildSettingsScene[] currentBuildScenes = EditorBuildSettings.scenes;
            HashSet<string> managedScenePaths = new HashSet<string>(allScenes.Select(scene => scene.Path));
            List<SceneEntry> orderedManagedBuildScenes = allScenes.Where(scene => scene.InBuildSettings).ToList();
            List<EditorBuildSettingsScene> updatedBuildScenes = new List<EditorBuildSettingsScene>();
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

        private static bool OpenScene(string path, bool additive)
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