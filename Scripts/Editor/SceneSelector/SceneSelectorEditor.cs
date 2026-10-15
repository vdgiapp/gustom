using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    public static class SceneSelectorEditor
    {
        public const string SearchControlName = "Gustom.SceneSelector.Search";
        public const float RowHeight = 22f;

        public static void ShowAt(Rect activatorRect)
        {
            PopupWindow.Show(activatorRect, new SceneSelectorPopup());
        }

        [MenuItem("Tools/Gustom/Scene Selector Window")]
        private static void OpenFromMenu()
        {
            SceneSelectorWindow.Open();
        }
    }
}
