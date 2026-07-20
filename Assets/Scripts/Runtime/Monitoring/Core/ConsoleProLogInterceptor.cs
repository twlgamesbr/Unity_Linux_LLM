using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Global interceptor that hooks Application.logMessageReceivedThreaded
    /// to route ALL NPC-prefixed Debug.Log calls through Console Pro's CPAPI protocol.
    ///
    /// Catches every stray Debug.Log from the entire codebase that uses
    /// [NPC...], [Supabase...], [Qdrant...], or known markers, and re-emits
    /// it with CPAPI commands so Console Pro routes it to the right filter.
    ///
    /// Falls back silently when Console Pro DLLs are absent — the CPAPI strings
    /// are harmless extra text in standard Debug.Log output.
    ///
    /// No assembly references to Console Pro required.
    /// </summary>
    public static class ConsoleProLogInterceptor
    {
        static readonly HashSet<string> _interceptedPrefixes = new HashSet<string>
        {
            "[NPC", "[TelemetryRouter]", "[TelemetryBootstrapper]", "[ConsoleSink]",
            "[Supabase", "[WebGLDiagnostics]", "[Qdrant",
            "[CreateGenericItems]", "[ConsoleProIntegration]", "[SettingsGuard]",
        };

        static readonly Regex _categoryPattern = new Regex(
            @"\[(NPC|Supabase|Qdrant|WebGL|Settings|ConsolePro|Telemetry|Watch|ConsoleSink)[^]]*\]",
            RegexOptions.Compiled
        );

        static bool _hooked;

        /// <summary>
        /// Install the log interceptor. Call once during initialization.
        /// </summary>
        public static void Initialize()
        {
            if (_hooked)
                return;
            _hooked = true;
            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        /// <summary>
        /// Uninstall. Call during shutdown.
        /// </summary>
        public static void Shutdown()
        {
            if (!_hooked)
                return;
            _hooked = false;
            Application.logMessageReceivedThreaded -= OnLogMessage;
        }

        static void OnLogMessage(string logString, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(logString))
                return;

            // Only intercept NPC-prefixed / known system logs
            if (!ShouldIntercept(logString))
                return;

            string category = InferCategory(logString);
            string severity = type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => "Error",
                LogType.Warning => "Warning",
                _ => "Info"
            };

            // Add #NPC# #category# temp filter tags — Console Pro auto-creates filter buttons
            string augmented = $"#NPC# #{category}# {logString}";

            // Re-emit with CPAPI command so Console Pro picks it up
            if (severity is "Error" or "Warning")
            {
                // Use LogType command so it gets the right color in Console Pro
                Debug.Log($"{augmented}\nCPAPI:{{\"cmd\":\"LogType\",\"name\":\"{severity}\"}}");
            }
            else
            {
                // Use Filter command so it only shows in the category filter
                Debug.Log($"{augmented}\nCPAPI:{{\"cmd\":\"Filter\",\"name\":\"npc/{category}\"}}");
            }
        }

        static bool ShouldIntercept(string logString)
        {
            foreach (var prefix in _interceptedPrefixes)
            {
                if (logString.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static string InferCategory(string logString)
        {
            var match = _categoryPattern.Match(logString);
            if (match.Success)
            {
                string tag = match.Groups[1].Value.ToLowerInvariant();
                return tag switch
                {
                    "npc" => InferNpcSubCategory(logString),
                    "supabase" => "auth",
                    "qdrant" => "rag",
                    "webgl" => "network",
                    "settings" => "system",
                    "consolepro" => "system",
                    "telemetry" => "system",
                    "watch" => "system",
                    "consolesink" => "system",
                    _ => "system"
                };
            }
            return "system";
        }

        static string InferNpcSubCategory(string logString)
        {
            if (logString.Contains("rag", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("retriev", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("qdrant", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("embed", StringComparison.OrdinalIgnoreCase))
                return "rag";

            if (logString.Contains("llm", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("localai", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("complet", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("chat", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("token", StringComparison.OrdinalIgnoreCase))
                return "llm";

            if (logString.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("login", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("supabase", StringComparison.OrdinalIgnoreCase))
                return "auth";

            if (logString.Contains("network", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("transport", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("websocket", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("rpc", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("netcode", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("ping", StringComparison.OrdinalIgnoreCase))
                return "network";

            if (logString.Contains("item", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("trade", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("catalog", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("invent", StringComparison.OrdinalIgnoreCase))
                return "items";

            if (logString.Contains("dialogue", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("dialog", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("conversation", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("message", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("response", StringComparison.OrdinalIgnoreCase)
                || logString.Contains("session", StringComparison.OrdinalIgnoreCase))
                return "dialog";

            return "system";
        }
    }
}
