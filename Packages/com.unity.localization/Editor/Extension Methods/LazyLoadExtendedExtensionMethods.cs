using UnityEngine;

namespace UnityEditor.Localization
{
    static class LazyLoadExtendedExtensionMethods
    {
        public static int GetInstanceId<T>(this LazyLoadReference<T> lazy) where T : Object
        {
            #if UNITY_6000_5_OR_NEWER
            return (int)(EntityId.ToULong(lazy.entityId) & 0xFFFFFFFFu);
            #elif UNITY_6000_3_OR_NEWER
            return (int)lazy.entityId;
            #elif UNITY_2020_1_OR_NEWER
            return lazy.instanceID;
            #else
            var field = lazy.GetType().GetField("m_InstanceID", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (int)field.GetValue(lazy);
            #endif
        }
    }
}
