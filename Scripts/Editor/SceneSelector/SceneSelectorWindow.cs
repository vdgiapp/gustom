using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    [InitializeOnLoad]
    public sealed class SceneSelectorWindow : EditorWindow
    {
        private SceneSelectorPopup selector;
        
        static SceneSelectorWindow()
        {
            
        }
        
        public static void Open()
        {
            SceneSelectorWindow window = GetWindow<SceneSelectorWindow>(false, "Scene Selector", true);
            window.minSize = new Vector2(300f, 240f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            selector = new SceneSelectorPopup();
            selector.InitializeForWindow(this);
        }
        
        private void OnDisable()
        {
            selector?.Unsubscribe();
        }

        private void OnGUI()
        {
            if (selector == null)
            {
                selector = new SceneSelectorPopup();
                selector.InitializeForWindow(this);
            }
            selector.DrawSelectorGUI(new Rect(0f, 0f, position.width, position.height));
        }
    }
}