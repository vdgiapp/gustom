using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gustom.Editor
{
    internal sealed class SelectionHistoryEntry
    {
        public Object Target;
        public bool IsPinned;
        public string AssetGuid;
        public long IdentityRaw;
        public string CachedPath;
        public DateTime Timestamp;

        public bool IsAsset => !string.IsNullOrEmpty(AssetGuid);

        public static SelectionHistoryEntry FromObject(Object target, bool pinned = false)
        {
            return new SelectionHistoryEntry
            {
                Target = target,
                IsPinned = pinned,
                AssetGuid = TryGetAssetGuid(target),
                IdentityRaw = ObjectIdentity.Of(target).RawValue,
                CachedPath = GetPath(target),
                Timestamp = DateTime.UtcNow
            };
        }

        public static SelectionHistoryEntry FromGuid(string guid, bool pinned)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object target = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
            return new SelectionHistoryEntry
            {
                Target = target,
                IsPinned = pinned,
                AssetGuid = guid,
                IdentityRaw = ObjectIdentity.Of(target).RawValue,
                CachedPath = path,
                Timestamp = DateTime.UtcNow
            };
        }

        public bool Refresh()
        {
            if (IsAsset)
            {
                string path = AssetDatabase.GUIDToAssetPath(AssetGuid);
                if (string.IsNullOrEmpty(path)) { Target = null; return false; }
                if (!Target) Target = AssetDatabase.LoadAssetAtPath<Object>(path);
                CachedPath = path;
                return Target;
            }

            if (!Target) return false;
            CachedPath = GetPath(Target);
            return true;
        }

        public string DisplayName()
        {
            if (!Target) return "[Deleted Object]";
            string name = string.IsNullOrEmpty(Target.name) ? "[Unnamed Object]" : Target.name;
            return $"{name} ({Target.GetType().Name})";
        }

        public bool IsFolder => IsAsset && Target is DefaultAsset && AssetDatabase.IsValidFolder(CachedPath);

        private static string TryGetAssetGuid(Object obj)
        {
            if (!obj) return null;
            string path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && AssetDatabase.Contains(obj) ? AssetDatabase.AssetPathToGUID(path) : null;
        }

        private static string GetPath(Object obj)
        {
            if (!obj) return "N/A";
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath)) return assetPath;

            Transform t = obj switch
            {
                GameObject go => go.transform,
                Component c => c.transform,
                _ => null
            };
            if (!t) return $"Scene: {obj.name}";

            string path = t.name;
            for (Transform p = t.parent; p; p = p.parent) path = p.name + "/" + path;
            string scene = t.gameObject.scene.name;
            return $"[{(string.IsNullOrEmpty(scene) ? "Unsaved Scene" : scene)}]/{path}";
        }
    }
}