using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Web.Stripping.Editor
{
    // Pre-/postprocesses Web builds to be suitable for submodule stripping
    class WebBuildPreprocessor : IPostprocessBuildWithReport, IPreprocessBuildWithReport
    {
        public static string LibraryMethodMapPath =>
            Path.Combine(Utils.LibraryPath, "Bee/artifacts/WebGL/il2cppOutput/cpp/Symbols/MethodMap.tsv");

        public int callbackOrder => 0; // SubmoduleStrippingBuildProcessor has 1

        public static string GetBuildGuidFilePath(BuildReport report) =>
            Path.Combine(report.summary.outputPath, WebBuildReport.k_BuildGuidFileName);

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            // Use build-guid.txt to determine whether this is a new build or not.
            // For existing builds we want to clean up the old stripping and profiling mods and artefacts.
            var build = WebBuildReportList.Instance.GetBuild(report.summary.outputPath);
            if (build is not null && File.Exists(GetBuildGuidFilePath(report)))
            {
                if (File.Exists(build.WasmBackupFilePath))
                    File.Delete(build.WasmBackupFilePath);
                if (File.Exists(build.FrameworkBackupFilePath))
                    File.Delete(build.FrameworkBackupFilePath);
                // Restore delete additional files and the rebuild will take care of the actual build files
                build.Restore();
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;
            // NOTE: it seems we can get Unknown for successful builds also, so only skip explicitly failed builds
            if (report.summary.result == BuildResult.Failed)
                return;

            // Make sure build GUID is updated (any modification to the project and it gets a new GUID, even if existing folder was used)
            var buildGuidFilePath = GetBuildGuidFilePath(report);
            File.WriteAllText(buildGuidFilePath, report.summary.guid.ToString());

            var build = WebBuildReportList.Instance.AddOrUpdateBuild(report.summary.outputPath);

            // Store the files required for stripping into backup folder.
            // Adding/updating the WebBuildReport had made sure the backup folder exists

            File.WriteAllText(
                Path.Combine(build.GetBackupFolderPath(), WebBuildReport.PlayerSettingFileName),
                JsonConvert.SerializeObject(WebPlayerSettings.FromPlayerSettings())
            );

            if (File.Exists(LibraryMethodMapPath))
            {
                File.Copy(
                    LibraryMethodMapPath,
                    Path.Combine(build.GetBackupFolderPath(), WebBuildReport.MethodMapFileName),
                    overwrite: true
                );
            }

            // Set Unity version and Emscripten version after build
            // and serialize build list
            build.UnityVersion = InternalEditorUtility.GetUnityDisplayVersion();
            build.EmscriptenVersion = BuildToolsLocator.EmscriptenVersion;
            WebBuildReportList.Instance.UpdateBuild(build);
        }
    }

    static class Utils
    {
        // filePath can be absolute or just a file name
        public static string GetBackupFileName(string backupDirectory, string filePath, string bakFileExt) =>
            Path.Combine(backupDirectory, $"{Path.GetFileName(filePath)}{bakFileExt}");

        // C:\Path\To\MyProject
        public static readonly string ProjectPath = Path.GetDirectoryName(Application.dataPath);

        // C:\Path\To\MyProject\Library
        public static readonly string LibraryPath = Path.Combine(ProjectPath, "Library");

        // C:\Path\To\GitRepo\package or C:\Path\To\Project\Library\PackageCache\com.unity.web.stripping-tool
        public static readonly string PackagePath = UnityEditor
            .PackageManager.PackageInfo.FindForPackageName(PackageConstants.PackageName)
            .resolvedPath;
    }
}
