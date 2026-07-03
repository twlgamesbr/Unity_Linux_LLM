using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCStartupAuthorityStaticTests
    {
        static readonly Regex BlockComments = new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
        static readonly Regex LineComments = new Regex(@"^\s*//.*$", RegexOptions.Multiline | RegexOptions.Compiled);
        static readonly Regex DirectStartCall = new Regex(@"\.\s*(StartHost|StartClient|StartServer)\s*\(", RegexOptions.Compiled);

        [Test]
        public void OnlyNPCNetworkBootstrapMayDirectlyStartNetworkManager()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "Scripts", "Runtime");
            string[] files = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            var offenders = new List<string>();

            foreach (string file in files)
            {
                if (file.EndsWith("NPCNetworkBootstrap.cs"))
                {
                    continue;
                }

                string source = File.ReadAllText(file);
                string stripped = StripComments(source);
                if (DirectStartCall.IsMatch(stripped))
                {
                    offenders.Add(ToAssetPath(file));
                }
            }

            Assert.That(offenders, Is.Empty,
                "Only NPCNetworkBootstrap may directly call NetworkManager.StartHost/StartClient/StartServer. " +
                "Other components must set intent and delegate to NPCNetworkBootstrap.StartConfiguredMode().");
        }

        static string StripComments(string source)
        {
            return LineComments.Replace(BlockComments.Replace(source, string.Empty), string.Empty);
        }

        static string ToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            return normalized.StartsWith(dataPath) ? "Assets" + normalized.Substring(dataPath.Length) : normalized;
        }
    }
}
