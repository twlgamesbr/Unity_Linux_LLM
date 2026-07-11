using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WebGLIndexPostBuild : IPostprocessBuildWithReport
{
    const string BrowserAgentDomain = "datadoghq-browser-agent.com";

    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        string indexPath = Path.Combine(report.summary.outputPath, "index.html");
        if (!File.Exists(indexPath))
            return;

        string html = File.ReadAllText(indexPath);
        if (!html.Contains(BrowserAgentDomain))
            return;

        string sanitized = Regex.Replace(
            html,
            @"\s*<script\b(?:(?!</script>).)*" + Regex.Escape(BrowserAgentDomain) + @"(?:(?!</script>).)*</script>",
            string.Empty,
            RegexOptions.Singleline
        );

        if (sanitized == html)
            return;

        File.WriteAllText(indexPath, sanitized);
        Debug.Log("[WebGLIndexPostBuild] Removed stale Datadog browser-agent script from WebGL index.html.");
    }
}
