using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WebGLStripPostBuild : IPreprocessBuildWithReport
{
    const string SettingsPath = "Assets/DefaultSubmoduleStrippingSettings.asset";

    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
        {
            return;
        }

        var settings = AssetDatabase.LoadAssetAtPath<SubmoduleStrippingSettings>(SettingsPath);
        if (settings == null)
        {
            throw new BuildFailedException(
                $"WebGL submodule stripping settings were not found at '{SettingsPath}'."
            );
        }

        if (settings.SubmodulesToStrip == null || settings.SubmodulesToStrip.Count == 0)
        {
            throw new BuildFailedException(
                $"WebGL submodule stripping settings at '{SettingsPath}' contain no submodules."
            );
        }

        StrippingProjectSettings.ActiveSettings = settings;
        StrippingProjectSettings.StripAutomaticallyAfterBuild = true;
        Debug.Log(
            $"[WebGLStrip] Enabled package-managed stripping with "
                + $"{settings.SubmodulesToStrip.Count} configured submodule(s)."
        );
    }
}
