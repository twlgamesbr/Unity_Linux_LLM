using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A helper class that finds the Web Platform build tools path
    /// for the currently used Unity Editor version.
    /// </summary>
    class BuildToolsLocator
    {
        // Same as IsWebBuildTargetSupported() but a bit more "user-friendly" language + can be altered for testing purposes
        public static bool IsWebBuildSupportInstalled { get; internal set; } = IsWebBuildTargetSupported();

        internal static bool IsWebBuildTargetSupported() =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        /// <summary>
        /// The absolute path to the build tools, either in the Unity Web Platform PlaybackEngine module or embedded in the package.
        /// </summary>
        public static string BuildToolsPath =>
            Directory.Exists(PackageBuildToolsPath) ? PackageBuildToolsPath : UnityBuildToolsPath;

        /// <summary>
        /// The absolute path to the build tools in Unity Editor.
        /// </summary>
        static string UnityBuildToolsPath
        {
            get
            {
                if (m_BuildToolsPath == "")
                {
                    m_BuildToolsPath = GetBuildToolsPath();
                }

                return m_BuildToolsPath;
            }
        }

        /// <summary>
        /// The absolute path to the build tools in this package, if they are embedded.
        /// </summary>
        static string PackageBuildToolsPath => Path.Combine(Utils.PackagePath, ".BuildTools");

        /// <summary>
        /// The absolute path to Emscripten.
        /// Usually uses the Emscripten bundled with Unity but it can be overwritten by setting the environment variable
        /// "UNITY_EMSDK" or by embedding Emscripten to ".BuildTools" folder of the package.
        /// </summary>
        public static string EmscriptenSdkPath
        {
            get
            {
                var emsdkOverride = Environment.GetEnvironmentVariable("UNITY_EMSDK");
                if (!string.IsNullOrEmpty(emsdkOverride))
                    return emsdkOverride;

                var buildToolsPath = BuildToolsPath;
                var emscriptenFolder = Path.Combine(
                    buildToolsPath,
                    CommandLineUtils.HostPlatformPick(
                        "emscripten-linux-x64",
                        "emscripten-mac-x64",
                        "emscripten-mac-arm64",
                        "emscripten-win-x64"
                    )
                );
                if (Directory.Exists(emscriptenFolder))
                    return emscriptenFolder;

                return Path.Combine(buildToolsPath, "Emscripten");
            }
        }

        /// <summary>
        /// Return the version of the currently used Emscripten.
        /// Usually uses the Emscripten bundled with Unity but it can be overwritten by setting the environment variable
        /// "UNITY_EMSDK" or by embedding Emscripten to ".BuildTools" folder of the package.
        /// </summary>
        public static string EmscriptenVersion
        {
            get
            {
                if (string.IsNullOrEmpty(s_EmscriptenVersion))
                {
                    var emscriptenVersionFile = Path.Combine(EmscriptenSdkPath, "emscripten", "emscripten-version.txt");
                    if (File.Exists(emscriptenVersionFile))
                        s_EmscriptenVersion = File.ReadAllText(emscriptenVersionFile).TrimEnd();
                }

                return s_EmscriptenVersion;
            }
        }
        private static string s_EmscriptenVersion;

        /// <summary>
        /// Path to the brotli compression tool folder.
        /// </summary>
        public static string BrotliPath => Path.Combine(BuildToolsPath, "Brotli");

        /// <summary>
        /// Path to the 7z executable.
        /// </summary>
#if UNITY_6000_3_OR_NEWER
        public static string SevenZipPath => EditorApplication.sevenZipPath;
#else
        public static string SevenZipPath =>
            Path.Combine(
                EditorApplication.applicationContentsPath,
                "Tools",
                CommandLineUtils.HostPlatformPick("7za", "7za", "7za", "7z.exe")
            );
#endif

        /// <summary>
        /// Get paths to submodule definition files. This includes the submodule definition file in
        /// the WebGLSupport module and the submodule definition files in this package.
        /// </summary>
        /// <param name="useMultithreading">Set to true if multithreading support is enabled.</param>
        /// <param name="useWasm2023">Set to true if Wasm2023 features are enabled.</param>
        /// <returns>Paths to submodules definition files</returns>
        public static List<string> GetSubmoduleDefinitionFilePaths(bool useMultithreading, bool useWasm2023)
        {
            var paths = new List<string>();
            paths.Add(GetSubmoduleDefinitionFilePath(useMultithreading, useWasm2023));
            paths.AddRange(GetPackageSubmoduleDefinitionPaths());

            return paths;
        }

        /// <summary>
        /// Get the path to the submodules definition file in WebGLSupport module depending on the build configuration
        /// </summary>
        /// <param name="useMultithreading">Set to true if multithreading support is enabled.</param>
        /// <param name="useWasm2023">Set to true if Wasm2023 features are enabled.</param>
        /// <returns>Path to submodules definition file</returns>
        public static string GetSubmoduleDefinitionFilePath(bool useMultithreading, bool useWasm2023)
        {
            var moduleFolderName = "modules";
            if (useMultithreading)
            {
                // WebAssembly 2023 is implicitly enabled when multithreading is enabled
                moduleFolderName = "modules_mt_wasm23";
            }
            else if (useWasm2023)
            {
                moduleFolderName = "modules_wasm23";
            }

            return Path.Combine(UnityBuildToolsPath, "lib", moduleFolderName, "submodules.json");
        }

        public static List<string> GetPackageSubmoduleDefinitionPaths()
        {
            var submoduleDefinitionPaths = new List<string>();

            foreach (var path in Directory.GetFiles(Path.Combine(Utils.PackagePath, "SubmoduleDefinitions")))
            {
                // Only add .json files and skip .meta files
                if (path.EndsWith(".json"))
                {
                    submoduleDefinitionPaths.Add(path);
                }
            }

            return submoduleDefinitionPaths;
        }

        private static string m_BuildToolsPath = "";

        private static string GetBuildToolsPath()
        {
            // The Playback engine is most likely installed inside the Unity Editor folder.
            // On Windows: C:\unity_xxxx.x\Editor\Data\PlaybackEngines\WebGLSupport\BuildTools
            // On Mac /Applications/Unity.app/Contents/PlaybackEngines/WebGLSupport/BuildTools
            var path = Path.Combine(
                EditorApplication.applicationContentsPath,
                "PlaybackEngines",
                "WebGLSupport",
                "BuildTools"
            );

            // Sometimes it is installed in a separate folder at the same level as the Unity Editor.
            if (!Directory.Exists(path))
            {
                path = Path.GetFullPath(
                    Path.Combine(
                        EditorApplication.applicationPath,
                        "..",
                        "PlaybackEngines",
                        "WebGLSupport",
                        "BuildTools"
                    )
                );
            }

            // Manual installs can have this path:
            if (!Directory.Exists(path))
            {
                path = Path.GetFullPath(
                    Path.Combine(
                        EditorApplication.applicationPath,
                        "..",
                        "..",
                        "..",
                        "..",
                        "WebGLSupport",
                        "BuildTools"
                    )
                );
            }

            if (!Directory.Exists(path))
            {
                Debug.LogError(
                    $"Can not find Web Platform build tools path for Unity installed in \"{EditorApplication.applicationPath}\"\nIs the Web Platform module not installed?"
                );
                return "";
            }

            return path;
        }
    }
}
