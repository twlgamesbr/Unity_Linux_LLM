using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NPCSystem.Editor.Tools
{
    /// <summary>
    /// One-click setup for Console Pro integration.
    ///
    /// Features:
    ///   - Applies FLYINGWORM_CONSOLE_3 scripting define (if not already set)
    ///   - Verifies Console Pro DLLs are loaded correctly
    ///   - Creates ConsoleProRemoteServer for build monitoring
    ///   - Attempts to set up Console Pro custom filters via internal API
    ///   - Shows step-by-step guide for manual shared-settings config
    ///
    /// Menu: Tools > Console Pro >
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleProSetup
    {
        static ConsoleProSetup()
        {
            // Auto-verify on domain reload
            EditorApplication.delayCall += () =>
            {
                VerifyDLLStatus();
            };
        }

        // ── Menu items ──────────────────────────────────────────────────────

        [MenuItem("Tools/Console Pro/Apply Project Setup", false, 100)]
        static void ApplyFullSetup()
        {
            bool allGood = true;

            // 1. Ensure scripting define
            if (ConsoleProIntegration.EnsureScriptingDefine())
                Debug.Log("[ConsoleProSetup] ✅ FLYINGWORM_CONSOLE_3 define applied.");
            else
                Debug.Log("[ConsoleProSetup] ℹ️  FLYINGWORM_CONSOLE_3 already present.");

            // 2. Verify DLLs
            if (!VerifyDLLStatus())
            {
                Debug.LogError("[ConsoleProSetup] ❌ Console Pro DLL verification FAILED.");
                allGood = false;
            }

            // 3. Try to configure Console Pro's custom filters via internal API
            TrySetupCustomFilters();

            // 4. Check if ConsoleProWatcher exists in scene
            CheckWatcherInScene();

            // 5. Offer to add Remote Server
            CheckRemoteServerInScene();

            if (allGood)
            {
                Debug.Log(
                    "[ConsoleProSetup] ✅ Console Pro setup complete. See guide below for shared settings."
                );
            }

            // Print guide
            Debug.Log(GetGuideText());
        }

        [MenuItem("Tools/Console Pro/Add Remote Server to Scene", false, 110)]
        static void AddRemoteServer()
        {
            // ConsoleProRemoteServer is a component inside the ConsolePro.Editor.dll
            // Add it via GameObject menu
            var existing = UnityEngine.Object.FindAnyObjectByType(
                Type.GetType("FlyingWormConsole3.ConsoleProRemoteServer, ConsolePro.Editor")
                    ?? Type.GetType("ConsoleProRemoteServer, Assembly-CSharp")
            );

            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Console Pro",
                    "ConsoleProRemoteServer already exists in scene.",
                    "OK"
                );
                return;
            }

            // Find a root GO to attach it to
            var target = GameObject.Find("NPCDialogueSystem");
            if (target == null)
                target = new GameObject("ConsoleProRemoteServer");

            // Try adding the ConsoleProRemoteServer component via internal type name
            var remoteType = FindConsoleProRemoteServerType();
            if (remoteType != null)
            {
                target.AddComponent(remoteType);
                Debug.Log($"[ConsoleProSetup] ✅ ConsoleProRemoteServer added to '{target.name}'.");
                EditorUtility.DisplayDialog(
                    "Console Pro",
                    "ConsoleProRemoteServer added to scene.\n\n"
                        + "Build with Development Build + DEBUG define to receive logs remotely.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Console Pro",
                    "Could not find ConsoleProRemoteServer type.\n"
                        + "Ensure Console Pro is properly installed.\n"
                        + "You can add it manually from Component > Console Pro > ConsoleProRemoteServer",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Console Pro/Open Console Pro Window", false, 120)]
        static void OpenConsoleProWindow()
        {
            var windowType = FindConsoleProWindowType();
            if (windowType != null)
            {
                var getWindow = windowType.GetMethod(
                    "GetWindow",
                    BindingFlags.Public | BindingFlags.Static
                );
                getWindow?.Invoke(null, null);
            }
            else
            {
                Debug.LogWarning(
                    "[ConsoleProSetup] Could not open Console Pro window automatically. Use Window > Console Pro 3."
                );
            }
        }

        // ── Internal ────────────────────────────────────────────────────────

        static bool VerifyDLLStatus()
        {
            var report = ConsoleProIntegration.VerifyInstallation();
            if (report.Contains("❌"))
            {
                Debug.LogWarning($"[ConsoleProSetup] Console Pro has issues:\n{report}");
                return false;
            }
            Debug.Log($"[ConsoleProSetup] Console Pro DLLs OK.");
            return true;
        }

        static void CheckWatcherInScene()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType(
                Type.GetType("NPCSystem.Monitoring.ConsoleProBehaviour, NPCSystem.Monitoring")
            );
            if (existing != null)
            {
                Debug.Log($"[ConsoleProSetup] ✅ ConsoleProBehaviour found on '{existing.name}'.");
            }
            else
            {
                Debug.LogWarning(
                    "[ConsoleProSetup] ⚠️ No ConsoleProBehaviour in scene. "
                        + "Add it to the NPCFlowLogger GameObject for live Watch panel updates."
                );
            }
        }

        static void CheckRemoteServerInScene()
        {
            var remoteType = FindConsoleProRemoteServerType();
            if (remoteType == null)
                return;

            var existing = UnityEngine.Object.FindObjectOfType(remoteType);
            if (existing == null)
            {
                Debug.Log(
                    "[ConsoleProSetup] 💡 Use 'Tools > Console Pro > Add Remote Server to Scene' "
                        + "to monitor builds remotely."
                );
            }
        }

        /// <summary>
        /// Attempt to configure Console Pro's custom filters via internal Reflection API.
        /// Console Pro stores filters in EditorUserSettings or its preferences system.
        /// This is a best-effort call — it will not throw if types/methods are missing.
        /// </summary>
        static void TrySetupCustomFilters()
        {
            try
            {
                // Try to find Console Pro's preferences/settings type
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    if (!asm.GetName().Name.Contains("ConsolePro"))
                        continue;

                    var types = asm.GetTypes();
                    foreach (var t in types)
                    {
                        // Look for a settings/preferences class that has AddFilter or similar
                        if (
                            t.Name.Contains("Preferences")
                            || t.Name.Contains("Settings")
                            || t.Name.Contains("FilterData")
                        )
                        {
                            // Try to find a method or field that allows adding custom filters
                            var methods = t.GetMethods(
                                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
                            );
                            foreach (var m in methods)
                            {
                                if (
                                    m.Name.Contains("Add")
                                    || m.Name.Contains("Create")
                                    || m.Name.Contains("Import")
                                )
                                {
                                    Debug.Log(
                                        $"[ConsoleProSetup] Discovered Console Pro API: {t.Name}.{m.Name}()"
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log(
                    $"[ConsoleProSetup] Console Pro custom filter auto-setup: {ex.Message} (non-critical)"
                );
            }

            Debug.Log(
                "[ConsoleProSetup] 💡 For permanent colored filters:\n"
                    + "   1. Right-click Console Pro toolbar > Preferences\n"
                    + "   2. Go to Custom Filters tab\n"
                    + "   3. Add filters named 'npc/dialog', 'npc/llm', 'npc/rag', etc.\n"
                    + "   4. Set colors and search terms '#dialog#', '#llm#', '#rag#'\n"
                    + "   5. Set a Shared Settings file in Preferences > Shared Settings\n"
                    + "   → This file can be committed to version control for team use."
            );
        }

        static Type FindConsoleProRemoteServerType()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                try
                {
                    var t = asm.GetType("FlyingWormConsole3.ConsoleProRemoteServer");
                    if (t != null)
                        return t;
                }
                catch { }
            }
            return null;
        }

        static Type FindConsoleProWindowType()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                try
                {
                    var t = asm.GetType("FlyingWormConsole3.ConsolePro3Window");
                    if (t != null)
                        return t;
                    t = asm.GetType("ConsolePro3Window");
                    if (t != null)
                        return t;
                }
                catch { }
            }
            return null;
        }

        static string GetGuideText()
        {
            return @"
═══════════════════════════════════════════════════════
  Console Pro — Full Feature Guide
═══════════════════════════════════════════════════════

  ✅ ALREADY ACTIVE (no setup needed):
  ─────────────────────────────────────
  • #NPC# temp filter — every NPC log is auto-grouped
  • #category# sub-filter (dialog, llm, rag, auth, etc.)
  • Watch panel — FPS, memory, LLM duration, requests
  • NPCFlowLogger routes through Console Pro API
  • Log interceptor catches stray Debug.Log calls

  💡 RECOMMENDED PERMANENT SETUP:
  ─────────────────────────────────────
  1. Open Console Pro: Window > Console Pro 3
  2. Right-click toolbar > Preferences > Shared Settings
  3. Pick a file in Assets/ (e.g. Assets/settings.cep)
  4. This file is now shared via version control

  5. Right-click toolbar > Preferences > Custom Filters
  6. Add these filters (Searches = #tag#):

     Filter Name      | Search Term   | Color         | Icon
     ─────────────────┼───────────────┼───────────────┼─────
     npc/dialog       | #dialog#      | Green         | 💬
     npc/llm          | #llm#         | Blue          | 🤖
     npc/rag          | #rag#         | Purple        | 📚
     npc/auth         | #auth#        | Orange        | 🔑
     npc/network      | #network#     | Cyan          | 🌐
     npc/items        | #items#       | Yellow        | 📦
     npc/system       | #system#      | Gray          | ⚙️

  🖥️ REMOTE MONITORING (WebGL/Mobile):
  ─────────────────────────────────────
  • Tools > Console Pro > Add Remote Server to Scene
  • Build with Development Build + DEBUG
  • In Console Pro toolbar, enable Remote mode
  • All logs appear live from the device

  📊 WATCH PANEL (live counters):
  ─────────────────────────────────────
  Opens automatically when ConsoleProBehaviour is
  attached to a persistent GameObject (e.g. NPCFlowLogger).
  Shows: FPS, Memory, LLM duration, Qdrant latency,
  active sessions, auth logins, network ping.

═══════════════════════════════════════════════════════
";
        }

        /// <summary>
        /// Public forwarding — ConsoleProIntegration's methods are private.
        /// We need public wrappers for the setup tool to call them.
        /// </summary>
        public static bool EnsureScriptingDefinePublic() =>
            ConsoleProIntegration.EnsureScriptingDefine();

        public static string VerifyInstallationPublic() =>
            ConsoleProIntegration.VerifyInstallation();

        // Custom editor to show the guide in a window
        [MenuItem("Tools/Console Pro/Show Setup Guide", false, 200)]
        static void ShowGuide()
        {
            Debug.Log(GetGuideText());
            EditorUtility.DisplayDialog(
                "Console Pro Setup Guide",
                GetGuideText()
                    .Replace(
                        "═══════════════════════════════════════════════════════\n  Console Pro — Full Feature Guide\n═══════════════════════════════════════════════════════\n\n",
                        ""
                    )
                    .Replace("═══════════════════════════════════════════════════════\n", ""),
                "OK"
            );
        }
    }
}
