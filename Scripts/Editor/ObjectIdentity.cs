using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gustom.Editor
{
    internal readonly struct ObjectIdentity
    {
        public readonly long RawValue;
        private ObjectIdentity(long rawValue) => RawValue = rawValue;
        public bool IsValid => RawValue != 0;

        public static ObjectIdentity Of(Object obj)
        {
            if (!obj) return default;
#if UNITY_6000_4_OR_NEWER
            return new ObjectIdentity((long)EntityId.ToULong(obj.GetEntityId()));
#else
            return new ObjectIdentity(obj.GetInstanceID());
#endif
        }

        public static Object Resolve(long rawValue)
        {
            if (rawValue == 0) return null;
#if UNITY_6000_4_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)rawValue));
#else
            return EditorUtility.InstanceIDToObject((int)rawValue);
#endif
        }

        public override int GetHashCode() => RawValue.GetHashCode();
        public override bool Equals(object obj) => obj is ObjectIdentity other && other.RawValue == RawValue;
        public static bool operator ==(ObjectIdentity a, ObjectIdentity b) => a.RawValue == b.RawValue;
        public static bool operator !=(ObjectIdentity a, ObjectIdentity b) => a.RawValue != b.RawValue;
    }
}