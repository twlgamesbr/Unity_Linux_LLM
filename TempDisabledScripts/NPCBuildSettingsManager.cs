using UnityEditor.SettingsManagement;

namespace NPCSystem.Editor
{
    static class NPCBuildSettingsManager
    {
        internal const string k_PackageName = "com.npcsystem.build";

        static Settings s_Instance;

        internal static Settings instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new Settings(k_PackageName);
                return s_Instance;
            }
        }

        public static void Save() => instance.Save();

        public static T Get<T>(string key, SettingsScope scope = SettingsScope.Project, T fallback = default) =>
            instance.Get(key, scope, fallback);

        public static void Set<T>(string key, T value, SettingsScope scope = SettingsScope.Project) =>
            instance.Set(key, value, scope);

        public static bool ContainsKey<T>(string key, SettingsScope scope = SettingsScope.Project) =>
            instance.ContainsKey<T>(key, scope);
    }
}
