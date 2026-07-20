using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A build post processor that performs submodule stripping on a Web build.
    /// Uses StrippingProjectSettings.ActiveSettings as its settings.
    /// </summary>
    class SubmoduleStrippingBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 1; // WebBuildNotifier has 0

        /// <summary>
        /// The file name used for player settings backup file, if we modify Player Settings.
        /// </summary>
        public const string OriginalPlayerSettingFileName = "original_player_settings.json";

        bool SkipBuild { get; set; }

        WebPlayerSettings m_OriginalPlayerSettings;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Reset settings
            SkipBuild = false;
            m_OriginalPlayerSettings = null;

            if (StrippingProjectSettings.ActiveSettings == null)
            {
                SkipBuild = true;
                return;
            }

            if (report.summary.platform != BuildTarget.WebGL)
            {
                Debug.Log(
                    "Submodule stripping options set, but the feature is supported only on the Web Platform, skipping processing of the build."
                );
                SkipBuild = true;
                return;
            }

            if (!StrippingProjectSettings.StripAutomaticallyAfterBuild)
            {
                SkipBuild = true;
                return;
            }

            if (StrippingProjectSettings.ActiveSettings.SubmodulesToStrip.Count < 1)
            {
                Debug.Log("No submodules specified to be stripped, skipping processing of the build.");
                SkipBuild = true;
                return;
            }

            // Backup web player build settings if we do changes. Store them also next to the build.
            // NOTE: Make sure to restore these in erroneous situtations.
            m_OriginalPlayerSettings = WebPlayerSettings.FromPlayerSettings();

            bool settingsModified = false;

            // Debug symbols are required for stripping submodules, user's preference is restored after the build.
            if (PlayerSettings.WebGL.debugSymbolMode == WebGLDebugSymbolMode.Off)
            {
                PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Embedded;
                settingsModified = true;
            }

            if (
                PlayerSettingsHelper.IsSubmoduleStrippingCompatibilityAvailable
                && !PlayerSettingsHelper.EnableSubmoduleStrippingCompatibility
            )
            {
                Debug.LogWarning(
                    "Submodule stripping compatibility wasn't enabled in the Player settings. Enabling submodule stripping compatibility."
                );
                PlayerSettingsHelper.EnableSubmoduleStrippingCompatibility = true;
                settingsModified = true;
            }

            if (settingsModified)
            {
                // Store original settings, only if we modifed them, to the build folder instead of back-up folder as
                // we don't know the GUID of the build yet. WebBuildReport will take care of moving them to back-up folder.
                // WebBuildNotifier will store the effective Player Settings always directly to the back-up folder.
                var buildDirectory = Path.Combine(report.summary.outputPath, "Build");
                if (!Directory.Exists(buildDirectory))
                    Directory.CreateDirectory(buildDirectory);

                File.WriteAllText(
                    Path.Combine(buildDirectory, OriginalPlayerSettingFileName),
                    JsonConvert.SerializeObject(m_OriginalPlayerSettings)
                );
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (
                SkipBuild
                || StrippingProjectSettings.ActiveSettings == null
                || !StrippingProjectSettings.StripAutomaticallyAfterBuild
            )
                return;

            var webBuild = WebBuildReportList.Instance.GetBuild(report.summary.outputPath);
            if (webBuild == null)
            {
                Debug.LogError(
                    $"Did not find WebBuildReport for '{report.summary.outputPath}', cannot strip automatically."
                );
                m_OriginalPlayerSettings.WriteToPlayerSettings();
                return;
            }

            var success = WebBuildProcessor.StripBuild(webBuild, StrippingProjectSettings.ActiveSettings);

            m_OriginalPlayerSettings.WriteToPlayerSettings();
            // If we failed, throw to stop the current build.
            if (!success)
                throw new BuildFailedException("Submodule stripping failed.");
        }
    }
}
