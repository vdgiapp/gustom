#if UNITY_EDITOR
using System.IO;
using Gustom.References;
using UnityEditor;
using UnityEngine;

namespace Gustom.Editor
{
    [CustomPropertyDrawer(typeof(ResourceFolderReference))]
    public class ResourcesFolderReferenceDrawer : PropertyDrawer
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
                if (selectedObject != null)
                {
                    if (ResourceFolderReference.IsAssetInResourcesFolder(selectedObject))
                    {
                        folderAssetProperty.objectReferenceValue = selectedObject;
                    }
                    else
                    {
                        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
                        if (!Directory.Exists(assetPath))
                        {
                            Debug.LogWarning($"Assignment failed: '{selectedObject.name}' is a file, not a folder.");
                        }
                        else
                        {
                            Debug.LogError($"Assignment failed: Folder '{selectedObject.name}' must be inside a 'Resources' folder!");
                        }
                        
                        folderAssetProperty.objectReferenceValue = null;
                        property.FindPropertyRelative("_path").stringValue = string.Empty;
                    }
                }
                else
                {
                    folderAssetProperty.objectReferenceValue = null;
                    property.FindPropertyRelative("_path").stringValue = string.Empty;
                }
            }
        }
    }
}
#endif