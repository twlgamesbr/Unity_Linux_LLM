using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Content;
using UnityEngine.Rendering;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A structure that gathers up all [Web Player settings](xref:class-PlayerSettingsWebGL),
    /// in particular, those defined in ([`UnityEditor.PlayerSettings.WebGL`](xref:UnityEditor.PlayerSettings.WebGL)),
    /// and [Web Build Settings](xref:web-build-settings).
    /// </summary>
    /// <seealso cref="WebBuildSettings"/>
    /// <seealso cref="WebPlayerSettingsScope"/>
    [Serializable]
    [SuppressMessage("", "IDE1006", Justification = "Original field names used as is for easy maintainability")]
    public class WebPlayerSettings
    {
        // Not using nameof() for these in case they get removed completely from the public API
        internal static readonly string[] KnownObsoleteSettings =
        {
            "wasmArithmeticExceptions",
            "analyzeBuildSize",
            "enableWebGPU" // simply removed in 6.1a without becoming deprecated first
        };

        /// <summary>
        /// Constructs with default-initialized values.
        /// </summary>
        public WebPlayerSettings()
        {
        }

        /// <summary>
        /// Constructs a new `WebPlayerSettings` with values read from current Player settings.
        /// </summary>
        /// <returns>`WebPlayerSettings` with the current Player settings.</returns>
        public static WebPlayerSettings FromPlayerSettings()
        {
            var settings = new WebPlayerSettings();
            settings.ReadFromPlayerSettings();
            return settings;
        }

        /// <summary>
        /// Applies the settings from `UnityEditor.PlayerSettings.WebGL` to this object.
        /// </summary>
        public void ReadFromPlayerSettings()
        {
            closeOnQuit = PlayerSettings.WebGL.closeOnQuit;
            compressionFormat = PlayerSettings.WebGL.compressionFormat;
            dataCaching = PlayerSettings.WebGL.dataCaching;
            debugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
            decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            emscriptenArgs = PlayerSettings.WebGL.emscriptenArgs;
            exceptionSupport = PlayerSettings.WebGL.exceptionSupport;
            geometricMemoryGrowthStep = PlayerSettings.WebGL.geometricMemoryGrowthStep;
            initialMemorySize = PlayerSettings.WebGL.initialMemorySize;
            linearMemoryGrowthStep = PlayerSettings.WebGL.linearMemoryGrowthStep;
            linkerTarget = PlayerSettings.WebGL.linkerTarget;
            maximumMemorySize = PlayerSettings.WebGL.maximumMemorySize;
            memorySize = PlayerSettings.WebGL.memorySize;
            memoryGeometricGrowthCap = PlayerSettings.WebGL.memoryGeometricGrowthCap;
            memoryGrowthMode = PlayerSettings.WebGL.memoryGrowthMode;
            modulesDirectory = PlayerSettings.WebGL.modulesDirectory;
            nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            powerPreference = PlayerSettings.WebGL.powerPreference;
            showDiagnostics = PlayerSettings.WebGL.showDiagnostics;
            template = PlayerSettings.WebGL.template;
            runInBackground = PlayerSettings.runInBackground;
            showSplashScreen = PlayerSettings.SplashScreen.show;
            threadsSupport = PlayerSettings.WebGL.threadsSupport;
            useEmbeddedResources = PlayerSettings.WebGL.useEmbeddedResources;
            // NOTE technically Wasm2023 was introduced already in 2023.3.0a16
#if UNITY_6000_0_OR_NEWER
            wasm2023 = PlayerSettings.WebGL.wasm2023;
#endif
            webAssemblyBigInt = PlayerSettings.WebGL.webAssemblyBigInt;
            webAssemblyTable = PlayerSettings.WebGL.webAssemblyTable;
            enableSubmoduleStrippingCompatibility = PlayerSettingsHelper.EnableSubmoduleStrippingCompatibility;
            managedStrippingLevel = GetManagedStrippingLevel();
            stripEngineCode = PlayerSettings.stripEngineCode;
            scriptingDefineSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            il2CppCodeGeneration = GetIl2CppCodeGeneration();
            il2CppCompilerConfiguration = GetIl2CppCompilerConfiguration();
            autoGraphicsAPI = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL);
            graphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);

            BuildSettings = WebBuildSettings.FromEditorUserBuildSettings();
        }

        /// <summary>
        /// Applies the settings to `UnityEditor.PlayerSettings.WebGL`.
        /// </summary>
        public void WriteToPlayerSettings()
        {
            PlayerSettings.WebGL.closeOnQuit = closeOnQuit;
            PlayerSettings.WebGL.compressionFormat = compressionFormat;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.debugSymbolMode = debugSymbolMode;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
            PlayerSettings.WebGL.emscriptenArgs = emscriptenArgs;
            PlayerSettings.WebGL.exceptionSupport = exceptionSupport;
            PlayerSettings.WebGL.geometricMemoryGrowthStep = geometricMemoryGrowthStep;
            PlayerSettings.WebGL.initialMemorySize = initialMemorySize;
            PlayerSettings.WebGL.linearMemoryGrowthStep = linearMemoryGrowthStep;
            PlayerSettings.WebGL.linkerTarget = linkerTarget;
            PlayerSettings.WebGL.maximumMemorySize = maximumMemorySize;
            PlayerSettings.WebGL.memorySize = memorySize;
            PlayerSettings.WebGL.memoryGeometricGrowthCap = memoryGeometricGrowthCap;
            PlayerSettings.WebGL.memoryGrowthMode = memoryGrowthMode;
            PlayerSettings.WebGL.modulesDirectory = modulesDirectory;
            PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;
            PlayerSettings.WebGL.powerPreference = powerPreference;
            PlayerSettings.WebGL.showDiagnostics = showDiagnostics;
            PlayerSettings.WebGL.template = template;
            if (runInBackground.HasValue)
                PlayerSettings.runInBackground = runInBackground.Value;
            if (showSplashScreen.HasValue)
                PlayerSettings.SplashScreen.show = showSplashScreen.Value;
            PlayerSettings.WebGL.threadsSupport = threadsSupport;
            PlayerSettings.WebGL.useEmbeddedResources = useEmbeddedResources;
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.WebGL.wasm2023 = wasm2023;
#endif
            PlayerSettings.WebGL.webAssemblyBigInt = webAssemblyBigInt;
            PlayerSettings.WebGL.webAssemblyTable = webAssemblyTable;
            PlayerSettingsHelper.EnableSubmoduleStrippingCompatibility = enableSubmoduleStrippingCompatibility;
            SetManagedStrippingLevel(managedStrippingLevel);
            if (stripEngineCode.HasValue)
                PlayerSettings.stripEngineCode = stripEngineCode.Value;
            if (scriptingDefineSymbols != null)
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, scriptingDefineSymbols);
            SetIl2CppCodeGeneration(il2CppCodeGeneration);
            SetIl2CppCompilerConfiguration(il2CppCompilerConfiguration);
            if (autoGraphicsAPI.HasValue)
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, autoGraphicsAPI.Value);
            if (graphicsAPIs != null && graphicsAPIs.Length > 0)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, graphicsAPIs);
            }

            BuildSettings.WriteToEditorUserBuildSettings();

            // Ensure that PlayerSettings applied to ProjectSettings.asset
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.closeOnQuit`](xref:UnityEditor.PlayerSettings.WebGL.closeOnQuit).
        /// </summary>
        public bool closeOnQuit;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.compressionFormat`](xref:UnityEditor.PlayerSettings.WebGL.compressionFormat).
        /// </summary>
        public WebGLCompressionFormat compressionFormat;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.dataCaching`](xref:UnityEditor.PlayerSettings.WebGL.dataCaching).
        /// </summary>
        public bool dataCaching;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.debugSymbolMode`](xref:UnityEditor.PlayerSettings.WebGL.debugSymbolMode).
        /// </summary>
        public WebGLDebugSymbolMode debugSymbolMode;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.decompressionFallback`](xref:UnityEditor.PlayerSettings.WebGL.decompressionFallback).
        /// </summary>
        public bool decompressionFallback;
        /// <summary>
        /// For internal use only, hidden from the documentation.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string emscriptenArgs;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.exceptionSupport`](xref:UnityEditor.PlayerSettings.WebGL.exceptionSupport).
        /// </summary>
        public WebGLExceptionSupport exceptionSupport;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.geometricMemoryGrowthStep`](xref:UnityEditor.PlayerSettings.WebGL.geometricMemoryGrowthStep).
        /// </summary>
        public float geometricMemoryGrowthStep;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.initialMemorySize`](xref:UnityEditor.PlayerSettings.WebGL.initialMemorySize).
        /// </summary>
        public int initialMemorySize;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.linearMemoryGrowthStep`](xref:UnityEditor.PlayerSettings.WebGL.linearMemoryGrowthStep).
        /// </summary>
        public int linearMemoryGrowthStep;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.linkerTarget`](xref:UnityEditor.PlayerSettings.WebGL.linkerTarget).
        /// </summary>
        public WebGLLinkerTarget linkerTarget;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.maximumMemorySize`](xref:UnityEditor.PlayerSettings.WebGL.maximumMemorySize).
        /// </summary>
        public int maximumMemorySize;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.memorySize`](xref:UnityEditor.PlayerSettings.WebGL.memorySize).
        /// </summary>
        public int memorySize;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.memoryGeometricGrowthCap`](xref:UnityEditor.PlayerSettings.WebGL.memoryGeometricGrowthCap).
        /// </summary>
        public int memoryGeometricGrowthCap;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.memoryGrowthMode`](xref:UnityEditor.PlayerSettings.WebGL.memoryGrowthMode).
        /// </summary>
        public WebGLMemoryGrowthMode memoryGrowthMode;
        /// <summary>
        /// For internal use only, hidden from the documentation.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string modulesDirectory;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.nameFilesAsHashes`](xref:UnityEditor.PlayerSettings.WebGL.nameFilesAsHashes).
        /// </summary>
        public bool nameFilesAsHashes;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.powerPreference`](xref:UnityEditor.PlayerSettings.WebGL.powerPreference).
        /// </summary>
        public WebGLPowerPreference powerPreference;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.showDiagnostics`](xref:UnityEditor.PlayerSettings.WebGL.showDiagnostics).
        /// </summary>
        public bool showDiagnostics;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.template`](xref:UnityEditor.PlayerSettings.WebGL.template).
        /// </summary>
        public string template;

        /// <summary>
        /// Refer to [`PlayerSettings.runInBackground`](xref:UnityEditor.PlayerSettings.runInBackground).
        /// </summary>
        public bool? runInBackground;

        /// <summary>
        /// Refer to [`PlayerSettings.SplashScreen.show`](xref:UnityEditor.PlayerSettings.SplashScreen.show).
        /// </summary>
        public bool? showSplashScreen;

        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.threadsSupport`](xref:UnityEditor.PlayerSettings.WebGL.threadsSupport).
        /// </summary>
        public bool threadsSupport;
        /// <summary>
        /// Refer to [Embedded resources in Web](xref:webgl-embeddedresources).
        /// </summary>
        public bool useEmbeddedResources;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.wasm2023`](xref:UnityEditor.PlayerSettings.WebGL.wasm2023).
        /// </summary>
        public bool wasm2023;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.webAssemblyBigInt`](xref:UnityEditor.PlayerSettings.WebGL.webAssemblyBigInt).
        /// </summary>
        public bool webAssemblyBigInt;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.webAssemblyTable`](xref:UnityEditor.PlayerSettings.WebGL.webAssemblyTable).
        /// </summary>
        public bool webAssemblyTable;
        /// <summary>
        /// Refer to [`PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility`](xref:UnityEditor.PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility).
        /// </summary>
        public bool enableSubmoduleStrippingCompatibility;
        /// <summary>
        /// Refer to [`ManagedStrippingLevel`](xref:UnityEditor.ManagedStrippingLevel).
        /// </summary>
        public ManagedStrippingLevel managedStrippingLevel;
        /// <summary>
        /// Refer to [`PlayerSettings.stripEngineCode`](xref:UnityEditor.PlayerSettings.stripEngineCode).
        /// </summary>
        public bool? stripEngineCode;

        /// <summary>
        /// Refer to [`PlayerSettings.GetScriptingDefineSymbols`](xref:UnityEditor.PlayerSettings.GetScriptingDefineSymbols).
        /// </summary>
        public string scriptingDefineSymbols;

        /// <summary>
        /// Refer to [`Il2CppCodeGeneration`](xref:UnityEditor.Build.Il2CppCodeGeneration).
        /// </summary>
        public Il2CppCodeGeneration il2CppCodeGeneration;
        /// <summary>
        /// Refer to [`Il2CppCompilerConfiguration`](xref:UnityEditor.Il2CppCompilerConfiguration).
        /// </summary>
        public Il2CppCompilerConfiguration il2CppCompilerConfiguration;

        /// <summary>
        /// Whether Auto Graphics API is enabled for WebGL.
        /// Refer to [`PlayerSettings.GetUseDefaultGraphicsAPIs`](xref:UnityEditor.PlayerSettings.GetUseDefaultGraphicsAPIs).
        /// </summary>
        public bool? autoGraphicsAPI;

        /// <summary>
        /// The list of Graphics APIs enabled for WebGL.
        /// Refer to [`PlayerSettings.GetGraphicsAPIs`](xref:UnityEditor.PlayerSettings.GetGraphicsAPIs).
        /// </summary>
        public GraphicsDeviceType[] graphicsAPIs;

        /// <summary>
        /// The build settings used together with the Player settings.
        /// </summary>
        public WebBuildSettings BuildSettings;

        // In dev build debug symbols are always embedded
        internal WebGLDebugSymbolMode DebugSymbolMode =>
            BuildSettings?.development == true ? WebGLDebugSymbolMode.Embedded : debugSymbolMode;
        internal bool MultithreadingEnabled => threadsSupport;
        // Native multithreading means that all other wasm features are enabled also even if not explicitly set
        internal bool Wasm2023Enabled => MultithreadingEnabled || wasm2023;
        // Wasm2023 means that all other wasm features are enabled
        internal bool WasmTableEnabled => Wasm2023Enabled || webAssemblyTable;
        internal bool WasmBigIntEnabled => Wasm2023Enabled || webAssemblyBigInt;

        /// <summary>
        /// Check whether Player settings have debug info necessary for submodule
        /// profiling and stripping available.
        /// </summary>
        internal bool HasDebugInfo => debugSymbolMode != WebGLDebugSymbolMode.Off;

        /// <summary>
        /// Check whether Player settings contain additional Emscripten args that
        /// are incompatible with submodule profiling and stripping.
        /// </summary>
        internal bool HasIncompatibleEmscriptenArg => FindIncompatibleEmscriptenArgs(emscriptenArgs).Any() && debugSymbolMode == WebGLDebugSymbolMode.External;

        internal static ManagedStrippingLevel GetManagedStrippingLevel() =>
            PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
        internal static void SetManagedStrippingLevel(ManagedStrippingLevel l) =>
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, l);

        internal static Il2CppCodeGeneration GetIl2CppCodeGeneration() =>
            PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
        internal static void SetIl2CppCodeGeneration(Il2CppCodeGeneration l) =>
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, l);

        internal static Il2CppCompilerConfiguration GetIl2CppCompilerConfiguration() =>
            PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);
        internal static void SetIl2CppCompilerConfiguration(Il2CppCompilerConfiguration l) =>
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, l);
        internal static IEnumerable<string> FindIncompatibleEmscriptenArgs(string emscriptenArgs) =>
            string.IsNullOrEmpty(emscriptenArgs)
                ? new string[0]
                : emscriptenArgs.Split(' ').Where(arg => arg == "--profiling-funcs" || arg == "--profiling");
    }
}
