using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Unity.PlatformToolkit.Editor
{
    internal class PlatformToolkitBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        private static readonly string s_PtMarkerFilePath = Path.Combine(Application.streamingAssetsPath, "__UnityPlatformToolkit");
        private IPlatformToolkitBuilder m_Builder;
        private Object m_RuntimeConfiguration;

        public void OnPreprocessBuild(BuildReport report)
        {
            string declarationKey = null;

#if UNITY_6000_4_OR_NEWER
            var buildProfile = BuildProfile.GetActiveBuildProfile();
            if (buildProfile != null)
            {
                var buildProfileSettings = buildProfile.GetComponent<BuildProfileSettings>();
                if (buildProfileSettings != null)
                {
                     // Ensure default is set if key is uninitialized
                    buildProfileSettings.AssignKeyIfEmpty(buildProfile);

                    declarationKey = buildProfileSettings.GetImplementationKey(buildProfile, out bool isKeyValidChoice);
                    if (!isKeyValidChoice)
                    {
                        throw new Exception($"No valid implementation found for key '{declarationKey}' on {nameof(BuildProfileSettings)} component");
                    }

                    if (string.IsNullOrEmpty(declarationKey))
                    {
                        throw new Exception($"An empty key was returned by a {nameof(BuildProfileSettings)} component but this is not a valid choice");
                    }
                }
            }
#endif

            // If no declaration key was found from a build profile, try to get it from the global settings
            if (string.IsNullOrEmpty(declarationKey))
            {
                if (!PlatformToolkitSettings.instance.SupportDeclarationTargetsManager.TryGetDeclarationForBuildTarget(report.summary.platform, out declarationKey))
                {
                    Debug.LogWarning($"No PT implementation configured for build target {report.summary.platform}");
                    return;
                }
            }

            Assert.IsTrue(SupportDeclarationManager.TryGetSupportDeclaration(declarationKey, out var supportDeclaration));

            IAchievementConfigurationContext achievementContext = null;
            if (supportDeclaration.AchievementsSupported)
                achievementContext = new AchievementConfigurationContext(supportDeclaration.Key, PlatformToolkitSettings.instance.StoredAchievements);

            ISettingsConfigurationContext settingsContext = null;
            if (supportDeclaration.SettingsProvider != null)
                settingsContext = new SettingsConfigurationContext(supportDeclaration.Key, PlatformToolkitSettings.instance.StoredSettings);

            m_Builder = supportDeclaration.CreateBuilder(achievementContext, settingsContext);
            if (m_Builder == null)
                return;

            m_RuntimeConfiguration = m_Builder.PrepareBuild(report);

            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                try
                {
                    Directory.CreateDirectory(Application.streamingAssetsPath);
                }
                catch
                {
                    // ignored
                }
            }

            if (!File.Exists(s_PtMarkerFilePath))
            {
                using var ptMarkerFileStream = File.Create(s_PtMarkerFilePath);
                ptMarkerFileStream.Write(new UTF8Encoding(true).GetBytes("1"));
            }

            AssetDatabase.CreateAsset(m_RuntimeConfiguration, "Assets/_PT_BuildBundle.asset");
            AddPreloadedAsset(m_RuntimeConfiguration);

            AssemblyReloadEvents.beforeAssemblyReload += CleanUpProjectSettings;
            CleanUpProjectSettingsAfterBuildEnds();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (m_Builder != null)
            {
                m_Builder.PostBuild(report);
            }
        }

        private async void CleanUpProjectSettingsAfterBuildEnds()
        {
            while (BuildPipeline.isBuildingPlayer)
            {
                await Task.Delay(100);
            }
            CleanUpProjectSettings();
        }

        private void CleanUpProjectSettings()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CleanUpProjectSettings;

            RemovePreloadedAsset(m_RuntimeConfiguration);
            File.Delete(s_PtMarkerFilePath);
            File.Delete($"{s_PtMarkerFilePath}.meta");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(m_RuntimeConfiguration));
            AssetDatabase.SaveAssets();
        }

        private void AddPreloadedAsset(Object asset)
        {
            if (asset == null)
                return;

            var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
            if (!preloadedAssets.Contains(asset))
            {
                preloadedAssets.Add(asset);
                PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            }
        }

        private void RemovePreloadedAsset(Object asset)
        {
            if (asset == null)
                return;

            var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
            if (preloadedAssets.Remove(asset))
            {
                PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            }
        }
    }
}
