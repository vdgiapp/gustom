using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gustom.References
{
    /// <summary>
    /// Wrapper for a folder asset inside a Resources directory.
    /// </summary>
    [Serializable]
    public class ResourceFolderReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [SerializeField] private UnityObject _folderObject;

        private bool IsValidFolderObject => _folderObject && IsAssetInResourcesFolder(_folderObject);
#endif

        [SerializeField] private string _path;

        /// <summary>
        /// Relative path to folder inside Resources (e.g., "UI/Icons").
        /// </summary>
        public string Path
        {
            get
            {
#if UNITY_EDITOR
                return GetResourcesFolderPathFromAsset();
#else
                return _path;
#endif
            }
            set
            {
                _path = value;
#if UNITY_EDITOR
                _folderObject = GetFolderAssetFromResourcesPath();
#endif
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (!IsValidFolderObject && !string.IsNullOrEmpty(_path))
            {
                if (!(_folderObject = GetFolderAssetFromResourcesPath())) _path = null;
                return;
            }
            _path = GetResourcesFolderPathFromAsset();
#endif
        }

        public void OnAfterDeserialize()
        {
#if UNITY_EDITOR
            EditorApplication.update += HandleAfterDeserialize;
#endif
        }

#if UNITY_EDITOR
        private void HandleAfterDeserialize()
        {
            EditorApplication.update -= HandleAfterDeserialize;
            if (IsValidFolderObject) return;
            if (string.IsNullOrEmpty(_path)) return;
            if (!(_folderObject = GetFolderAssetFromResourcesPath())) _path = null;
        }

        private DefaultAsset GetFolderAssetFromResourcesPath()
        {
            if (string.IsNullOrEmpty(_path)) return null;

            string fullPath = System.IO.Path.Combine("Assets/Resources", _path);
            var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(fullPath);
            if (asset && Directory.Exists(fullPath)) return asset;

            string[] guids = AssetDatabase.FindAssets("t:DefaultAsset");
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Directory.Exists(assetPath) && assetPath.EndsWith("/Resources/" + _path))
                {
                    return AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
                }
            }
            return null;
        }

        private string GetResourcesFolderPathFromAsset()
        {
            if (!_folderObject) return string.Empty;
            string fullPath = AssetDatabase.GetAssetPath(_folderObject);
            return ConvertToResourcesPath(fullPath);
        }

        public static bool IsAssetInResourcesFolder(UnityObject obj)
        {
            if (obj is not DefaultAsset) return false;
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (!Directory.Exists(assetPath)) return false;

            return assetPath.Contains("/Resources/") || assetPath.EndsWith("/Resources");
        }

        public static string ConvertToResourcesPath(string fullPath)
        {
            const string resourcesSegment = "/Resources/";
            int index = fullPath.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
            
            if (index != -1)
            {
                return fullPath.Substring(index + resourcesSegment.Length);
            }
            
            if (fullPath.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Empty;
        }
#endif

        public static implicit operator string(ResourceFolderReference resourceFolderReference) => resourceFolderReference.Path;
    }
}
