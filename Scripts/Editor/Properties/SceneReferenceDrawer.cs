#if UNITY_EDITOR
using Gustom.References;
using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    [CustomPropertyDrawer(typeof(SceneReference))]
    public class SceneReferencePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                GUI.Label(position, "Scene multi-editing not supported");
                return;
            }
            var sceneAssetProperty = property.FindPropertyRelative("_sceneObject");
            EditorGUI.BeginChangeCheck();
            sceneAssetProperty.objectReferenceValue = EditorGUI.ObjectField(position, label, sceneAssetProperty.objectReferenceValue, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck() && GetEditorBuildSettingsScene(sceneAssetProperty.objectReferenceValue) == null)
                property.FindPropertyRelative("_path").stringValue = string.Empty;
        }
        
        private static EditorBuildSettingsScene GetEditorBuildSettingsScene(Object sceneObject)
        {
            if (sceneObject is not SceneAsset) return null;
            var assetPath = AssetDatabase.GetAssetPath(sceneObject);
            var assetGuid = new GUID(AssetDatabase.AssetPathToGUID(assetPath));
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (assetGuid != scene.guid) continue;
                return scene;
            }
            return null;
        }
    }
}
#endif