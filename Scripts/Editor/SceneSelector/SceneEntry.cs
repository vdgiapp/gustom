namespace Gustom.Editor
{
    internal sealed class SceneEntry
    {
        public string Guid;
        public string Path;
        public string Name;
        public string Folder;
        public bool IsPinned;
        public bool InBuildSettings;
        public bool Enabled;
        public int BuildOrder;
        public int BuildIndex;
    }
}
