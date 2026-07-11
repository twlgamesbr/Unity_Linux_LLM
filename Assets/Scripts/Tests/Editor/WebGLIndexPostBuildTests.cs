using System;
using System.Reflection;
using NUnit.Framework;

namespace NPCSystem.Tests
{
    public class WebGLIndexPostBuildTests
    {
        [Test]
        public void SanitizeDatadogBrowserAgent_RemovesBrowserAgentScriptsAndAddsNoopStubs()
        {
            const string html =
                "<html><head>"
                + "<script async src=\"https://www.datadoghq-browser-agent.com/us1/v6/datadog-rum-v4.js\"></script>"
                + "<script>window.DD_RUM.onReady(function(){window.DD_RUM.init({clientToken:'secret'});});"
                + "document.write('https://www.datadoghq-browser-agent.com/us1/v6/datadog-logs-v4.js');</script>"
                + "</head><body><canvas id=\"unity-canvas\"></canvas></body></html>";

            string sanitized = Sanitize(html);

            Assert.That(sanitized, Does.Not.Contain("datadoghq-browser-agent.com"));
            Assert.That(sanitized, Does.Not.Contain("clientToken"));
            Assert.That(sanitized, Does.Contain("webgl-datadog-noop-stub"));
            Assert.That(sanitized, Does.Contain("window.DD_RUM"));
            Assert.That(sanitized, Does.Contain("window.DD_LOGS"));
        }

        [Test]
        public void SanitizeDatadogBrowserAgent_WhenNoBrowserAgent_ReturnsOriginalHtml()
        {
            const string html = "<html><head></head><body><canvas id=\"unity-canvas\"></canvas></body></html>";

            string sanitized = Sanitize(html);

            Assert.That(sanitized, Is.EqualTo(html));
        }

        static string Sanitize(string html)
        {
            Type type = Type.GetType("WebGLIndexPostBuild, Assembly-CSharp-Editor");
            Assert.That(type, Is.Not.Null);

            MethodInfo method = type.GetMethod(
                "SanitizeDatadogBrowserAgent",
                BindingFlags.Public | BindingFlags.Static
            );
            Assert.That(method, Is.Not.Null);

            return (string)method.Invoke(null, new object[] { html });
        }
    }
}
