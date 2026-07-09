# Add submodule profiling with scripting

Use the `WebBuildProcessor` class to instrument a build for [submodule profiling](submodule-profiling.md).

Create a script in `Assets\Editor`:
<!-- The source code of the sample is Samples~\SubmoduleProfiling\Editor\SubmoduleProfilingScriptingSample.cs -->
```C#
using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

static class SubmoduleProfilingScriptingSample
{
    public const string SampleName = "Submodule Profiling";
    public const string SampleNameNoSpace = "SubmoduleProfiling";

    [MenuItem("Window/" + SubmoduleStrippingSettings.RootMenuName + "/Samples/" + SampleName + " Scripting Sample")]
    static void RunSample()
    {
        var buildReport = Utils.BuildSamplePlayer();
        if (buildReport.summary.result == BuildResult.Failed)
        {
            Debug.LogError("Build failed");
            return;
        }

        var webBuild = WebBuildReportList.Instance.GetBuild(buildReport.summary.outputPath);

        var successful = WebBuildProcessor.InstrumentBuild(webBuild);
        if (successful)
            Debug.Log("The build was instrumented successfully.");
        else
            Debug.LogError("Failed to instrument the build.");
    }

    // The rest of the code is common utilities shared between the samples
    public static class Utils
    {
        public static SubmoduleStrippingSettings StrippingSettings;

        public static BuildReport BuildSamplePlayer()
        {
            // Use a settings scope so that our modifications to build settings are reverted automatically
            using var _ = new WebPlayerSettingsScope();

            // Currently submodule profiling works only on the Default template.
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            // Builds are faster without compression
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            // This option is recommended for new projects
            PlayerSettings.WebGL.wasm2023 = true;
            // This option is required for submodule stripping
            PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility = true;
            // Low managed stripping level to make the build faster
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            // Optimize Size makes the build faster and is recommended for Web builds
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            // Release builds are preferred for submodule stripping
            EditorUserBuildSettings.development = false;
            // "BuildTimes" == Shorter Build Time
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.WebGL), "CodeOptimization", "BuildTimes");

            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GetSampleScenePath() },
                locationPathName = $"Builds/Samples/{SampleNameNoSpace}",
                target = BuildTarget.WebGL,
            });
        }

        public static string GetSampleScenePath()
        {
            var path = $"{GetSamplePath()}/{SampleNameNoSpace}.unity";
            if (AssetDatabase.AssetPathExists(path))
                return path;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        public static string GetSamplePath()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.web.stripping-tool");
            return $"Assets/Samples/{pkg.displayName}/{pkg.version}/{SampleName}";
        }

        public static SubmoduleStrippingSettings GetSampleStrippingSettings()
        {
            if (StrippingSettings == null)
                StrippingSettings = SubmoduleStrippingSettings.Create($"{GetSamplePath()}/StrippingSettings.asset");
            return StrippingSettings;
        }
    }
}
```
