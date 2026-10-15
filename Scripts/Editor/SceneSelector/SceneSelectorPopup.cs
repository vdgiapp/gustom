using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gustom.Editor
{
    public class SceneSelectorPopup : PopupWindowContent
    {
        private readonly Color _hoverColor = new(0.5f, 0.5f, 0.5f, 0.2f);
        private readonly Color _altRowColor = new(0f, 0f, 0f, 0.08f);

        private readonly SceneSelectorStore _store = SceneSelectorStore.Shared;

        private readonly List<SceneEntry> _visibleScenes = new();
        private Rect[] _sceneRowRects = Array.Empty<Rect>();
        private Vector2 _scrollPosition;
        private string _search = string.Empty;
        private SceneSelectorTab _activeTab = SceneSelectorTab.All;
        private bool _showSettings;
        private bool _focusSearch = true;
        private Texture _sceneIcon;
        private int _pressedSceneIndex = -1;
        private int _dragControlId;
        private int _dropTargetIndex = -1;
        private int _dropInsertionIndex = -1;
        private Vector2 _mouseDownPosition;
        private Rect _pressedSceneRect;
        private string _pressedScenePath;
        private bool _isDraggingScene;
        private bool _pressedCanReorder;
        private EditorWindow _hostWindow;
        private bool _isInitialized;
        private GUIStyle _pinButtonStyle;
        private float RowHeight => _store.DisplaySize == SceneDisplaySize.Large
            ? GustomEditorGUI.LargeListRowHeight
            : GustomEditorGUI.CompactListRowHeight;

        public override Vector2 GetWindowSize() => new(340f, 420f);

        public override void OnOpen()
        {
            if (!_isInitialized) Initialize();
        }

        public override void OnClose()
        {
            base.OnClose();
            Unsubscribe();
        }

        public void InitializeForWindow(EditorWindow window)
        {
            _hostWindow = window;
            Initialize();
        }

        private EditorWindow HostWindow => _hostWindow ? _hostWindow : editorWindow;

        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            if (HostWindow) HostWindow.wantsMouseMove = true;
            _sceneIcon = AssetPreview.GetMiniTypeThumbnail(typeof(SceneAsset));
            if (!_sceneIcon) _sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image;

            _store.Changed += OnStoreChanged;

            EditorApplication.delayCall += () =>
            {
                _store.Refresh();
                FocusSearchField();
            };

            SceneAssetChangeWatcher.OnScenesChanged += HandleScenesChanged;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
        }

        private void OnStoreChanged()
        {
            RebuildVisibleScenes();
            RefreshSceneRows();
            HostWindow?.Repaint();
        }

        private void HandleScenesChanged() => EditorApplication.delayCall += _store.Refresh;

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene) => HostWindow?.Repaint();

        public void Unsubscribe()
        {
            _store.Changed -= OnStoreChanged;
            SceneAssetChangeWatcher.OnScenesChanged -= HandleScenesChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
        }

        public override void OnGUI(Rect rect) => DrawSelectorGUI(rect);

        public void DrawSelectorGUI(Rect rect)
        {
            if (Event.current.type == EventType.MouseMove && rect.Contains(Event.current.mousePosition))
                HostWindow?.Repaint();

            DrawSearchToolbar();
            DrawControlsBar();
            if (_showSettings) DrawSettingsArea();
            DrawTabsBar();
            EditorGUILayout.Space(2f);

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            if (_visibleScenes.Count == 0)
            {
                GUILayout.Space(12f);
                string emptyMessage = _store.AllScenes.Count == 0
                    ? "No scenes in this project"
                    : _activeTab == SceneSelectorTab.Pinned
                        ? "No pinned scenes"
                    : _activeTab == SceneSelectorTab.Builds
                        ? "No scenes in Build Settings"
                        : "No scenes match your search";
                GUILayout.Label(emptyMessage, EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
                for (int index = 0; index < _visibleScenes.Count; index++)
                {
                    Rect rowRect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
                    DrawSceneElement(rowRect, index);
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            GustomEditorGUI.DrawFooterGuide("Click: Open  ·  Ctrl+Click: Additive  ·  Right-click: Options");
        }

        private void DrawControlsBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(EditorGUIUtility.IconContent("Settings"), EditorStyles.toolbarButton, GUILayout.Width(25f)))
                _showSettings = !_showSettings;
            GUILayout.FlexibleSpace();
            //if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
            if (GUILayout.Button(new GUIContent("Refresh", "Update the list of scenes in the project."), EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                _store.Refresh();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsArea()
        {
            EditorGUILayout.BeginVertical(GustomEditorGUI.SettingsBoxStyle);
            bool showStripedRows = EditorGUILayout.ToggleLeft("Striped rows", _store.ShowStripedRows);
            bool showSceneIcon = EditorGUILayout.ToggleLeft("Show scene icon", _store.ShowSceneIcon);
            bool showBuildIndex = EditorGUILayout.ToggleLeft("Show build index", _store.ShowBuildIndex);
            bool showSceneState = EditorGUILayout.ToggleLeft("Show status", _store.ShowSceneState);
            SceneDisplaySize displaySize = (SceneDisplaySize)EditorGUILayout.EnumPopup("Display size", _store.DisplaySize);
            if (showSceneIcon != _store.ShowSceneIcon || showBuildIndex != _store.ShowBuildIndex
                || showSceneState != _store.ShowSceneState || displaySize != _store.DisplaySize
                || showStripedRows != _store.ShowStripedRows)
            {
                _store.SetDisplayOptions(showSceneIcon, showBuildIndex, showSceneState, displaySize, showStripedRows);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTabsBar()
        {
            string[] tabLabels =
            {
                "All",
                "Builds",
                "Pinned"
            };
            int selectedTab = (int)_activeTab;
            int nextTab = GUILayout.Toolbar(selectedTab, tabLabels, EditorStyles.toolbarButton);
            if (nextTab != selectedTab)
            {
                _scrollPosition = Vector2.zero;
                _activeTab = (SceneSelectorTab)nextTab;
                RebuildVisibleScenes();
                RefreshSceneRows();
            }
        }

        private void RefreshSceneRows()
        {
            _sceneRowRects = new Rect[_visibleScenes.Count];
        }

        private void RebuildVisibleScenes()
        {
            _visibleScenes.Clear();
            foreach (SceneEntry scene in _store.AllScenes)
            {
                if (_activeTab == SceneSelectorTab.Pinned && !scene.IsPinned) continue;
                if (_activeTab == SceneSelectorTab.Builds && !scene.InBuildSettings) continue;

                if (string.IsNullOrEmpty(_search)
                    || scene.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    || scene.Folder.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _visibleScenes.Add(scene);
                }
            }
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
            string nextSearch = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (!string.Equals(nextSearch, _search, StringComparison.Ordinal))
            {
                _search = nextSearch;
                _scrollPosition = Vector2.zero;
                RebuildVisibleScenes();
                RefreshSceneRows();
            }

            if (_focusSearch)
            {
                EditorGUI.FocusTextInControl(SceneSelectorEditor.SearchControlName);
                _focusSearch = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneElement(Rect rowRect, int index)
        {
            if (index < 0 || index >= _visibleScenes.Count) return;

            SceneEntry scene = _visibleScenes[index];
            Event currentEvent = Event.current;
            int rowControlId = GUIUtility.GetControlID(FocusType.Passive, rowRect);
            if (currentEvent.type == EventType.Repaint) _sceneRowRects[index] = rowRect;
            bool isActiveScene = SceneManager.GetActiveScene().path == scene.Path;
            bool isLoaded = EditorSceneManager.GetSceneByPath(scene.Path).isLoaded;
            bool isHovered = rowRect.Contains(currentEvent.mousePosition);

            if (currentEvent.type == EventType.Repaint)
            {
                GustomEditorGUI.DrawRowBackground(rowRect, index, isActiveScene, isHovered, _store.ShowStripedRows, _altRowColor, _hoverColor);

                if (_isDraggingScene && index == _pressedSceneIndex)
                    EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin ? new Color(0.35f, 0.65f, 1f, 0.16f) : new Color(0.1f, 0.35f, 0.8f, 0.12f));

                if (_isDraggingScene && (index == _dropInsertionIndex || (index == _visibleScenes.Count - 1 && _dropInsertionIndex == _visibleScenes.Count)))
                {
                    float markerY = _dropInsertionIndex == _visibleScenes.Count ? rowRect.yMax - 2f : rowRect.y;
                    EditorGUI.DrawRect(new Rect(rowRect.x, markerY, rowRect.width, 2f), EditorGUIUtility.isProSkin ? new Color(0.35f, 0.65f, 1f, 0.9f) : new Color(0.1f, 0.35f, 0.8f, 0.9f));
                }
            }

            GUIStyle nameStyle = isActiveScene ? GustomEditorGUI.ActiveRowLabelStyle : GustomEditorGUI.RowLabelStyle;
            string sceneInfo = isActiveScene ? "[active]" : isLoaded ? "[loaded]" : scene.Folder;
            float sceneIconSize = _store.DisplaySize == SceneDisplaySize.Large
                ? GustomEditorGUI.LargeListIconSize
                : GustomEditorGUI.CompactListIconSize;
            GUIContent buildIndexContent = _store.ShowBuildIndex && scene.InBuildSettings
                ? new GUIContent(scene.Enabled ? scene.BuildIndex.ToString() : "–", "Build index. Right-click for Build Settings options.")
                : null;
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path);
            const float iconGap = 4f;
            const float pinSize = 18f;
            Rect pinnedRect = new(rowRect.xMax - pinSize - 3f, rowRect.y + (rowRect.height - pinSize) * 0.5f, pinSize, pinSize);
            Rect pingRect = new(pinnedRect.x - 19f, rowRect.y + (rowRect.height - 16f) * 0.5f, 18f, 16f);
            _pinButtonStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            _pinButtonStyle.normal.textColor = scene.IsPinned
                ? EditorGUIUtility.isProSkin ? Color.yellow : new Color(0.9f, 0.7f, 0.1f)
                : Color.gray;
            if (GUI.Button(pinnedRect, scene.IsPinned ? "★" : "☆", _pinButtonStyle))
            {
                _store.TogglePinned(scene);
                GUIUtility.ExitGUI();
            }

            float contentX = rowRect.x + 8f;
            if (_store.ShowSceneIcon && _sceneIcon)
            {
                Rect iconRect = new(contentX, rowRect.y + (rowRect.height - sceneIconSize) * 0.5f, sceneIconSize, sceneIconSize);
                GustomEditorGUI.DrawTexture(iconRect, _sceneIcon);
                contentX = iconRect.xMax + iconGap;
            }

            if (buildIndexContent != null)
            {
                float prefixWidth = GustomEditorGUI.CenteredMiniLabelStyle.CalcSize(buildIndexContent).x;
                Rect prefixRect = new(contentX, rowRect.y, prefixWidth, rowRect.height);
                GUI.Label(prefixRect, buildIndexContent, GustomEditorGUI.CenteredMiniLabelStyle);
                contentX = prefixRect.xMax + iconGap;
            }

            float textRight = isHovered && sceneAsset
                ? pingRect.x - 2f
                : pinnedRect.x - 2f;
            if (_store.ShowSceneState)
            {
                float trailingLeft = Mathf.Max(contentX, textRight - 100f);
                Rect statusRect = new(trailingLeft, rowRect.y, Mathf.Max(0f, textRight - trailingLeft), rowRect.height);
                GUI.Label(statusRect, sceneInfo, GustomEditorGUI.RightMiniLabelStyle);
                textRight = trailingLeft - 5f;
            }

            GUI.Label(new Rect(contentX, rowRect.y, Mathf.Max(0f, textRight - contentX), rowRect.height), new GUIContent(scene.Name, scene.Path), nameStyle);
            GustomEditorGUI.DrawPingButton(pingRect, sceneAsset, isHovered);

            if (currentEvent.type == EventType.ContextClick && rowRect.Contains(currentEvent.mousePosition))
            {
                ShowSceneContextMenu(scene);
                currentEvent.Use();
            }
            else if (currentEvent.type != EventType.MouseDown || !pinnedRect.Contains(currentEvent.mousePosition))
            {
                HandleSceneRowInput(rowRect, pingRect, index, scene.Path, rowControlId);
            }
        }

        private void HandleSceneRowInput(Rect rowRect, Rect pingRect, int index, string scenePath, int rowControlId)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition) && !pingRect.Contains(currentEvent.mousePosition))
            {
                _pressedSceneIndex = index;
                _dragControlId = rowControlId;
                _dropTargetIndex = index;
                _dropInsertionIndex = index;
                _mouseDownPosition = currentEvent.mousePosition;
                _pressedSceneRect = rowRect;
                _pressedScenePath = scenePath;
                _isDraggingScene = false;
                _pressedCanReorder = _visibleScenes[index].InBuildSettings;
                GUIUtility.hotControl = rowControlId;
                currentEvent.Use();
                return;
            }

            if (_pressedSceneIndex < 0) return;

            switch (currentEvent.type)
            {
                case EventType.MouseDrag:
                {
                    if (!_isDraggingScene && (currentEvent.mousePosition - _mouseDownPosition).sqrMagnitude >= 16f)
                        _isDraggingScene = _pressedCanReorder;

                    if (_isDraggingScene)
                    {
                        UpdateDropTarget(currentEvent.mousePosition);
                        HostWindow?.Repaint();
                    }

                    currentEvent.Use();
                    break;
                }
                case EventType.MouseUp when currentEvent.button == 0:
                {
                    if (!_isDraggingScene && (currentEvent.mousePosition - _mouseDownPosition).sqrMagnitude >= 16f)
                        _isDraggingScene = _pressedCanReorder;

                    bool wasDragging = _isDraggingScene;
                    int sourceIndex = _pressedSceneIndex;
                    bool additiveOpen = currentEvent.control || currentEvent.command;
                    if (wasDragging) UpdateDropTarget(currentEvent.mousePosition);
                    int targetIndex = _dropTargetIndex;
                    bool openScene = !wasDragging
                                     && _pressedSceneRect.Contains(currentEvent.mousePosition)
                                     && !GetPingRect(_pressedSceneRect).Contains(currentEvent.mousePosition);
                    string openScenePath = _pressedScenePath;
                    int controlId = _dragControlId;

                    ResetSceneRowDrag();
                    if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
                    currentEvent.Use();

                    if (wasDragging)
                    {
                        _store.ReorderBuildScene(_visibleScenes, sourceIndex, targetIndex);
                    }
                    else if (openScene && SceneSelectorStore.OpenScene(openScenePath, additiveOpen))
                    {
                        HostWindow?.Repaint();
                    }

                    break;
                }
            }
        }

        private void UpdateDropTarget(Vector2 mousePosition)
        {
            _dropTargetIndex = -1;
            _dropInsertionIndex = -1;
            for (int index = 0; index < _sceneRowRects.Length; index++)
            {
                Rect rowRect = _sceneRowRects[index];
                if (rowRect.Contains(mousePosition))
                {
                    int insertionIndex = index + (mousePosition.y >= rowRect.center.y ? 1 : 0);
                    _dropInsertionIndex = insertionIndex;
                    if (insertionIndex > _pressedSceneIndex) insertionIndex--;
                    _dropTargetIndex = Mathf.Clamp(insertionIndex, 0, _visibleScenes.Count - 1);
                    return;
                }
            }
        }

        private static Rect GetPingRect(Rect rowRect) =>
            new(rowRect.xMax - 21f, rowRect.y + (rowRect.height - 16f) * 0.5f, 18f, 18f);

        private void ResetSceneRowDrag()
        {
            _pressedSceneIndex = -1;
            _dragControlId = 0;
            _dropTargetIndex = -1;
            _dropInsertionIndex = -1;
            _isDraggingScene = false;
            _pressedCanReorder = false;
            _pressedSceneRect = default;
            _pressedScenePath = null;
        }

        private void ShowSceneContextMenu(SceneEntry scene)
        {
            GenericMenu menu = new();
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.Path);
            menu.AddItem(new GUIContent("Select"), false, () => Selection.activeObject = sceneAsset);
            menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(sceneAsset));

            if (File.Exists(scene.Path))
                menu.AddItem(new GUIContent("Open Containing Folder"), false, () => EditorUtility.RevealInFinder(scene.Path));

            menu.AddItem(new GUIContent("Open"), false, () => SceneSelectorStore.OpenScene(scene.Path, false));
            menu.AddItem(new GUIContent("Open Additively"), false, () => SceneSelectorStore.OpenScene(scene.Path, true));

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Copy GUID"), false, () => EditorGUIUtility.systemCopyBuffer = scene.Guid);
            menu.AddItem(new GUIContent("Copy Path"), false, () => EditorGUIUtility.systemCopyBuffer = scene.Path);
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent(scene.IsPinned ? "Remove from Pinned" : "Add to Pinned"), scene.IsPinned, () => _store.TogglePinned(scene));
            menu.AddItem(new GUIContent(scene.InBuildSettings ? "Remove from Build Settings" : "Add to Build Settings"), scene.InBuildSettings, () => _store.ToggleBuildStatus(scene));
            menu.ShowAsContext();
        }

    }
}
