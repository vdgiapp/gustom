#if UNITY_6000_3_OR_NEWER

#else
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gustom.Editor
{
    [InitializeOnLoad]
    public static class ToolbarCallback
    {
        private static readonly Type ToolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");

        public static Action OnToolbarGUILeft;

        private static ScriptableObject _currentToolbar;
        private static VisualElement _toolbarRoot;
        private static VisualElement _leftContainer;
        private static VisualElement _wrapper;

        static ToolbarCallback()
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.update -= OnUpdate;
                EditorApplication.update += OnUpdate;
            };
        }

        private static void OnUpdate()
        {
            if (ToolbarType == null) return;
            
            if (!_currentToolbar)
            {
                UnityEngine.Object[] toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
                if (toolbars == null || toolbars.Length == 0) return;

                _currentToolbar = toolbars[0] as ScriptableObject;
                if (!_currentToolbar) return;

                _toolbarRoot = GetToolbarRoot(_currentToolbar);
                if (_toolbarRoot == null)
                {
                    _currentToolbar = null;
                    return;
                }
            }

            if (_leftContainer == null)
            {
                _leftContainer = _toolbarRoot.Q("ToolbarZoneLeftAlign") ?? _toolbarRoot.Q(className: "unity-editor-toolbar__left-zone");
            }

            if (_leftContainer == null) return;
            
            if (_leftContainer.Q("CustomToolbarElements") != null)
            {
                EditorApplication.update -= OnUpdate;
                return;
            }

            CreateToolbarGUI();
            EditorApplication.update -= OnUpdate;
        }

        private static VisualElement GetToolbarRoot(ScriptableObject toolbar)
        {
            FieldInfo rootField = ToolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rootField != null)
            {
                if (rootField.GetValue(toolbar) is VisualElement root) return root;
            }

            PropertyInfo visualTreeProperty = ToolbarType.GetProperty("visualTree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (visualTreeProperty != null)
            {
                if (visualTreeProperty.GetValue(toolbar, null) is VisualElement root) return root;
            }
            return null;
        }

        private static void CreateToolbarGUI()
        {
            _wrapper = new VisualElement
            {
                name = "CustomToolbarElements",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    height = 20,
                    flexShrink = 0,
                    marginLeft = 6,
                    marginRight = 2
                }
            };

            IMGUIContainer imguiContainer = new IMGUIContainer(InvokeLeftGUI);
            imguiContainer.style.height = 20;
            imguiContainer.style.flexShrink = 0;

            _wrapper.Add(imguiContainer);
            _leftContainer.Add(_wrapper);
        }

        private static void InvokeLeftGUI()
        {
            GUILayout.BeginHorizontal();
            try
            {
                OnToolbarGUILeft?.Invoke();
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }
}
#endif
