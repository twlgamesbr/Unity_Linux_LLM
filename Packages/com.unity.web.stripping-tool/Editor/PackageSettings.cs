using UnityEditor.SettingsManagement;

namespace Unity.Web.Stripping.Editor
{
    // The settings of the package are stored in two locations:
    // 1. ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json, these should be committed to version control
    // 2. UserSettings/Packages/com.unity.web.stripping-tool/Settings.json, should not be committed to version control
    static class PackageSettings
    {
        static readonly Settings s_Settings = new(
            new ISettingsRepository[]
            {
                new PackageSettingsRepository(PackageConstants.PackageName, "Settings"),
                new ProjectUserSettings(PackageConstants.PackageName, "Settings"),
            }
        );

        public static T GetProjectSetting<T>(string key, T fallbackValue) =>
            s_Settings.Get<T, PackageSettingsRepository>(key, fallbackValue);

        public static void SetProjectSetting<T>(string key, T value) =>
            s_Settings.Set<T, PackageSettingsRepository>(key, value);

        public static void DeleteProjectSetting<T>(string key) =>
            s_Settings.DeleteKey<T, PackageSettingsRepository>(key);

        public static T GetUserSetting<T>(string key, T fallbackValue) =>
            s_Settings.Get<T, ProjectUserSettings>(key, fallbackValue);

        public static void SetUserSetting<T>(string key, T value) => s_Settings.Set<T, ProjectUserSettings>(key, value);

        public static void Save()
        {
            s_Settings.Save();
        }
    }
}
