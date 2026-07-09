namespace UnityEngine.Localization
{
    internal static class InstanceIdHelper
    {
        public static int GetInstanceId(Object obj)
        {
            if (obj == null) return 0;

            #if UNITY_6000_5_OR_NEWER
            return (int)(EntityId.ToULong(obj.GetEntityId()) & 0xFFFFFFFFu);
            #else
            return obj.GetInstanceID();
            #endif
        }

        public static string GetInstanceIdString(Object obj)
        {
            if (obj == null) return "0";

            #if UNITY_6000_5_OR_NEWER
            return EntityId.ToULong(obj.GetEntityId()).ToString();
            #else
            return obj.GetInstanceID().ToString();
            #endif
        }
    }
}
