using Unity.Web.Stripping.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Automatically strips unused .NET submodules from WebGL builds using
/// com.unity.web-stripping-tool@1.3.0.
///
/// Reads submodule list from Assets/DefaultSubmoduleStrippingSettings.asset
/// and applies stripping after every successful WebGL build.
/// </summary>
public class WebGLStripPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogWarning("[WebGLStrip] Skipping post-build stripping: build did not succeed.");
            return;
        }

        // Load settings from the project's default stripping settings asset
        string settingsPath = "Assets/DefaultSubmoduleStrippingSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<SubmoduleStrippingSettings>(settingsPath);
        if (settings == null)
        {
            Debug.LogError(
                $"[WebGLStrip] Could not load stripping settings at '{settingsPath}'. "
                + "Create one via Assets > Create > Web Stripping > Submodule Stripping Settings."
            );
            return;
        }

        if (settings.SubmodulesToStrip == null || settings.SubmodulesToStrip.Count == 0)
        {
            Debug.Log("[WebGLStrip] No submodules configured to strip. Skipping.");
            return;
        }

        // Get the WebBuild for this output
        var webBuild = WebBuildReportList.Instance.GetBuild(report.summary.outputPath);
        if (webBuild == null)
        {
            Debug.LogError(
                $"[WebGLStrip] No WebBuild found for output path: {report.summary.outputPath}. "
                + "Ensure 'Enable Submodule Stripping Compatibility' is checked in WebGL Player Settings."
            );
            return;
        }

        bool success = WebBuildProcessor.StripBuild(webBuild, settings);
        if (success)
        {
            long savedBytes = EstimateSavedBytes(settings);
            Debug.Log(
                $"[WebGLStrip] Stripped {settings.SubmodulesToStrip.Count} submodule(s) "
                + $"from WebGL build (~{FormatBytes(savedBytes)} saved)."
            );
        }
        else
        {
            Debug.LogError("[WebGLStrip] Failed to strip WebGL build. Check Editor log for details.");
        }
    }

    static long EstimateSavedBytes(SubmoduleStrippingSettings settings)
    {
        // Rough estimates per submodule — exact savings depend on usage
        long total = 0;
        foreach (string module in settings.SubmodulesToStrip)
        {
            total += module switch
            {
                "System.Xml" => 800_000L,
                "System.Data" => 1_200_000L,
                "System.Drawing" => 300_000L,
                "System.IO.Compression" => 200_000L,
                "System.Runtime.Serialization" => 400_000L,
                "System.ComponentModel.DataAnnotations" => 100_000L,
                "System.Data.SqlClient" => 500_000L,
                _ => 200_000L,
            };
        }
        return total;
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
