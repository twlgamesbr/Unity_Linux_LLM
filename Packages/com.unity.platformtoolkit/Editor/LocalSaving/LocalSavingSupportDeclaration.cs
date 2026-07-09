using System.Collections.Generic;
using Unity.PlatformToolkit.Editor;
using UnityEditor;

#if UNITY_6000_4_OR_NEWER
using GUID = UnityEngine.GUID;
#endif

namespace Unity.PlatformToolkit.LocalSaving.Editor
{
    internal class LocalSavingSupportDeclaration : IPlatformToolkitSupportDeclaration
    {
        private static readonly BuildTarget[] k_SupportedBuildTargets = new[]
        {
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64
        };

        public string DisplayName => "Local Saving";
        public string Key => "Unity.LocalSaving";
        public int SortIndex => 0;
        public IReadOnlyCollection<BuildTarget> SupportedPlatforms => k_SupportedBuildTargets;

        private static readonly GUID[] k_SupportedBuildProfileGuids = new[]
        {
            new GUID("4e3c793746204150860bf175a9a41a05"), // Windows
            new GUID("0d2129357eac403d8b359c2dcbf82502"), // macOS
        };
        public IReadOnlyCollection<GUID> SupportedBuildProfileGuids => k_SupportedBuildProfileGuids;

        public IPlatformToolkitBuilder CreateBuilder(IAchievementConfigurationContext achievementContext, ISettingsConfigurationContext settingsContext)
        {
            return new LocalSavingBuilder();
        }
    }
}
