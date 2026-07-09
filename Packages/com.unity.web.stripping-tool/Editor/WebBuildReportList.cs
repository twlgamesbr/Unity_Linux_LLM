using System;
using System.IO;
using System.Collections.Generic;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Manage all web builds. The list of builds is automatically updated when a build is completed.
    /// The build list is saved to the UserSettings folder in the project's root directory.
    /// </summary>
    public class WebBuildReportList
    {
        // Singleton pattern setup
        private static readonly WebBuildReportList m_Instance = new WebBuildReportList();

        WebBuildReportList()
        {
            LoadBuildList();
        }

        /// <summary>
        /// Get instance of `WebBuildReportList` (Singleton pattern).
        /// </summary>
        public static WebBuildReportList Instance => m_Instance;

        internal const string k_BuildListKey = "WebBuildReportList.Builds";
        List<WebBuildReport> m_Builds = new();

        /// <summary>
        /// A list of all web build reports.
        /// </summary>
        public List<WebBuildReport> Builds => m_Builds;

        /// <summary>
        /// An event that is triggered when the web build report list is updated.
        /// </summary>
        public event Action<List<WebBuildReport>> BuildsUpdated;

        /// <summary>
        /// Find the web build report for the given path in the build list.
        /// If the path is a relative path, it will be automatically converted to an absolute path.
        /// </summary>
        /// <param name="path">Path to a web build.</param>
        /// <returns>A web build report or null if build is not in the list.</returns>
        public WebBuildReport GetBuild(string path)
        {
            if (!Path.IsPathFullyQualified(path))
                path = Path.GetFullPath(path).Replace("\\", "/");

            return m_Builds.Find(b => b.OutputPath == path);
        }

        /// <summary>
        /// Add a build at the given path to the list of builds or update the existing
        /// build report if the build is already in the list.
        /// </summary>
        /// <param name="path">Path to a web build.</param>
        /// <returns>A web build report object</returns>
        public WebBuildReport AddOrUpdateBuild(string path)
        {
            var build = GetBuild(path);
            if (build == null)
            {
                build = WebBuildReport.CreateFromPath(path);
                m_Builds.Add(build);
            }
            else
            {
                build.Update();
            }

            BuildsUpdated?.Invoke(m_Builds);
            SaveBuildList();

            return build;
        }

        /// <summary>
        /// Update the build at the given path.
        /// </summary>
        /// <param name="path">Path to a web build.</param>
        /// <returns>A web build report object or null if the build is not in the build list.</returns>
        public WebBuildReport UpdateBuild(string path)
        {
            var build = GetBuild(path);
            if (build == null)
                return null;

            build.Update();
            BuildsUpdated?.Invoke(m_Builds);
            SaveBuildList();

            return build;
        }

        /// <summary>
        /// Update the given build report object and serialize build list.
        /// This will also trigger the BuildsUpdated callbacks.
        /// </summary>
        /// <param name="build">A web build report object.</param>
        public void UpdateBuild(WebBuildReport build)
        {
            build.Update();
            BuildsUpdated?.Invoke(m_Builds);
            SaveBuildList();
        }

        /// <summary>
        /// Removes a build from the build list based on the output path.
        /// </summary>
        /// <param name="build">The web build report to remove from the list.</param>
        /// <returns>Returns 'true' if the build was removed, 'false' otherwise.</returns>
        public bool RemoveBuild(WebBuildReport build)
        {
            var resolvedBuild = !string.IsNullOrEmpty(build?.OutputPath) ? GetBuild(build.OutputPath) : null;
            var successful = m_Builds.Remove(resolvedBuild);
            if (successful)
            {
                BuildsUpdated?.Invoke(m_Builds);
                SaveBuildList();
            }

            return successful;
        }

        /// <summary>
        /// Removes all builds from the build list.
        /// </summary>
        public void ClearBuilds()
        {
            m_Builds.Clear();
            BuildsUpdated?.Invoke(m_Builds);
            SaveBuildList();
        }

        /// <summary>
        /// Updates the information of all builds to match the contents of their build directories.
        /// </summary>
        /// <remarks>
        /// Doesn't trigger `BuildsUpdated` as the list itself doesn't change.
        /// </remarks>
        public void Update()
        {
            m_Builds.ForEach(b => b.Update());
        }

        internal void LoadBuildList()
        {
            // Migrate build list stored in ProjectSettings in version < 1.0.0-final, this logic can be removed at some point
            var buildsFromProjectSettings = PackageSettings.GetProjectSetting(k_BuildListKey, new List<WebBuildReport>());
            if (buildsFromProjectSettings.Count > 0)
            {
                m_Builds = buildsFromProjectSettings;
                PackageSettings.DeleteProjectSetting<List<WebBuildReport>>(k_BuildListKey);
                SaveBuildList();
                return;
            }

            m_Builds = PackageSettings.GetUserSetting(k_BuildListKey, buildsFromProjectSettings);
        }

        void SaveBuildList()
        {
            PackageSettings.SetUserSetting(k_BuildListKey, m_Builds);
            PackageSettings.Save();
        }
    }
}
