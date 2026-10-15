using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gustom.Editor
{
    internal sealed class SelectionHistoryStore
    {
        private const string PinnedKey = "Gustom.SelectionHistory.Pinned";
        private const string HistoryKey = "Gustom.SelectionHistory.History";
        private const string MaxSizeKey = "Gustom.SelectionHistory.MaxSize";

        public readonly List<SelectionHistoryEntry> Entries = new();
        public int MaxHistorySize = 30;

        public event Action Changed;
        public static event Action<string> ExternalAssetRemovalRequested;

        public static void RemoveAssetFromHistory(string assetGuid)
        {
            if (string.IsNullOrEmpty(assetGuid)) return;

            if (ExternalAssetRemovalRequested != null)
            {
                ExternalAssetRemovalRequested.Invoke(assetGuid);
                return;
            }

            SelectionHistoryStore store = new();
            store.Load();
            SelectionHistoryEntry entry = store.Entries.FirstOrDefault(item => item.AssetGuid == assetGuid);
            if (entry == null) return;

            store.Remove(entry);
        }

        public void Load()
        {
            MaxHistorySize = EditorPrefs.GetInt(MaxSizeKey, 30);
            Entries.Clear();

            HashSet<string> pinnedGuids = new();
            foreach (string guid in SplitGuids(EditorPrefs.GetString(PinnedKey, "")))
            {
                Entries.Add(SelectionHistoryEntry.FromGuid(guid, true));
                pinnedGuids.Add(guid);
            }

            int loaded = 0;
            foreach (string guid in SplitGuids(EditorPrefs.GetString(HistoryKey, "")))
            {
                if (loaded >= MaxHistorySize) break;
                if (pinnedGuids.Contains(guid)) continue;

                SelectionHistoryEntry entry = SelectionHistoryEntry.FromGuid(guid, false);
                if (entry.Target) { Entries.Add(entry); loaded++; }
            }
        }

        public void Save()
        {
            EditorPrefs.SetInt(MaxSizeKey, MaxHistorySize);

            string pinned = string.Join("|", Entries.Where(e => e.IsPinned && e.IsAsset).Select(e => e.AssetGuid));
            EditorPrefs.SetString(PinnedKey, pinned);

            string history = string.Join("|", Entries
                .Where(e => !e.IsPinned && e.IsAsset)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => e.AssetGuid)
                .Distinct()
                .Take(MaxHistorySize));
            EditorPrefs.SetString(HistoryKey, history);
        }

        public void Add(Object target)
        {
            if (!target) return;
            if (target is AssetImporter || target is EditorWindow || target is UnityEditor.Editor) return;

            long identity = ObjectIdentity.Of(target).RawValue;
            string guid = AssetDatabase.Contains(target) ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)) : null;

            SelectionHistoryEntry existing = Entries.FirstOrDefault(e =>
                e.IsAsset == !string.IsNullOrEmpty(guid) &&
                (e.IsAsset ? e.AssetGuid == guid : e.IdentityRaw == identity));

            if (existing != null)
            {
                Entries.Remove(existing);
                existing.Timestamp = DateTime.UtcNow;
                Insert(existing);
            }
            else
            {
                Insert(SelectionHistoryEntry.FromObject(target));
            }

            PruneInvalid();
            TrimOverflow();
            Save();
            Changed?.Invoke();
        }

        public void Pin(Object target)
        {
            if (!target) return;
            long identity = ObjectIdentity.Of(target).RawValue;
            string guid = AssetDatabase.Contains(target) ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)) : null;

            SelectionHistoryEntry existing = Entries.FirstOrDefault(e =>
                e.IsAsset == !string.IsNullOrEmpty(guid) &&
                (e.IsAsset ? e.AssetGuid == guid : e.IdentityRaw == identity));

            if (existing != null)
            {
                if (existing.IsPinned) return;
                Entries.Remove(existing);
                existing.IsPinned = true;
                existing.Timestamp = DateTime.UtcNow;
                Entries.Insert(0, existing);
            }
            else
            {
                Entries.Insert(0, SelectionHistoryEntry.FromObject(target, true));
            }

            Save();
            Changed?.Invoke();
        }

        public void TogglePin(SelectionHistoryEntry entry)
        {
            Entries.Remove(entry);
            entry.IsPinned = !entry.IsPinned;
            if (!entry.IsPinned) entry.Timestamp = DateTime.UtcNow;
            Insert(entry);
            if (!entry.IsPinned) TrimOverflow();
            Save();
            Changed?.Invoke();
        }

        public void Remove(SelectionHistoryEntry entry)
        {
            Entries.Remove(entry);
            Save();
            Changed?.Invoke();
        }

        public void Reorder(SelectionHistoryEntry moved, SelectionHistoryEntry before)
        {
            Entries.Remove(moved);
            int index = before != null ? Entries.IndexOf(before) : Entries.FindIndex(e => !e.IsPinned);
            Entries.Insert(index < 0 ? Entries.Count : index, moved);
            Save();
            Changed?.Invoke();
        }

        public void Clear()
        {
            Entries.RemoveAll(e => !e.IsPinned);
            Save();
            Changed?.Invoke();
        }

        public void RefreshValidity()
        {
            bool changed = false;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                SelectionHistoryEntry e = Entries[i];
                bool valid = e.Refresh();
                if (!valid && !e.IsPinned) { Entries.RemoveAt(i); changed = true; }
            }
            if (changed) Changed?.Invoke();
        }

        private void Insert(SelectionHistoryEntry entry)
        {
            int index = entry.IsPinned ? 0 : Entries.TakeWhile(e => e.IsPinned).Count();
            Entries.Insert(index, entry);
        }

        private void PruneInvalid() => Entries.RemoveAll(e => !e.IsPinned && !e.Refresh());

        private void TrimOverflow()
        {
            List<SelectionHistoryEntry> overflow = Entries
                .Where(e => !e.IsPinned)
                .OrderBy(e => e.Timestamp)
                .Take(Math.Max(0, Entries.Count(e => !e.IsPinned) - MaxHistorySize))
                .ToList();
            foreach (SelectionHistoryEntry e in overflow) Entries.Remove(e);
        }

        private static IEnumerable<string> SplitGuids(string raw) =>
            string.IsNullOrEmpty(raw) ? Array.Empty<string>() : raw.Split('|');
    }
}
