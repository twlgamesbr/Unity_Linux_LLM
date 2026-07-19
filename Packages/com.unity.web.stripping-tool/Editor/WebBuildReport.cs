using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine; // GUID is here beginning in 6000.4.0a4

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Manages all files and build settings of a web build.
    /// </summary>
    /// <remarks>
    /// All path properties use absolute paths.
    /// </remarks>
    [Serializable]
    public class WebBuildReport
    {
        /// <summary>
        /// Create a web build report for a given path. Automatically updates all paths of a build.
        /// If the path is a relative path, it will be automatically converted to an absolute path.
        /// </summary>
        /// <param name="outputPath"> For example, "D:/MyProject/Builds/MyBuild", "Builds/MyBuild"</param>
        /// <returns>The build report</returns>
        public static WebBuildReport CreateFromPath(string outputPath)
        {
            if (!Path.IsPathFullyQualified(outputPath))
                outputPath = Path.GetFullPath(outputPath).Replace("\\", "/");

            var report = new WebBuildReport();
            report.OutputPath = outputPath;
            report.Update();

            return report;
        }

        /// <summary>
        /// Checks whether the "/Build" folder of a build contains the expected files.
        /// </summary>
        /// <param name="buildPath">E.g. "Path/To/MyBuild/Build"</param>
        /// <returns>True if build path is valid.</returns>
        public static bool IsValidBuildPath(string buildPath)
        {
            return Directory.Exists(buildPath)
                && Directory.GetFiles(buildPath, DataFilePattern).Length > 0
                && Directory.GetFiles(buildPath, FrameworkFilePattern).Length > 0
                && Directory.GetFiles(buildPath, LoaderFilePattern).Length > 0
                && Directory.GetFiles(buildPath, WasmFilePattern).Length > 0;
        }

        internal static bool IsValidBackupPath(string path) =>
            Directory.Exists(path)
            && Directory.GetFiles(path, PlayerSettingFileName).Length > 0
            && Directory.GetFiles(path, MethodMapFileName).Length > 0;

        internal bool IsBackupFolderValid() => IsValidBackupPath(GetBackupFolderPath());

        internal const string DataFilePattern = "*.data.*";
        internal const string FrameworkFilePattern = "*.framework.js.*";
        internal const string LoaderFilePattern = "*.loader.js";
        internal const string WasmFilePattern = "*.wasm.*";
        internal const string SymbolFilePattern = "*.symbols.json.*";
        internal const string k_BuildGuidFileName = "build-guid.txt";

        /// <summary>
        /// Returns the folder path where this build's back-up files are stored.
        /// </summary>
        /// <remarks>
        /// Valid if OutputPath is valid and Update() has been called.
        /// </remarks>
        /// <returns>Absolute path to the back-up folder.</returns>
        public string GetBackupFolderPath() => GetBackupFolderPath(Name, m_BuildGuid);

        // Returns an absolute path within the current Unity project
        internal static string GetBackupFolderPath(string name, GUID buildGuid)
        {
            static string SanitizeFileName(string name)
            {
                var invalidChars = Path.GetInvalidFileNameChars();
                return new string(name?.Where(c => !invalidChars.Contains(c)).ToArray());
            }

            return Path.Combine(
                // "D:/MyProject/Library"
                Utils.LibraryPath,
                // "com.unity.web.stripping-tool"
                PackageConstants.PackageName,
                // "MyBuild-914750b1110d4c878bf4dd504e81e590"
                $"{SanitizeFileName(name)}{(string.IsNullOrEmpty(name) ? "" : "-")}{buildGuid}"
            );
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public WebBuildReport()
        {
        }

        /// <summary>
        /// Reads the files from the build path and updates the fields accordingly.
        /// </summary>
        public void Update()
        {
            var outputPathDirInfo = new DirectoryInfo(OutputPath); // e.g. "D:/MyProject/Builds/MyBuild"
            Name = outputPathDirInfo.Name; // "MyBuild"
            if (outputPathDirInfo.Exists)
            {
                // Make sure build GUID and backup folder are up to date
                var oldBuildGuid = m_BuildGuid;
                var oldBackupFolder = GetBackupFolderPath();

                var buildGuidFilePath = Path.Combine(OutputPath, k_BuildGuidFileName);
                if (File.Exists(buildGuidFilePath)
                    && !GUID.TryParse(File.ReadAllText(buildGuidFilePath), out m_BuildGuid))
                {
                    Debug.LogWarning($"{buildGuidFilePath} did not contain a valid GUID, generating a new GUID.");
                }

                if (m_BuildGuid.Empty())
                {
                    m_BuildGuid = GUID.Generate();
                    File.WriteAllText(buildGuidFilePath, m_BuildGuid.ToString());
                }

                // If a rebuild has been made, make sure the old backup folder is deleted
                // (stripping/instrumenting is not considered a build mod).
                var buildModified = !oldBuildGuid.Empty() && oldBuildGuid != m_BuildGuid;
                if (buildModified && Directory.Exists(oldBackupFolder))
                    Directory.Delete(oldBackupFolder, recursive: true);

                var newBackupFolder = GetBackupFolderPath();
                Directory.CreateDirectory(newBackupFolder);
            }

            // BuildPath ("D:/MyProject/Builds/MyBuild/Build") is the only directory that
            // typically changes when overwriting a build so use its last-modified time.
            // If this appears to be a valid build, the value will be updated to be .wasm file's last-modified time
            LastModifiedAtUniversal = Directory.GetLastWriteTime(BuildPath).ToUniversalTime().ToString();

            var buildPath = BuildPath;
            var backupPath = GetBackupFolderPath();

            // TODO should we handle multiple .wasm(.bak) files in the folder? Could happen theoretically.
            WasmFilePath = GetFiles(buildPath, WasmFilePattern)
                .Where(fn => !fn.EndsWith(FileBackup.BackupFileExtension))
                .FirstOrDefault();
            // back-up files are stored in separate folder now but we still use the .bak extension
            WasmBackupFilePath = GetFiles(backupPath, WasmFilePattern)
                .Where(fn => fn.EndsWith(FileBackup.BackupFileExtension))
                .FirstOrDefault();

            if (IsValid)
            {
                LastModifiedAtUniversal = File.GetLastWriteTime(WasmFilePath).ToUniversalTime().ToString();
                if (string.IsNullOrEmpty(WasmBackupFilePath))
                {
                    // no wasm.bak -> no stripping, wasmFile is original file
                    OriginalWasmSize = new FileInfo(WasmFilePath).Length;
                    StrippedWasmSize = 0;
                }
                else
                {
                    // bak -> stripping, bak is the original, wasm is strippedWasm
                    OriginalWasmSize = new FileInfo(WasmBackupFilePath).Length;
                    StrippedWasmSize = new FileInfo(WasmFilePath).Length;
                }
            }

            FrameworkFilePath = GetFiles(buildPath, FrameworkFilePattern)
                .Where(fn => !fn.EndsWith(FileBackup.BackupFileExtension))
                .FirstOrDefault();

            FrameworkBackupFilePath = GetFiles(backupPath, FrameworkFilePattern)
                .Where(fn => fn.EndsWith(FileBackup.BackupFileExtension))
                .FirstOrDefault();

            SymbolFilePath = GetFiles(buildPath, SymbolFilePattern)
                .FirstOrDefault();

            StrippingInfoFilePath = GetFiles(backupPath, SubmoduleStripper.StrippingInfoFileName)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(StrippingInfoFilePath))
                AdditionalFiles.Add(StrippingInfoFilePath);

            const string ogPlayerSettingFileName = SubmoduleStrippingBuildProcessor.OriginalPlayerSettingFileName;
            var ogPlayerSettingsInBuildPath = Path.Combine(buildPath, ogPlayerSettingFileName);
            var ogPlayerSettingsInBackupPath = Path.Combine(backupPath, ogPlayerSettingFileName);
            if (File.Exists(ogPlayerSettingsInBuildPath))
            {
                if (File.Exists(ogPlayerSettingsInBackupPath))
                    File.Delete(ogPlayerSettingsInBackupPath);
                File.Move(ogPlayerSettingsInBuildPath, ogPlayerSettingsInBackupPath);
            }
            OriginalPlayerSettingsFilePath = GetFiles(backupPath, ogPlayerSettingFileName).FirstOrDefault();

            PlayerSettingsFilePath = GetFiles(backupPath, PlayerSettingFileName).FirstOrDefault();

            MethodMapFilePath = GetFiles(backupPath, MethodMapFileName).FirstOrDefault();
        }

        /// <summary>
        /// Get the stored `WebPlayerSettings` for this build.
        /// </summary>
        /// <returns>A `WebPlayerSettings` object with all settings that were used to create this build.</returns>
        public WebPlayerSettings GetWebPlayerSettings()
        {
            // Potential original settings take precedence
            if (File.Exists(OriginalPlayerSettingsFilePath))
                return JsonConvert.DeserializeObject<WebPlayerSettings>(File.ReadAllText(OriginalPlayerSettingsFilePath));
            if (File.Exists(PlayerSettingsFilePath))
                return JsonConvert.DeserializeObject<WebPlayerSettings>(File.ReadAllText(PlayerSettingsFilePath));
            return null;
        }

        /// <summary>
        /// Get the stored `WebPlayerSettings` for this build.
        /// Compared to GetWebPlayerSettings() this retrieves the player settings that were actually used to create the build.
        /// </summary>
        /// <returns></returns>
        internal WebPlayerSettings GetEffectivePlayerSettings()
        {
            if (File.Exists(PlayerSettingsFilePath))
                return JsonConvert.DeserializeObject<WebPlayerSettings>(File.ReadAllText(PlayerSettingsFilePath));
            return null;
        }

        /// <summary>
        /// Restores the build from its back-up files and removes any additional files we might have.
        /// </summary>
        public void Restore()
        {
            FileBackup.RestoreBackupFile(WasmBackupFilePath, WasmFilePath);
            FileBackup.RestoreBackupFile(FrameworkBackupFilePath, FrameworkFilePath);

            foreach (var file in AdditionalFiles)
            {
                if (!File.Exists(file))
                    continue;
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to delete file: {ex.Message}");
                }
            }

            AdditionalFiles.Clear();
            HasSubmoduleProfiling = false;

            Update();
        }

        /// <summary>
        /// The file name used to store the actual Player and build settings for the build.
        /// </summary>
        public const string PlayerSettingFileName = "player_settings.json";

        /// <summary>
        /// The file name of our copy of the method map.
        /// </summary>
        public const string MethodMapFileName = "MethodMap.tsv";

        /// <summary>
        /// The GUID of the build, stored also as build-guid.txt at the root of the build folder.
        /// </summary>
        /// <remarks>
        /// Should not be set directly, instead set it using the build-guid.txt file.
        /// Changes if a rebuild of the project is made with any modifications (assets, project settings, etc.) to the project.
        /// </remarks>
        [SerializeField]
        internal GUID m_BuildGuid;

        /// <summary>
        /// Display name of the build.
        /// </summary>
        public string Name;

        /// <summary>
        /// Root folder of build.
        /// for example, "D:/MyProject/Builds/MyBuild"
        /// </summary>
        public string OutputPath;

        /// <summary>
        /// The Unity version used to create this build.
        /// </summary>
        public string UnityVersion;

        /// <summary>
        /// The Emscripten version used to create this build.
        /// </summary>
        public string EmscriptenVersion;

        /// <summary>
        /// Path to build data inside build
        /// for example, "D:/MyProject/Builds/MyBuild/Build"
        /// </summary>
        public string BuildPath => Path.Combine(OutputPath, "Build");

        /// <summary>
        /// Path to template data inside build
        /// for example, "D:/MyProject/Builds/MyBuild/TemplateData"
        /// </summary>
        public string TemplateDataPath => Path.Combine(OutputPath, "TemplateData");

        /// <summary>
        /// Timestamp when build was last modified as string in universal time format
        /// `DateTime.ToUniversalTime().ToString()`.
        /// </summary>
        public string LastModifiedAtUniversal;

        /// <summary>
        /// Timestamp when build was last modified.
        /// </summary>
        public DateTimeOffset LastModifiedAt => DateTimeOffset.Parse(LastModifiedAtUniversal);

        /// <summary>
        /// Size of the original wasm file in bytes.
        /// </summary>
        public long OriginalWasmSize;

        /// <summary>
        /// Optional: Size of the stripped wasm file in bytes.
        /// </summary>
        public long StrippedWasmSize;

        /// <summary>
        /// Path to the WebAssembly file.
        /// </summary>
        public string WasmFilePath;

        /// <summary>
        /// Optional: Path to the backup WebAssembly file.
        /// This file is created when a build is stripped or instrumented for profiling.
        /// </summary>
        public string WasmBackupFilePath;

        /// <summary>
        /// Optional: Path to the external debug symbols file.
        /// </summary>
        public string SymbolFilePath;

        /// <summary>
        /// Optional: Path to the `stripping_info.json` file.
        /// </summary>
        public string StrippingInfoFilePath;

        /// <summary>
        /// Path to the Player settings file used for this build.
        /// </summary>
        public string PlayerSettingsFilePath;

        /// <summary>
        /// Path to the IL2CPP method map of this build.
        /// </summary>
        public string MethodMapFilePath;

        /// <summary>
        /// Path to the JavaScript framework file.
        /// </summary>
        public string FrameworkFilePath;

        /// <summary>
        /// Optional: Path to the backup JavaScript framework file.
        /// This file is created when a build is instrumented for profiling.
        /// </summary>
        public string FrameworkBackupFilePath;

        /// <summary>
        /// Additional files are files that, for example, submodule stripping or submodule profiling instrumentation
        /// might add to to the build folder. Files that are prerequisites for these operations are not considered additional files.
        /// </summary>
        public List<string> AdditionalFiles = new();

        /// <summary>
        /// Is true when the build was instrumented for submodule profiling.
        /// </summary>
        public bool HasSubmoduleProfiling;

        /// <summary>
        /// Is true when the build has a stripping info file.
        /// </summary>
        public bool HasStrippingInfo => File.Exists(StrippingInfoFilePath);

        /// <summary>
        /// If SubmoduleStrippingBuildProcessor modified settings, the originals are here.
        /// </summary>
        public string OriginalPlayerSettingsFilePath;

        // Could be moved to Utils
        // Directory.GetFiles() but also safe to call for directories that do not exist
        internal static string[] GetFiles(string path, string searchPattern) =>
            Directory.Exists(path) ? Directory.GetFiles(path, searchPattern) : Array.Empty<string>();

        /// <summary>
        /// Returns true if the build at Path is a valid web build.
        /// </summary>
        public bool IsValid => File.Exists(WasmFilePath);
    }
}
