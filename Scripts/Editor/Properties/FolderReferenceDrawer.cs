#if UNITY_EDITOR
using System.IO;
using Gustom.References;
using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    [CustomPropertyDrawer(typeof(FolderReference))]
    public class FolderReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                GUI.Label(position, "Folder multi-editing not supported");
                return;
            }
            
            var folderAssetProperty = property.FindPropertyRelative("_folderObject");
            EditorGUI.BeginChangeCheck();
            var selectedObject = EditorGUI.ObjectField(position, label, folderAssetProperty.objectReferenceValue, typeof(DefaultAsset), false);
            
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedObject != null && Directory.Exists(AssetDatabase.GetAssetPath(selectedObject)))
                {
                    folderAssetProperty.objectReferenceValue = selectedObject;
                }
                else
                {
                    if (selectedObject != null) Debug.LogWarning($"'{selectedObject.name}' is a file, not a folder.");
                    folderAssetProperty.objectReferenceValue = null;
                    property.FindPropertyRelative("_path").stringValue = string.Empty;
                }
            }
        }
    }
}
#endif