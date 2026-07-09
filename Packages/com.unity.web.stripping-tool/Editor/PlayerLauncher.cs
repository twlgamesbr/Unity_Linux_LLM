using System;
using System.Reflection;
using UnityEditor;

namespace Unity.Web.Stripping.Editor
{
    // NOTE: requires Web Build Support module for SimpleWebServer. Functions that access SimpleWebServer
    // throw NotSupportedException if called when Web Build Support module is not present.
    class PlayerLauncher
    {
        // NOTE: UnityEditor.WebGL.Browser is IDisposable but we don't take that into consideration right now;
        // disposability is used to close the browser process but we have no need for it.
        internal object m_Browser;
        internal static MethodInfo s_CreateMethod;
        internal static MethodInfo s_LaunchMethod;
        internal static MethodInfo s_QuitMethod;

        public string BuildPath { get; private set; }
        public string ClientUrl { get; private set; }

        public bool IsServing =>
            SimpleWebServerHelper.s_IsRunning && !string.IsNullOrEmpty(BuildPath) && !string.IsNullOrEmpty(ClientUrl);

        public PlayerLauncher()
        {
            if (!BuildToolsLocator.IsWebBuildSupportInstalled)
                return;
            if (s_CreateMethod != null && s_LaunchMethod != null && s_QuitMethod != null)
                return;

            const string assemblyName = "UnityEditor.WebGL.Extensions";
            const string className = "UnityEditor.WebGL.Browser";
            var assembly = Assembly.Load(assemblyName)
                ?? throw new TargetException($"Type '{assemblyName}' not found in assembly.");
            var classType = assembly.GetType(className)
                ?? throw new TargetException($"Type '{className}' not found in assembly '{assemblyName}'.");
            s_CreateMethod = GetMethod(classType, "Create", BindingFlags.Static | BindingFlags.Public);
            s_LaunchMethod = GetMethod(classType, "Launch");
            s_QuitMethod = GetMethod(classType, "Quit");
        }

        public void OpenInPreferredBrowser(string query = "")
        {
            if (string.IsNullOrEmpty(ClientUrl))
                return;

            // if configurable browser is not worth of the reflection shenanigans, could simply do
            //Application.OpenURL(ClientUrl);


            // Append search params to client url
            var url = ClientUrl;
            if (!string.IsNullOrEmpty(query))
            {
                // Workaround: We need to manually add "" around the url
                // because of a bug in the Unity Browser class.
                url = $"\"{url}?{query}\"";
            }

            m_Browser = s_CreateMethod.Invoke(
                null,
                new object[]
                {
                    0, // UnityEditor.WebGLClientPlatform.Desktop
                    EditorUserBuildSettings.webGLClientBrowserType,
                    EditorUserBuildSettings.webGLClientBrowserPath,
                    url,
                    null // WebProxy
                }
            );
            s_LaunchMethod.Invoke(m_Browser, null);
        }

        public string ServeBuild(string buildPath)
        {
            string clientHostname = "localhost";
            string acceptedHostname = "localhost";
            // NOTE: Uncomment to enable sharing in local network
            // Use the LAN IP address for platforms running on machines separate from host
            //if (EditorUserBuildSettings.webGLClientPlatform != WebGLClientPlatform.Desktop)
            //{
            //    clientHostname = SimpleWebServer.GetLocalIPAddress();
            //    acceptedHostname = "+";
            //}

            StopServing();

            BuildPath = buildPath;
            int port = SimpleWebServerHelper.GetUnusedPort();
            SimpleWebServerHelper.Start(BuildPath, $"http://{acceptedHostname}:{port}/");
            ClientUrl = $"http://{clientHostname}:{port}/";
            return ClientUrl;
        }

        public void StopServing()
        {
            if (SimpleWebServerHelper.s_IsRunning)
                SimpleWebServerHelper.Stop();
            QuitBrowser();
            BuildPath = string.Empty;
            ClientUrl = string.Empty;
        }

        void QuitBrowser()
        {
            if (m_Browser == null)
                return;

            s_QuitMethod.Invoke(m_Browser, null);
            m_Browser = null;
        }

        static MethodInfo GetMethod(Type classType, string methodName, BindingFlags? bindingFlags = null)
        {
            var methodInfo = (bindingFlags.HasValue)
                ? classType.GetMethod(methodName, bindingFlags.Value)
                : classType.GetMethod(methodName);

            if (methodInfo == null)
                throw new TargetException($"Method '{methodName}' not found in '{classType.FullName}'.");

            return methodInfo;
        }
    }
}
