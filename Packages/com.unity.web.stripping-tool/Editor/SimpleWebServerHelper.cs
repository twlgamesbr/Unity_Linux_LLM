using System.Diagnostics.CodeAnalysis;

namespace Unity.Web.Stripping.Editor
{
    // Wrapper around Unity.Automation.Players.WebGL.SimpleWebServer.
    // Requires the active build target to be Web in order to function, throws NotSupportedException if not.
    // It's best to guard tests that use this class with "#if UNITY_WEBGL"
    static class SimpleWebServerHelper
    {
        [SuppressMessage("", "IDE1006", Justification = "Keeping the wrapper 1:1 with the target class")]
        public static bool s_IsRunning =>
#if UNITY_WEBGL
            Automation.Players.WebGL.SimpleWebServer.s_IsRunning;
#else
            throw new System.NotSupportedException("SimpleWebServer requires Web Build Support module.");
#endif

        public static void Start(string sourceDir, string url)
        {
#if UNITY_WEBGL
            Automation.Players.WebGL.SimpleWebServer.Start(sourceDir, url);
#else
            throw new System.NotSupportedException("SimpleWebServer requires Web Build Support module.");
#endif
        }

        public static void Stop()
        {
#if UNITY_WEBGL
            Automation.Players.WebGL.SimpleWebServer.Stop();
#else
            throw new System.NotSupportedException("SimpleWebServer requires Web Build Support module.");
#endif
        }

        public static string GetLocalIPAddress()
        {
#if UNITY_WEBGL
            return Automation.Players.WebGL.SimpleWebServer.GetLocalIPAddress();
#else
            throw new System.NotSupportedException("SimpleWebServer requires Web Build Support module.");
#endif
        }

        public static int GetUnusedPort()
        {
#if UNITY_WEBGL
            return Automation.Players.WebGL.SimpleWebServer.GetUnusedPort();
#else
            throw new System.NotSupportedException("SimpleWebServer requires Web Build Support module.");
#endif
        }
    }
}
