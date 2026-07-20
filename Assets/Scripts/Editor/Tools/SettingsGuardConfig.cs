using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NPCSystem.Editor.Tools
{
    /// <summary>
    /// Configuration asset for SettingsGuard.
    /// Defines expected values the verifier checks against.
    /// Create via Assets/Create/SettingsGuard Config or Tools > SettingsGuard > Create Config.
    /// </summary>
    [CreateAssetMenu(menuName = "NPCSystem/SettingsGuard Config", fileName = "SettingsGuardConfig.asset")]
    public class SettingsGuardConfig : ScriptableObject
    {
        [Header("Api Compatibility Level")]
        public ApiCompatibilityLevel requiredApiCompatibilityLevel = ApiCompatibilityLevel.NET_Standard;

        [Tooltip("Per-platform overrides. Key = NamedBuildTarget name (e.g. WebGL, Standalone, Server)")]
        public List<PlatformCompatOverride> platformOverrides = new List<PlatformCompatOverride>();

        [Header("Editor Assemblies")]
        public EditorAssembliesCompatibilityLevel requiredEditorAssembliesLevel =
            EditorAssembliesCompatibilityLevel.NET_Standard;

        [Header("Scripting Defines")]
        public List<PlatformDefines> scriptingDefines = new List<PlatformDefines>();

        [Header("Build Scenes")]
        public List<SceneAsset> requiredScenes = new List<SceneAsset>();

        [Header("Build Profiles")]
        public bool verifyBuildProfileSettings = true;

        [Header("Auto-Fix")]
        public bool autoFixOnVerify = false;
    }

    [System.Serializable]
    public class PlatformCompatOverride
    {
        public string platformName = "WebGL"; // Must match NamedBuildTarget field name
        public ApiCompatibilityLevel requiredLevel = ApiCompatibilityLevel.NET_Standard;
    }

    [System.Serializable]
    public class PlatformDefines
    {
        public string platformName = "WebGL";
        public string defines = "SENTIS_ANALYTICS_ENABLED";
    }
}
