using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Can be used to add submodule profiling to a build, and perform submodule stripping for a build.
    /// </summary>
    public class WebBuildProcessor
    {
        internal const string k_ProfilingIconFileName = "submodule-profiling-icon.png";

        /// <summary>
        /// Adds submodule profiling to a build.
        /// </summary>
        /// <param name="build">The Web build.</param>
        /// <remarks>Shows a progress bar. This option isn't available for stripped builds or builds that are already instrumented for submodule profiling.</remarks>
        /// <returns>Returns 'true' if the build was successfully instrumented, 'false' otherwise.</returns>
        public static bool InstrumentBuild(WebBuildReport build)
        {
            try
            {
                var playerSettings = build.GetWebPlayerSettings();
                if (playerSettings == null)
                {
                    Debug.LogError($"Could not read WebPlayerSettings JSON from {build.OutputPath}. This file is required for stripping. Rebuild to generate the file.");
                    return false;
                }

                var effectivePlayerSettings = build.GetEffectivePlayerSettings();
                if (!effectivePlayerSettings.HasDebugInfo)
                {
                    Debug.LogError($"Build is missing debug information. Can't add submodule profiling to the build.");
                    return false;
                }

                if (effectivePlayerSettings.HasIncompatibleEmscriptenArg)
                {
                    var incompatibleArgs = WebPlayerSettings.FindIncompatibleEmscriptenArgs(effectivePlayerSettings.emscriptenArgs);
                    Debug.LogError($"Build has the incompatible Emscripten argument '{string.Join(' ', incompatibleArgs)}'.  Can't add submodule profiling to the build.");
                    return false;
                }

                if (build.HasSubmoduleProfiling)
                {
                    Debug.LogError($"Build '{build.WasmFilePath}' already has submodule profiling enabled.");
                    return false;
                }

                if (build.HasStrippingInfo)
                {
                    Debug.LogError($"Build '{build.WasmFilePath}' is already stripped and can't be instrumented for profiling.");
                    return false;
                }

                if (!string.IsNullOrEmpty(build.EmscriptenVersion) && build.EmscriptenVersion != BuildToolsLocator.EmscriptenVersion)
                {
                    Debug.LogError($"Build was created with a different Emscripten version than available in the Unity Editor: '{build.EmscriptenVersion}'. The editor uses '{BuildToolsLocator.EmscriptenVersion}'. Please rebuild the project or open the project with a different editor version.");
                    return false;
                }

                DefaultProgressCallback(0, 1, "Add profiling to build.");

                // Get Emscripten version of build
                var emscriptenVersion = !string.IsNullOrEmpty(build.EmscriptenVersion)
                    ? build.EmscriptenVersion
                    : "Unknown";

                // Create backup of framework and wasm file
                var originalWasmFile = BackUpBuildFile(build, build.WasmFilePath);
                build.WasmBackupFilePath = originalWasmFile;
                var originalFrameworkFile = BackUpBuildFile(build, build.FrameworkFilePath);
                build.FrameworkBackupFilePath = originalFrameworkFile;

                var submoduleDefinitionFiles = BuildToolsLocator.GetSubmoduleDefinitionFilePaths(playerSettings.threadsSupport, playerSettings.wasm2023);
                var submoduleProfiler = new SubmoduleProfiler()
                {
                    SubmoduleDefinitionFiles = submoduleDefinitionFiles,
                    SymbolFile = build.SymbolFilePath,
                    MethodMapFile = build.MethodMapFilePath,
                    UnityVersion = Application.unityVersion,
                    EmscriptenSdkPath = BuildToolsLocator.EmscriptenSdkPath,
                    BrotliPath = BuildToolsLocator.BrotliPath,
                    SevenZipPath = BuildToolsLocator.SevenZipPath,
                    EnableEmscripten4Features = emscriptenVersion.StartsWith("4."),
                    LogFunctionCode = TemplateAssetsHelper.GetFunctionCode("SubmoduleProfilingOverlay.wasmimport"),
                    MinifiedLogFunctionCode = TemplateAssetsHelper.GetFunctionCode("MinifiedSubmoduleProfilingOverlay.wasmimport")
                };

                submoduleProfiler.InstrumentBuild(
                    originalWasmFile,
                    build.WasmFilePath,
                    originalFrameworkFile,
                    build.FrameworkFilePath,
                    build.BuildPath
                );

                DefaultProgressCallback(1, 1, "Finished.");

                // Copy icon file
                CopyProfilingIcon(build);
                // Make sure additional files are cleaned up if Restore functionality is used
                build.AdditionalFiles.Add(Path.Combine(build.BuildPath, "functions.json"));
                build.AdditionalFiles.Add(Path.Combine(build.BuildPath, "labels.json"));
                build.HasSubmoduleProfiling = true;

                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"An error occurred when adding submodule profiling to build:\n{error}");

                // Try to restore files
                build.Restore();

                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Remove submodule profiling from build.
        /// This restores the backup files of the WebAssembly and JavaScript Framework and deletes
        /// any additional TemplateData, for example, icons.
        /// </summary>
        /// <param name="build">The Web build.</param>
        /// <returns>Returns 'true' if submodule profiling code was removed, 'false' otherwise.</returns>
        public static bool RemoveInstrumentationFromBuild(WebBuildReport build)
        {
            if (!build.HasSubmoduleProfiling)
            {
                Debug.LogWarning($"Build {build.WasmFilePath} does not have submodule profiling enabled.");
                return false;
            }

            build.Restore();

            return true;
        }

        internal static string BackUpBuildFile(WebBuildReport build, string filePath)
        {
            // Technically we don't need the .bak file extension anymore but keeping it to be consistent with the old naming
            string GetBackupFileName(string filePath) =>
                Utils.GetBackupFileName(build.GetBackupFolderPath(), filePath, FileBackup.BackupFileExtension);

            return FileBackup.BackupFile(filePath, GetBackupFileName(filePath));
        }

        internal static void CopyProfilingIcon(WebBuildReport build)
        {
            var iconFile = Path.Combine(TemplateAssetsHelper.DataPath, k_ProfilingIconFileName);
            var templateDataPath = build.TemplateDataPath;
            if (!Directory.Exists(templateDataPath))
                Directory.CreateDirectory(templateDataPath);
            var destination = Path.Combine(templateDataPath, "submodule-profiling-icon.png");

            if (!File.Exists(destination))
            {
                File.Copy(iconFile, destination);
                build.AdditionalFiles.Add(destination);
            }
        }

        /// <summary>
        /// Perform submodule stripping for a build.
        /// </summary>
        /// <remarks>
        /// Before a build is stripped, it is restored to its original state, meaning,
        /// existing submodule profiling instrumentation or submodule stripping will be reverted.
        /// Shows a progress bar.
        /// </remarks>
        /// <param name="build">The Web build.</param>
        /// <param name="settings">The stripping settings to be used.</param>
        /// <returns>Returns 'true' if submodule stripping was performed, 'false' otherwise.</returns>
        public static bool StripBuild(WebBuildReport build, SubmoduleStrippingSettings settings) =>
            StripBuild(build, settings, null);

        internal static bool StripBuild(
            WebBuildReport build,
            SubmoduleStrippingSettings settings,
            string additionalSubmoduleDefinitionFile)
        {
            if (build == null)
            {
                Debug.LogError($"No Web build report specified.");
                return false;
            }

            if (settings == null)
            {
                Debug.LogError($"No submodules stripping settings specified.");
                return false;
            }

            if (!settings.CanRunStripping)
            {
                Debug.LogError($"No submodules or optimization passes specified.");
                return false;
            }

            var playerSettings = build.GetWebPlayerSettings();
            if (playerSettings == null)
            {
                Debug.LogError($"Could not read WebPlayerSettings JSON from {build.OutputPath}. This file is required for stripping. Rebuild to generate the file.");
                return false;
            }

            var effectivePlayerSettings = build.GetEffectivePlayerSettings();
            if (!effectivePlayerSettings.HasDebugInfo)
            {
                Debug.LogError($"Build is missing debug information. Can't strip submodules.");
                return false;
            }

            if (effectivePlayerSettings.HasIncompatibleEmscriptenArg)
            {
                var incompatibleArgs = WebPlayerSettings.FindIncompatibleEmscriptenArgs(effectivePlayerSettings.emscriptenArgs);
                Debug.LogError($"Build has the incompatible Emscripten argument '{string.Join(' ', incompatibleArgs)}'. Can't strip submodules.");
                return false;
            }

            if (!string.IsNullOrEmpty(build.EmscriptenVersion) && build.EmscriptenVersion != BuildToolsLocator.EmscriptenVersion)
            {
                Debug.LogError($"Build was created with a different Emscripten version than available in the Unity Editor: '{build.EmscriptenVersion}'. The editor uses '{BuildToolsLocator.EmscriptenVersion}'. Please rebuild the project or open the project with a different editor version.");
                return false;
            }

            // Remove submodule profiling code if present
            if (build.HasSubmoduleProfiling)
            {
                Debug.Log("Removing submodule profiling code before stripping.");
                RemoveInstrumentationFromBuild(build);
            }

            // Restore original wasm file if build has been stripped previously
            if (!string.IsNullOrEmpty(build.WasmBackupFilePath))
            {
                Debug.Log("Restoring original wasm file for stripping.");
                build.Restore();
            }

            // Warn user if build was created without enableSubmoduleStrippingCompatibility
            if (PlayerSettingsHelper.IsSubmoduleStrippingCompatibilityAvailable
                && !playerSettings.enableSubmoduleStrippingCompatibility)
            {
                Debug.LogWarning("Submodule stripping works best with \"Enable Submodule Stripping Compatibility\". Rebuild with the setting enabled for best results.");
            }

            // Replace the original file, make a back-up of it.
            // The originals will be rewritten, i.e., they are the output files
            // NOTE: technically we don't need the .bak file extension anymore as the back-ups are
            // in a dedicated directory, but keeping it for now.
            var backupDirectory = build.GetBackupFolderPath();
            Directory.CreateDirectory(backupDirectory);

            var outputWasmFile = build.WasmFilePath;
            var originalWasmFile = FileBackup.BackupFile(
                outputWasmFile,
                Utils.GetBackupFileName(backupDirectory, build.WasmFilePath, FileBackup.BackupFileExtension)
            );
            build.WasmBackupFilePath = originalWasmFile;

            // Create back-up of framework file if missing submodule handling is enabled
            var enableLoggingOfMissingSubmodule = false;
            var outputFrameworkFile = build.FrameworkFilePath;
            var originalFrameworkFile = outputFrameworkFile;
            var logFunctionCode = "";
            var minifiedLogFunctionCode = "";
            if (settings.MissingSubmoduleErrorHandling != MissingSubmoduleErrorHandlingType.Ignore)
            {
                enableLoggingOfMissingSubmodule = true;
                originalFrameworkFile = FileBackup.BackupFile(
                    outputFrameworkFile,
                    Utils.GetBackupFileName(backupDirectory, outputFrameworkFile, FileBackup.BackupFileExtension)
                );
                build.FrameworkBackupFilePath = originalFrameworkFile;
                var replacements = new Dictionary<string, string>
                {
                    {
                        "THROW_ON_EXECUTION",
                        (settings.MissingSubmoduleErrorHandling == MissingSubmoduleErrorHandlingType.ThrowException).ToString().ToLower()
                    }
                };

                logFunctionCode = TemplateAssetsHelper.GetFunctionCode("SubmoduleErrorHandler.wasmimport", replacements);
                minifiedLogFunctionCode = TemplateAssetsHelper.GetFunctionCode("MinifiedSubmoduleErrorHandler.wasmimport", replacements);
            }

            var submoduleDefinitionFiles = BuildToolsLocator.GetSubmoduleDefinitionFilePaths(playerSettings.threadsSupport, playerSettings.wasm2023);
            // Also use user-supplied submodule definition file if present
            if (!string.IsNullOrEmpty(additionalSubmoduleDefinitionFile))
            {
                submoduleDefinitionFiles.Add(additionalSubmoduleDefinitionFile);
            }

            // Remove debug information added by SubmoduleStrippingBuildProcessor
            // if user selected no debug symbols in the player options (WebGLDebugSymbolMode.Off).
            var removeDebugInformation = playerSettings.debugSymbolMode == WebGLDebugSymbolMode.Off;

            // Get Emscripten version of build
            var emscriptenVersion = !string.IsNullOrEmpty(build.EmscriptenVersion)
                ? build.EmscriptenVersion
                : "Unknown";

            // Start stripping process
            var submoduleStripper = new SubmoduleStripper()
            {
                BuildCompressionType = Compression.GetCompressionType(originalWasmFile),
                SubmoduleDefinitionFiles = submoduleDefinitionFiles,
                Optimize = settings.OptimizeCodeAfterStripping,
                KeepDebugInformation = !(settings.RemoveDebugInformation || removeDebugInformation),
                EnableSimdSupport = playerSettings.wasm2023, // SIMD is bundled with Wasm2023
                EnableBigIntSupport = playerSettings.webAssemblyBigInt || playerSettings.wasm2023,
                EnableEmscripten4Features = emscriptenVersion.StartsWith("4."),
                Development = playerSettings.BuildSettings.development,
                CodeOptimization = playerSettings.BuildSettings.codeOptimization,
                EnableLoggingOfMissingSubmodule = enableLoggingOfMissingSubmodule,
                LogFunctionCode = logFunctionCode,
                MinifiedLogFunctionCode = minifiedLogFunctionCode,
                EmscriptenSdkPath = BuildToolsLocator.EmscriptenSdkPath,
                BrotliPath = BuildToolsLocator.BrotliPath,
                SevenZipPath = BuildToolsLocator.SevenZipPath,
                SubmodulesToStrip = settings.SubmodulesToStrip,
                MethodMapFile = build.MethodMapFilePath,
                UnityVersion = Application.unityVersion,
                ProductVersion = PackageConstants.VerboseVersion,
                MissingSubmoduleErrorHandling = ObjectNames.NicifyVariableName(settings.MissingSubmoduleErrorHandling.ToString())
            };

            if (!string.IsNullOrEmpty(build.SymbolFilePath))
            {
                Debug.Log($"Using SymbolFile: {build.SymbolFilePath}");
                submoduleStripper.SymbolFile = build.SymbolFilePath;
            }

            submoduleStripper.OnProgress += DefaultProgressCallback;
            try
            {
                submoduleStripper.StripSubmodules(originalWasmFile, outputWasmFile, originalFrameworkFile, outputFrameworkFile);

                // stripping_info.json is always written next the the output wasm, move the info to our backup folder
                var strippingInfoPath = Path.Combine(Path.GetDirectoryName(outputWasmFile), SubmoduleStripper.StrippingInfoFileName);
                if (File.Exists(strippingInfoPath))
                {
                    var strippingInfoBakPath = Utils.GetBackupFileName(backupDirectory, SubmoduleStripper.StrippingInfoFileName, null);
                    if (File.Exists(strippingInfoBakPath))
                        File.Delete(strippingInfoBakPath);
                    File.Move(strippingInfoPath, strippingInfoBakPath);
                }

                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"An error occurred during stripping of submodules:\n{error}");
                // It's possible we lost the orignal wasm, restore it from the back-up, if we have it.
                build.Restore();
                return false;
            }
            finally
            {
                build.Update();
                EditorUtility.ClearProgressBar();
            }
        }

        internal static void DefaultProgressCallback(int step, int totalSteps, string description) =>
            EditorUtility.DisplayProgressBar("WASM Submodule Stripping", description, (float)step / totalSteps);
    }
}
