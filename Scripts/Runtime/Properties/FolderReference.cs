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
    /// Wrapper for a folder asset.
    /// </summary>
    [Serializable]
    public class FolderReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [SerializeField] private UnityObject _folderObject;
        
        private bool IsValidFolderObject => _folderObject && IsAssetAFolder(_folderObject);
#endif

        [SerializeField] private string _path;

        /// <summary>
        /// Relative path to folder.
        /// </summary>
        public string Path
        {
            get => 
#if UNITY_EDITOR
                GetFolderPathFromAsset();
#else
                _path;
#endif
            set
            {
                _path = value;
#if UNITY_EDITOR
                _folderObject = GetFolderAssetFromPath();
#endif
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (!IsValidFolderObject && !string.IsNullOrEmpty(_path))
            {
                if (!(_folderObject = GetFolderAssetFromPath())) _path = null;
                return;
            }
            _path = GetFolderPathFromAsset();
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
            if (!(_folderObject = GetFolderAssetFromPath())) _path = null;
        }

        private DefaultAsset GetFolderAssetFromPath() => string.IsNullOrEmpty(_path) ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(_path);
        private string GetFolderPathFromAsset() => !_folderObject ? string.Empty : AssetDatabase.GetAssetPath(_folderObject);
        private static bool IsAssetAFolder(UnityObject obj) => obj is DefaultAsset && Directory.Exists(AssetDatabase.GetAssetPath(obj));
#endif

        public static implicit operator string(FolderReference folderReference) => folderReference.Path;
    }
}
