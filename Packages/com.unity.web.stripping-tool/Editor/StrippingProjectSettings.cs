using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Holds the submodule stripping settings for the project.
    /// Creates a default submodule stripping settings asset for the project.
    /// </summary>
    public static class StrippingProjectSettings
    {
        /// <summary>
        /// Raised when active settings change.
        /// </summary>
        public static event Action<SubmoduleStrippingSettings> SettingsChanged;

        /// <summary>
        /// The active submodule stripping settings for the project.
        /// </summary>
        public static SubmoduleStrippingSettings ActiveSettings
        {
            get => s_ActiveSettings;
            set
            {
                if (value != s_ActiveSettings)
                {
                    s_ActiveSettings = value;
                    SaveSettings();
                    SettingsChanged?.Invoke(s_ActiveSettings);
                }
            }
        }
        static SubmoduleStrippingSettings s_ActiveSettings;

        /// <summary>
        /// Enables a submodule stripping pass after a build has completed using the currently active settings, if they're set.
        /// </summary>
        public static bool StripAutomaticallyAfterBuild
        {
            get => s_StripAutomaticallyAfterBuild;
            set
            {
                s_StripAutomaticallyAfterBuild = value;
                SaveSettings();
            }
        }
        static bool s_StripAutomaticallyAfterBuild = false;

        internal const string k_DefaultSettingsPath = "Assets/DefaultSubmoduleStrippingSettings.asset";

        // allow changing the path for testing purposes
        internal static string DefaultSettingsPath { get; set; } = k_DefaultSettingsPath;
        internal static readonly string k_AssetPathKey =
            $"{nameof(StrippingProjectSettings)}.{nameof(ActiveSettings)}.AssetPath";
        internal static readonly string k_StripAutomaticallyAfterBuildKey =
            $"{nameof(StrippingProjectSettings)}.{nameof(StripAutomaticallyAfterBuild)}";

        // It seems codecov analysis cannot handle InitializeOnLoadMethod.
        // Make sure the functionality added in this method is covered by other ways.
        [ExcludeFromCodeCoverage]
        [InitializeOnLoadMethod]
        internal static void Initialize()
        {
            AssemblyReloadEvents.beforeAssemblyReload += SaveSettings;
            LoadSettings();
        }

        internal static SubmoduleStrippingSettings[] FindSettings()
        {
            return AssetDatabase
                .FindAssets($"t:{nameof(SubmoduleStrippingSettings)}")
                .Select(path =>
                    AssetDatabase.LoadAssetAtPath<SubmoduleStrippingSettings>(AssetDatabase.GUIDToAssetPath(path))
                )
                .ToArray();
        }

        const string k_SetAsActiveMenuItem =
            "Assets/" + SubmoduleStrippingSettings.RootMenuName + "/Set as Active Submodule Stripping Settings";

        [MenuItem(k_SetAsActiveMenuItem, true)]
        internal static bool SetSelectedSettingsAsActiveSettings_Validate() =>
            Selection.activeObject is SubmoduleStrippingSettings;

        [MenuItem(k_SetAsActiveMenuItem)]
        internal static void SetSelectedSettingsAsActiveSettings()
        {
            if (Selection.activeObject is SubmoduleStrippingSettings settings)
            {
                ActiveSettings = settings;
                Debug.Log($"{ActiveSettings.name} set as active submodule stripping settings.");
            }
        }

        [MenuItem("Assets/" + SubmoduleStrippingSettings.RootMenuName + "/Reveal Active Submodule Stripping Settings")]
        internal static void ShowActiveSettings()
        {
            if (ActiveSettings != null)
            {
                Selection.activeObject = ActiveSettings;
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            else
            {
                Debug.Log("No active settings. Create a settings asset and set is as active.");
            }
        }

        internal static void LoadSettings()
        {
            // Set backing fields directly to avoid the property setter triggering SaveSettings().
            s_StripAutomaticallyAfterBuild = PackageSettings.GetProjectSetting(
                k_StripAutomaticallyAfterBuildKey,
                s_StripAutomaticallyAfterBuild
            );

            // the rest is ActiveSettings handling
            var assetPath = PackageSettings.GetProjectSetting(k_AssetPathKey, string.Empty);
            if (!AssetDatabase.AssetPathExists(assetPath))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<SubmoduleStrippingSettings>(assetPath);
            if (asset == null)
                return;

            s_ActiveSettings = asset;
        }

        static void SaveSettings()
        {
            PackageSettings.SetProjectSetting(k_AssetPathKey, AssetDatabase.GetAssetPath(ActiveSettings));
            PackageSettings.SetProjectSetting(k_StripAutomaticallyAfterBuildKey, StripAutomaticallyAfterBuild);
            PackageSettings.Save();
        }

        /// <summary>
        /// Creates default settings if it appears we're starting the project for the first time and there are no user-created settings
        /// </summary>
        class DefaultSettingsCreator : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths,
                bool didDomainReload
            )
            {
                if (deletedAssets.Contains(DefaultSettingsPath) || FindSettings().Length > 0)
                    return;

                // PVP-300-4 requires an empty console after a package has been added to a project
                //Debug.Log($"Creating default submdule stripping settings at '{DefaultSettingsPath}' and setting them as active.");
                ActiveSettings = SubmoduleStrippingSettings.Create(DefaultSettingsPath);
            }
        }
    }
}
