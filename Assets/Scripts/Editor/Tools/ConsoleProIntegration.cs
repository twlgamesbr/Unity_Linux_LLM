using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace NPCSystem.Editor.Tools
{
    /// <summary>
    /// Console Pro integration manager.
    ///
    /// - Detects Console Pro installation and validates correct DLL loading
    /// - Manages the FLYINGWORM_CONSOLE_3 scripting define for conditional compilation
    /// - Provides menu items for status checks and setup
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleProIntegration
    {
        const string DefineSymbol = "FLYINGWORM_CONSOLE_3";
        const string ConsoleProAssetFolder = "Assets/ConsolePro";

        static ConsoleProIntegration()
        {
            EditorApplication.delayCall += OnDelayCall;
        }

        /// <summary>Void wrapper for delayCall (EnsureScriptingDefine returns bool).</summary>
        static void OnDelayCall()
        {
            EnsureScriptingDefine();
        }

        // ── Menu items ──────────────────────────────────────────────────────

        [MenuItem("Tools/Console Pro/Check Installation", false, 200)]
        static void CheckInstallation()
        {
            var report = VerifyInstallation();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Console Pro Installation", report, "OK");
        }

        [MenuItem("Tools/Console Pro/Reapply Scripting Define", false, 201)]
        static void ReapplyDefine()
        {
            var applied = EnsureScriptingDefine();
            if (applied)
                Debug.Log($"[ConsoleProIntegration] '{DefineSymbol}' scripting define applied.");
            else
                Debug.Log($"[ConsoleProIntegration] '{DefineSymbol}' already present.");
        }

        [MenuItem("Tools/Console Pro/Documentation", false, 202)]
        static void OpenDocs()
        {
            var docsPath = $"{ConsoleProAssetFolder}/Editor Console Pro Documentation.pdf";
            if (File.Exists(docsPath))
                Application.OpenURL($"file://{Path.GetFullPath(docsPath)}");
            else
                Debug.LogWarning("[ConsoleProIntegration] Documentation PDF not found. Check Console Pro installation.");
        }

        // ── Core ────────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures FLYINGWORM_CONSOLE_3 is defined in scripting symbols
        /// when Console Pro DLLs are detected. Returns true if the define was applied.
        /// </summary>
        public static bool EnsureScriptingDefine()
        {
            if (!ConsoleProDLLsExist())
                return false;

            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (defines.Contains(DefineSymbol))
                return false;

            var updated = defines.TrimEnd(';', ' ');
            updated = string.IsNullOrEmpty(updated) ? DefineSymbol : $"{updated};{DefineSymbol}";
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, updated);
            return true;
        }

        /// <summary>
        /// Check if Console Pro DLL files exist in the project.
        /// </summary>
        static bool ConsoleProDLLsExist()
        {
            var dllGuids = AssetDatabase.FindAssets("ConsolePro.Editor");
            return dllGuids.Any(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return path != null && path.EndsWith(".dll") && path.Contains("ConsolePro");
            });
        }

        /// <summary>
        /// Full installation report.
        /// </summary>
        public static string VerifyInstallation()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("═══ Console Pro Installation Report ═══");

            // Check asset folder
            if (AssetDatabase.IsValidFolder(ConsoleProAssetFolder))
            {
                report.AppendLine("✅ Console Pro folder found");
            }
            else
            {
                report.AppendLine("❌ Console Pro folder NOT found at Assets/ConsolePro");
                return report.ToString();
            }

            // Check DLLs
            var dllGuids = AssetDatabase.FindAssets("ConsolePro.Editor");
            var dllPaths = dllGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(p => p.EndsWith(".dll"))
                .ToArray();

            if (dllPaths.Length == 0)
            {
                report.AppendLine("❌ No ConsolePro.Editor.dll files found");
                return report.ToString();
            }

            report.AppendLine($"📁 Found {dllPaths.Length} DLL(s):");
            foreach (var dll in dllPaths)
            {
                var importer = AssetImporter.GetAtPath(dll) as PluginImporter;
                var isEnabled = importer != null && importer.GetCompatibleWithEditor();
                report.AppendLine($"   {(isEnabled ? "✅" : "⏹")} {dll}");
            }

            // Check scripting define
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (defines.Contains(DefineSymbol))
                report.AppendLine($"✅ '{DefineSymbol}' scripting define present");
            else
                report.AppendLine($"⚠ '{DefineSymbol}' scripting define MISSING — run Reapply Scripting Define");

            // Check runtime helper
            var debugScript = AssetDatabase.FindAssets("ConsoleProDebug")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .FirstOrDefault(p => p.EndsWith(".cs"));

            report.AppendLine(debugScript != null
                ? "✅ ConsoleProDebug.cs found"
                : "⚠ ConsoleProDebug.cs not found (runtime filtering won't work)");

            // Check version-specific DLL
#if UNITY_6000_5_OR_NEWER
            var has6005Dll = dllPaths.Any(p => p.Contains("6000_5_Plus"));
            report.AppendLine(has6005Dll
                ? "✅ Unity 6000.5+ DLL available"
                : "⚠ No Unity 6000.5+ specific DLL — ensure Console Pro is updated");
#elif UNITY_6000_3_OR_NEWER
            var has6003Dll = dllPaths.Any(p => p.Contains("6000_3_Plus"));
            report.AppendLine(has6003Dll
                ? "✅ Unity 6000.3+ DLL available"
                : "⚠ No Unity 6000.3+ specific DLL — ensure Console Pro is updated");
#endif

            report.AppendLine("═════════════════════════════════════════");
            return report.ToString();
        }
    }
}
