using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

#if UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars;
#endif

namespace Gustom.Editor
{
    [InitializeOnLoad]
    public static class SceneSelectorToolbar
    {
#if UNITY_6000_3_OR_NEWER
        private const string ToolbarElementPath = "Gustom/Scene Selector";

        static SceneSelectorToolbar()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        [MainToolbarElement(ToolbarElementPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateToolbarElement()
        {
            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            string displayName = string.IsNullOrEmpty(currentScenePath) ? "No Scene Opened" : Path.GetFileNameWithoutExtension(currentScenePath);
            MainToolbarContent content = new MainToolbarContent(displayName, "Select a scene");
            return new MainToolbarDropdown(content, SceneSelectorEditor.ShowAt);
        }

        private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            MainToolbar.Refresh(ToolbarElementPath);
        }
#else
        static SceneSelectorToolbar()
        {
            ToolbarCallback.OnToolbarGUILeft -= OnToolbarGUI;
            ToolbarCallback.OnToolbarGUILeft += OnToolbarGUI;
        }

        private static void OnToolbarGUI()
        {
            string currentScenePath = SceneManager.GetActiveScene().path;
            string displayName = string.IsNullOrEmpty(currentScenePath) ? "No Scene Opened" : Path.GetFileNameWithoutExtension(currentScenePath);

            GUIStyle dropdownStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedWidth = 140f,
                fixedHeight = 20f,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 15, 0, 0)
            };

            Rect buttonRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(140f), GUILayout.Height(20f));
            if (GUI.Button(buttonRect, new GUIContent(displayName), dropdownStyle))
            {
                SceneSelectorEditor.ShowAt(buttonRect);
            }

            if (Event.current.type == EventType.Repaint)
            {
                Rect arrowRect = new Rect(buttonRect.xMax - 16f, buttonRect.y, 16f, buttonRect.height);
                EditorStyles.toolbarDropDown.Draw(arrowRect, GUIContent.none, false, false, false, false);
            }
        }
#endif
    }
}