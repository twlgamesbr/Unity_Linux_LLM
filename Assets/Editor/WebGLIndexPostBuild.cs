using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class WebGLIndexPostBuild : IPostprocessBuildWithReport
{
    const string BrowserAgentDomain = "datadoghq-browser-agent.com";
    const string DatadogStubMarker = "webgl-datadog-noop-stub";

    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        string indexPath = Path.Combine(report.summary.outputPath, "index.html");
        if (!File.Exists(indexPath))
            return;

        string html = File.ReadAllText(indexPath);
        string sanitized = SanitizeDatadogBrowserAgent(html);

        if (sanitized == html)
            return;

        File.WriteAllText(indexPath, sanitized);
        Debug.Log("[WebGLIndexPostBuild] Removed stale Datadog browser-agent script from WebGL index.html.");
    }

    public static string SanitizeDatadogBrowserAgent(string html)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains(BrowserAgentDomain))
            return html;

        string sanitized = Regex.Replace(
            html,
            @"\s*<script\b(?:(?!</script>).)*" + Regex.Escape(BrowserAgentDomain) + @"(?:(?!</script>).)*</script>",
            string.Empty,
            RegexOptions.Singleline
        );

        if (sanitized.Contains(DatadogStubMarker))
            return sanitized;

        int headEnd = sanitized.IndexOf("</head>", System.StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0)
            return sanitized;

        return sanitized.Insert(headEnd, DatadogNoopStub);
    }

    const string DatadogNoopStub =
        "    <script id=\"webgl-datadog-noop-stub\">\n"
        + "      window.DD_RUM = window.DD_RUM || {\n"
        + "        onReady: function () {}, init: function () {}, addAction: function () {},\n"
        + "        addError: function () {}, setUser: function () {}, setUserProperty: function () {},\n"
        + "        setTrackingConsent: function () {}, setGlobalContextProperty: function () {},\n"
        + "        removeGlobalContextProperty: function () {}, addTiming: function () {},\n"
        + "        addFeatureFlagEvaluation: function () {}, startView: function () {}, stopSession: function () {}\n"
        + "      };\n"
        + "      window.DD_LOGS = window.DD_LOGS || {\n"
        + "        onReady: function () {}, init: function () {}, setUser: function () {},\n"
        + "        setUserProperty: function () {}, setTrackingConsent: function () {},\n"
        + "        setGlobalContextProperty: function () {}, removeGlobalContextProperty: function () {},\n"
        + "        createLogger: function () { return { debug: function () {}, info: function () {}, warn: function () {}, error: function () {} }; }\n"
        + "      };\n"
        + "    </script>\n";
}
