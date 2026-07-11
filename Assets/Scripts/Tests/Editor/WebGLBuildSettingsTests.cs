using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace NPCSystem.Tests
{
    public class WebGLBuildSettingsTests
    {
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        const string DesktopProfilePath = "Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset";
        const string MobileProfilePath = "Assets/Settings/Build Profiles/WebGL - Mobile - Development.asset";

        static readonly string[] WebGLSettingsFiles =
        {
            ProjectSettingsPath,
            DesktopProfilePath,
            MobileProfilePath,
        };

        [Test]
        public void WebGLBuildSettings_KeepBrowserMemoryHeadroomEnabled()
        {
            foreach (string path in WebGLSettingsFiles)
            {
                string contents = ReadProjectFile(path);

                AssertSetting(contents, "webGLMaximumMemorySize", "4096", path);
                AssertSetting(contents, "webGLDataCaching", "1", path);
                AssertSetting(contents, "webGLNameFilesAsHashes", "1", path);
                AssertSetting(contents, "webGLAnalyzeBuildSize", "1", path);
            }
        }

        static string ReadProjectFile(string path)
        {
            string absolutePath = Path.GetFullPath(path);
            Assert.That(File.Exists(absolutePath), Is.True, $"Missing expected project file: {path}");
            return File.ReadAllText(absolutePath);
        }

        static void AssertSetting(string contents, string key, string expectedValue, string path)
        {
            string directSetting = $"{key}: {expectedValue}";
            string buildProfileSetting = $"|   {key}: {expectedValue}";

            Assert.That(
                contents.Contains(directSetting) || contents.Contains(buildProfileSetting),
                Is.True,
                $"{path} must keep {key} at {expectedValue} for WebGL browser startup stability."
            );
        }
    }
}
