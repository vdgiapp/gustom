using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gustom.Editor
{
    public sealed class SelectionHistoryWindow : EditorWindow
    {
        private enum RowSize { Compact, Large }

        private const float ReorderThreshold = 5f;
        private const string StripedRowsPreferenceKey = "Gustom.SelectionHistory.StripedRows";

        private readonly Color _hoverColor = new(0.5f, 0.5f, 0.5f, 0.2f);
        private readonly Color _altRowColor = new(0f, 0f, 0f, 0.08f);

        private readonly SelectionHistoryStore _store = new();

        private readonly string[] _tabLabels = { "All", "History", "Pinned" };
        private int _currentTab;
        private string _searchQuery = "";
        private bool _showSettings;
        private RowSize _rowSize = RowSize.Compact;

        private bool _showStripedRows = false;
        private bool _showIndicatorIcons = true;
        private bool _showTypeName = true;
        private bool _moveSelectedToTopOnClick = true;

        private Vector2 _scrollPosition;

        private float CurrentRowHeight => _rowSize == RowSize.Large
            ? GustomEditorGUI.LargeListRowHeight
            : GustomEditorGUI.CompactListRowHeight;
        private float CurrentIconSize => _rowSize == RowSize.Large
            ? GustomEditorGUI.LargeListIconSize
            : GustomEditorGUI.CompactListIconSize;

        private GUIStyle _searchFieldStyle;
        private GUIStyle _searchCancelStyle;
        private GUIStyle _rowLabelStyle;
        private GUIStyle _rowLabelInvalidStyle;
        private GUIStyle _settingsBoxStyle;
        private GUIStyle _pinButtonStyle;
        private bool _stylesReady;

        private Texture2D _sceneIcon;
        private Texture2D _projectIcon;

        private SelectionHistoryEntry _draggedEntry;
        private Vector2 _dragStartPosition;
        private bool _isDraggingOut;
        private bool _isReordering;
        private SelectionHistoryEntry _reorderTarget;

        [MenuItem("Tools/Gustom/Selection History")]
        public static void ShowWindow()
        {
            SelectionHistoryWindow window = GetWindow<SelectionHistoryWindow>("Selection History");
            window.minSize = new Vector2(350, 300);
        }

        private void OnEnable()
        {
            _showStripedRows = EditorPrefs.GetBool(StripedRowsPreferenceKey, true);
            _store.Load();
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            _store.Changed += Repaint;
            SelectionHistoryStore.ExternalAssetRemovalRequested += OnExternalAssetRemovalRequested;
        }

        private void OnDisable()
        {
            _store.Save();
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            _store.Changed -= Repaint;
            SelectionHistoryStore.ExternalAssetRemovalRequested -= OnExternalAssetRemovalRequested;
        }

        private void OnExternalAssetRemovalRequested(string assetGuid)
        {
            SelectionHistoryEntry entry = _store.Entries.Find(item => item.AssetGuid == assetGuid);
            if (entry != null)
                _store.Remove(entry);
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject) _store.Add(Selection.activeObject);
        }

        private void OnHierarchyChanged() => _store.RefreshValidity();

        private void OnGUI()
        {
            InitStyles();
            HandleDragAndDropPinning();

            List<SelectionHistoryEntry> displayList = BuildDisplayList();

            DrawSearchBar();
            DrawControlsBar();
            if (_showSettings) DrawSettingsArea();
            DrawTabsBar();
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawList(displayList);
            EditorGUILayout.EndVertical();
            GustomEditorGUI.DrawFooterGuide("Click: Select  ·  Double-click: Open/Select  ·  Right-click: Options");
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _searchFieldStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? new GUIStyle(EditorStyles.toolbarTextField);
            _searchCancelStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? new GUIStyle(EditorStyles.miniButton);

            _rowLabelStyle = GustomEditorGUI.RowLabelStyle;
            _rowLabelInvalidStyle = new GUIStyle(_rowLabelStyle);
            _rowLabelInvalidStyle.normal.textColor = Color.grey;

            _settingsBoxStyle = GustomEditorGUI.SettingsBoxStyle;

            _sceneIcon = EditorGUIUtility.IconContent("d_SceneAsset Icon")?.image as Texture2D ?? EditorGUIUtility.IconContent("SceneAsset Icon")?.image as Texture2D;
            _projectIcon = EditorGUIUtility.IconContent("d_Project")?.image as Texture2D ?? EditorGUIUtility.IconContent("Project")?.image as Texture2D;
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            //GUILayout.Label("Filter:", EditorStyles.miniLabel, GUILayout.Width(35));

            EditorGUI.BeginChangeCheck();
            string newQuery = EditorGUILayout.TextField(_searchQuery, _searchFieldStyle, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                _searchQuery = newQuery;
            }

            // if (GUILayout.Button("✕", _searchCancelStyle, GUILayout.Width(18f)) && !string.IsNullOrEmpty(_searchQuery))
            // {
            //     _searchQuery = "";
            //     GUI.FocusControl(null);
            // }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawControlsBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(EditorGUIUtility.IconContent("Settings"), EditorStyles.toolbarButton, GUILayout.Width(25)))
                _showSettings = !_showSettings;

            GUILayout.FlexibleSpace();

            GUILayout.Label("MAX:", EditorStyles.miniLabel, GUILayout.Width(30));
            EditorGUI.BeginChangeCheck();
            int newMax = EditorGUILayout.IntField(_store.MaxHistorySize, EditorStyles.toolbarTextField, GUILayout.Width(35));
            if (EditorGUI.EndChangeCheck() && newMax >= 0)
            {
                _store.MaxHistorySize = newMax;
                _store.Save();
            }

            GUILayout.Space(5);

            // if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)) && EditorUtility.DisplayDialog("Clear History", "Remove all non-pinned items from history?", "Clear", "Cancel"))
            if (GUILayout.Button(new GUIContent("Clear", "Remove all non-pinned items from history."), EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                _store.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsArea()
        {
            EditorGUILayout.BeginVertical(_settingsBoxStyle);
            EditorGUI.BeginChangeCheck();

            _showStripedRows = EditorGUILayout.ToggleLeft("Striped rows", _showStripedRows);
            _showIndicatorIcons = EditorGUILayout.ToggleLeft("Show project/scene icon", _showIndicatorIcons);
            _showTypeName = EditorGUILayout.ToggleLeft("Show type", _showTypeName);
            _moveSelectedToTopOnClick = EditorGUILayout.ToggleLeft("Move clicked item to top", _moveSelectedToTopOnClick);
            _rowSize = (RowSize)EditorGUILayout.EnumPopup("Display size", _rowSize);

            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(StripedRowsPreferenceKey, _showStripedRows);

            EditorGUILayout.EndVertical();
        }

        private void DrawTabsBar()
        {
            int newTab = GUILayout.Toolbar(_currentTab, _tabLabels, EditorStyles.toolbarButton);
            if (newTab != _currentTab)
            {
                _currentTab = newTab;
                _scrollPosition = Vector2.zero;
            }
        }

        private List<SelectionHistoryEntry> BuildDisplayList()
        {
            List<SelectionHistoryEntry> result = new();
            string query = _searchQuery.Trim().ToLowerInvariant();

            foreach (SelectionHistoryEntry entry in _store.Entries)
            {
                if (_currentTab == 1 && entry.IsPinned) continue;
                if (_currentTab == 2 && !entry.IsPinned) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool nameMatch = entry.DisplayName().ToLowerInvariant().Contains(query);
                    bool pathMatch = entry.CachedPath != null && entry.CachedPath.ToLowerInvariant().Contains(query);
                    if (!nameMatch && !pathMatch) continue;
                }

                result.Add(entry);
            }

            return result;
        }

        // ====== List rows: pin, thumbnail, indicator, label, click/double-click, drag ======
        private void DrawList(List<SelectionHistoryEntry> displayList)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            Event currentEvent = Event.current;

            if (displayList.Count == 0)
            {
                GUILayout.Space(12f);
                GUILayout.Label("No items in this list", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }

            for (int i = 0; i < displayList.Count; i++)
            {
                SelectionHistoryEntry entry = displayList[i];
                Rect rowRect = GUILayoutUtility.GetRect(0, CurrentRowHeight, GUILayout.ExpandWidth(true));
                bool isHovered = rowRect.Contains(currentEvent.mousePosition);
                bool isSelected = entry.Target && Selection.activeObject == entry.Target;

                if (currentEvent.type == EventType.Repaint)
                {
                    GustomEditorGUI.DrawRowBackground(rowRect, i, isSelected, isHovered, _showStripedRows, _altRowColor, _hoverColor);
                    if (_isReordering && _reorderTarget == entry)
                        EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, rowRect.width, 2f), Color.cyan);
                }

                DrawRowContent(rowRect, entry, isHovered, isSelected, out Rect pinRect, out Rect pingRect);
                HandleRowInput(rowRect, entry, displayList, isHovered, pinRect, pingRect);

                if (currentEvent.type == EventType.ContextClick && isHovered)
                {
                    ShowContextMenu(entry);
                    currentEvent.Use();
                }
            }

            EditorGUILayout.EndScrollView();

            if (_isReordering && currentEvent.type == EventType.MouseUp)
            {
                if (_draggedEntry != null) _store.Reorder(_draggedEntry, _reorderTarget);
                _isReordering = false;
                _draggedEntry = null;
                _reorderTarget = null;
                currentEvent.Use();
            }
        }

        private void DrawRowContent(Rect rowRect, SelectionHistoryEntry entry, bool isHovered, bool isSelected, out Rect pinRect, out Rect pingRect)
        {
            Texture indicator = _showIndicatorIcons && !entry.IsFolder
                ? entry.IsAsset ? _projectIcon : _sceneIcon
                : null;
            Texture thumb = entry.Target ? AssetPreview.GetMiniThumbnail(entry.Target) : EditorGUIUtility.IconContent("console.warnicon.sml")?.image;
            float thumbSize = Mathf.Min(CurrentIconSize, rowRect.height - 4f);
            string labelText = entry.DisplayName();
            if (!_showTypeName && entry.Target)
            {
                labelText = string.IsNullOrEmpty(entry.Target.name) ? "[Unnamed Object]" : entry.Target.name;
            }

            GUIStyle style = !entry.Target
                ? _rowLabelInvalidStyle
                : isSelected ? GustomEditorGUI.ActiveRowLabelStyle : _rowLabelStyle;
            const float iconGap = 4f;
            const float pinSize = 18f;
            pinRect = new Rect(rowRect.xMax - pinSize - 3f, rowRect.y + (rowRect.height - pinSize) * 0.5f, pinSize, pinSize);
            pingRect = new Rect(pinRect.x - 19f, rowRect.y + (rowRect.height - 16f) * 0.5f, 18f, 16f);

            _pinButtonStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
            _pinButtonStyle.normal.textColor = entry.IsPinned
                ? EditorGUIUtility.isProSkin ? Color.yellow : new Color(0.9f, 0.7f, 0.1f)
                : Color.gray;
            if (GUI.Button(pinRect, entry.IsPinned ? "★" : "☆", _pinButtonStyle))
                _store.TogglePin(entry);

            float contentX = rowRect.x + 8f;
            if (indicator)
            {
                Rect indicatorRect = new(contentX, rowRect.y + (rowRect.height - CurrentIconSize) * 0.5f, CurrentIconSize, CurrentIconSize);
                GustomEditorGUI.DrawTexture(indicatorRect, indicator);
                contentX = indicatorRect.xMax + iconGap;
            }

            if (thumb)
            {
                Rect thumbRect = new(contentX, rowRect.y + (rowRect.height - thumbSize) * 0.5f, thumbSize, thumbSize);
                GustomEditorGUI.DrawTexture(thumbRect, thumb);
                contentX = thumbRect.xMax + iconGap;
            }

            float textRight = isHovered && entry.Target ? pingRect.x - 2f : pinRect.x - 2f;
            GUI.Label(new Rect(contentX, rowRect.y, Mathf.Max(0f, textRight - contentX), rowRect.height),
                new GUIContent(labelText, entry.Target ? entry.CachedPath : entry.DisplayName()), style);
            GustomEditorGUI.DrawPingButton(pingRect, entry.Target, isHovered);
        }

        private void HandleRowInput(Rect rowRect, SelectionHistoryEntry entry, List<SelectionHistoryEntry> displayList, bool isHovered, Rect pinRect, Rect pingRect)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && isHovered
                && !pinRect.Contains(currentEvent.mousePosition) && !pingRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.clickCount == 2)
                {
                    if (entry.Target)
                    {
                        if (entry.IsAsset) AssetDatabase.OpenAsset(entry.Target);
                        else SelectObject(entry);
                    }
                    currentEvent.Use();
                    return;
                }

                _draggedEntry = entry;
                _dragStartPosition = currentEvent.mousePosition;
                _isDraggingOut = false;

                if (_currentTab == 2 && entry.IsPinned)
                {
                    _isReordering = false; // Only activate it when the threshold is exceeded during MouseDrag.
                }

                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && _draggedEntry == entry)
            {
                float dragDistance = Vector2.Distance(currentEvent.mousePosition, _dragStartPosition);
                if (dragDistance > ReorderThreshold)
                {
                    if (_currentTab == 2 && entry.IsPinned)
                    {
                        _isReordering = true;
                        _reorderTarget = FindReorderTarget(displayList, currentEvent.mousePosition, rowRect, entry);
                        Repaint();
                    }
                    else if (!_isDraggingOut && entry.Target)
                    {
                        _isDraggingOut = true;
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new[] { entry.Target };
                        DragAndDrop.StartDrag(entry.DisplayName());
                    }
                    currentEvent.Use();
                }
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && _draggedEntry == entry)
            {
                if (!_isReordering && !_isDraggingOut
                    && !pinRect.Contains(currentEvent.mousePosition)
                    && !pingRect.Contains(currentEvent.mousePosition))
                {
                    SelectObject(entry);
                }
                _draggedEntry = null;
                _isDraggingOut = false;
                currentEvent.Use();
            }
        }

        private SelectionHistoryEntry FindReorderTarget(List<SelectionHistoryEntry> displayList, Vector2 mousePos, Rect currentRowRect, SelectionHistoryEntry current)
        {
            // Simplified: only checks whether the mouse is above or below the current row's
            // center to decide whether to insert before or after it.            
            int currentIndex = displayList.IndexOf(current);
            if (mousePos.y < currentRowRect.center.y) return current;
            return currentIndex + 1 < displayList.Count ? displayList[currentIndex + 1] : null;
        }

        private void SelectObject(SelectionHistoryEntry entry)
        {
            if (!entry.Target) return;
            if (_moveSelectedToTopOnClick) _store.Add(entry.Target);
            Selection.activeObject = entry.Target;
            EditorGUIUtility.PingObject(entry.Target);
        }

        private void HandleDragAndDropPinning()
        {
            Event currentEvent = Event.current;
            Rect dropArea = new Rect(0, 0, position.width, position.height);
            if (!dropArea.Contains(currentEvent.mousePosition)) return;

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences.Length > 0 ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj) _store.Pin(obj);
                }
                currentEvent.Use();
            }
        }

        // Right-click context menu
        private void ShowContextMenu(SelectionHistoryEntry entry)
        {
            GenericMenu menu = new GenericMenu();
            bool valid = entry.Target;

            if (valid)
            {
                menu.AddItem(new GUIContent("Select"), false, () => SelectObject(entry));
                menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(entry.Target));

                if (entry.IsAsset)
                {
                    if (!string.IsNullOrEmpty(entry.CachedPath) && (File.Exists(entry.CachedPath) || Directory.Exists(entry.CachedPath)))
                        menu.AddItem(new GUIContent("Open Containing Folder"), false, () => EditorUtility.RevealInFinder(entry.CachedPath));

                    if (!entry.IsFolder) menu.AddItem(new GUIContent("Open"), false, () => AssetDatabase.OpenAsset(entry.Target));
                }
                else
                {
                    // Is game object
                    //menu.AddItem(new GUIContent("Delete GameObject..."), false, () => DeleteGameObject(entry));
                }
                menu.AddSeparator(string.Empty);

                if (!string.IsNullOrEmpty(entry.AssetGuid))
                    menu.AddItem(new GUIContent("Copy GUID"), false, () => EditorGUIUtility.systemCopyBuffer = entry.AssetGuid);

                if (!string.IsNullOrEmpty(entry.CachedPath))
                    menu.AddItem(new GUIContent("Copy Path"), false, () => EditorGUIUtility.systemCopyBuffer = entry.CachedPath);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Select (Object Deleted)"));
            }

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent(entry.IsPinned ? "Remove from Pinned" : "Add to Pinned"), entry.IsPinned, () => _store.TogglePin(entry));
            menu.AddItem(new GUIContent("Remove from History"), false, () => _store.Remove(entry));

            menu.ShowAsContext();
        }
    }
}
