using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A structure that gathers up all pertinent [Web Build Settings](xref:web-build-settings)
    /// defined in [`UnityEditor.EditorUserBuildSettings`](xref:UnityEditor.EditorUserBuildSettings).
    /// </summary>
    /// <seealso cref="WebPlayerSettings"/>
    /// <seealso cref="WebPlayerSettingsScope"/>
    [Serializable]
    [SuppressMessage("", "IDE1006", Justification = "Original field names used as is for easy maintainability")]
    public class WebBuildSettings
    {
        /// <summary>
        /// Constructs with default-initialized values.
        /// </summary>
        public WebBuildSettings() { }

        /// <summary>
        /// Constructs a new `WebBuildSettings` with values read from current build settings.
        /// </summary>
        /// <returns>`WebPlayerSettings` with the current Player settings.</returns>
        public static WebBuildSettings FromEditorUserBuildSettings()
        {
            var settings = new WebBuildSettings();
            settings.ReadFromEditorUserBuildSettings();
            return settings;
        }

        /// <summary>
        /// Applies the current build settings to this object.
        /// </summary>
        public void ReadFromEditorUserBuildSettings()
        {
            development = EditorUserBuildSettings.development;
            buildWithDeepProfilingSupport = EditorUserBuildSettings.buildWithDeepProfilingSupport;
            connectProfiler = EditorUserBuildSettings.connectProfiler;
            webGLBuildSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            codeOptimization = GetCodeOptimization();
        }

        /// <summary>
        /// Applies the settings to `UnityEditor.EditorUserBuildSettings`.
        /// </summary>
        public void WriteToEditorUserBuildSettings()
        {
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = buildWithDeepProfilingSupport;
            EditorUserBuildSettings.connectProfiler = connectProfiler;
            EditorUserBuildSettings.webGLBuildSubtarget = webGLBuildSubtarget;
            SetCodeOptimization(codeOptimization);
        }

        /// <summary>
        /// Refer to [`EditorUserBuildSettings.development`](xref:UnityEditor.EditorUserBuildSettings.development).
        /// </summary>
        public bool development;

        /// <summary>
        /// Refer to [`EditorUserBuildSettings.buildWithDeepProfilingSupport`](xref:UnityEditor.EditorUserBuildSettings.buildWithDeepProfilingSupport).
        /// </summary>
        public bool buildWithDeepProfilingSupport;

        /// <summary>
        /// Refer to [`EditorUserBuildSettings.connectProfiler`](xref:UnityEditor.EditorUserBuildSettings.connectProfiler).
        /// </summary>
        public bool connectProfiler;

        /// <summary>
        /// Texture compression override.
        /// Refer to [`EditorUserBuildSettings.webGLBuildSubtarget`](xref:UnityEditor.EditorUserBuildSettings.webGLBuildSubtarget).
        /// </summary>
        public WebGLTextureSubtarget webGLBuildSubtarget;

        /// <summary>
        /// Refer to the description of **Code Optimization** in [Web Build Settings](xref:web-build-settings).
        /// The values used here are the `UnityEditor.WebGL.WasmCodeOptimization` values as strings, not the UI strings visible to the user.
        /// </summary>
        public string codeOptimization;

        internal static readonly string k_PlatformName = BuildPipeline.GetBuildTargetName(BuildTarget.WebGL);

        internal static string GetCodeOptimization() =>
            EditorUserBuildSettings.GetPlatformSettings(k_PlatformName, "CodeOptimization");

        internal static void SetCodeOptimization(string value) =>
            EditorUserBuildSettings.SetPlatformSettings(k_PlatformName, "CodeOptimization", value);
    }
}
