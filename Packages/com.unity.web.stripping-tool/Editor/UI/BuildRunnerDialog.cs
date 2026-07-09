using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Web.Stripping.Editor
{
    class BuildRunnerDialog : EditorWindow
    {
        // Path inside "Documentation~" folder to the documentation page containing the docs of this asset, no file extension.
        internal const string k_DocumentationPage = "submodule-stripping-window-reference";

        PlayerLauncher m_Launcher;

        public void Run(string buildPath)
        {
            ServeBuild(buildPath);
            OpenBuildInPreferredBrowser();
        }

        public void ServeBuild(string buildPath)
        {
            m_Launcher?.ServeBuild(buildPath);
            UpdateText();
        }

        void OpenBuildInPreferredBrowser()
        {
            m_Launcher?.OpenInPreferredBrowser();
        }

        void UpdateText()
        {
            if (m_Launcher == null)
            {
                rootVisualElement.Q<Label>().text =
                    $"The {PackageConstants.PackageDisplayName} requires the Web Build Support module. Add the module with the Unity Hub.";
                return;
            }

            if (!m_Launcher.IsServing)
            {
                rootVisualElement.Q<Label>().text = "Build is not served.\nClose this window.";
                return;
            }

            string buildOutputPath = m_Launcher.BuildPath;
            string url = m_Launcher.ClientUrl;
            rootVisualElement.Q<Label>().text =
                $"Serving build '{buildOutputPath}' at <a href=\"{url}\">{url}</a> .\n\nClose this window to close the server.";
        }

        void OnEnable()
        {
            m_Launcher ??= BuildToolsLocator.IsWebBuildSupportInstalled ? new() : null;
            minSize = maxSize = new(400, 150);
            titleContent = new GUIContent("Serving Build");
        }

        void OnDisable()
        {
            m_Launcher?.StopServing();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            UIUtils.AddHelpButton(toolbar, k_DocumentationPage);
            root.Add(toolbar);

            var infoLabel = new Label();
            infoLabel.style.whiteSpace = WhiteSpace.Normal;
            infoLabel.enableRichText = true;
            root.Add(infoLabel);
            UpdateText();
        }
    }
}
