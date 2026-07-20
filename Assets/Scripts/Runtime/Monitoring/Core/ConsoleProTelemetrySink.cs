using System.Text;
using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Telemetry sink that routes events through Console Pro's structured logging
    /// using the CPAPI magic-string protocol. This works from any assembly without
    /// referencing Console Pro DLLs — Console Pro reads the CPAPI commands embedded
    /// in Debug.Log output.
    ///
    /// Features:
    ///   - #NPC# #category# temp filter tags → Console Pro auto-creates filter buttons
    ///   - CPAPI Filter command → routes to named filter (npc/dialog, npc/llm, etc.)
    ///   - CPAPI LogType command → severity-typed logs (Error=red, Warning=yellow)
    ///   - Zero setup — works whether Console Pro is installed or not
    /// </summary>
    public class ConsoleProTelemetrySink : ITelemetrySink
    {
        public string DisplayName => "ConsolePro Telemetry Sink";

        readonly string _filterPrefix;

        public ConsoleProTelemetrySink(string filterPrefix = "NPC")
        {
            _filterPrefix = filterPrefix;
        }

        public void Emit(in TelemetryEvent evt)
        {
            string category = string.IsNullOrWhiteSpace(evt.Category) ? "system" : evt.Category.ToLowerInvariant();

            string filterName = $"{_filterPrefix.ToLowerInvariant()}/{category}";
            string logMessage = BuildLogMessage(evt, category);

            // Temp filter tags — Console Pro auto-creates colored filter buttons
            string taggedMessage = $"#NPC# #{category}# {logMessage}";

            // Determine severity from Status string
            string status = evt.Status?.ToLowerInvariant() ?? "";
            bool isError = status is "error" or "fallback";
            bool isWarning = status is "warning";

            if (isError)
            {
                // CPAPI LogType:Exception — appears in Error filter as red
                Debug.LogError(taggedMessage + "\nCPAPI:{\"cmd\":\"LogType\",\"name\":\"Error\"}");
            }
            else if (isWarning)
            {
                // CPAPI LogType:Warning — appears in Warning filter as yellow
                Debug.LogWarning(taggedMessage + "\nCPAPI:{\"cmd\":\"LogType\",\"name\":\"Warning\"}");
            }
            else
            {
                // CPAPI Filter:dialog — appears only in the specific filter
                Debug.Log($"{taggedMessage}\nCPAPI:{{\"cmd\":\"Filter\",\"name\":\"{filterName}\"}}");
            }
        }

        static string BuildLogMessage(TelemetryEvent evt, string category)
        {
            var sb = new StringBuilder();

            // Source
            if (!string.IsNullOrWhiteSpace(evt.Source))
                sb.Append($"[{evt.Source}] ");

            // Message
            if (!string.IsNullOrWhiteSpace(evt.Message))
                sb.Append(evt.Message);

            // Duration
            if (evt.DurationMs > 0)
                sb.Append($" ({evt.DurationMs}ms)");

            // Request ID (trimmed)
            if (!string.IsNullOrWhiteSpace(evt.RequestId))
                sb.Append($" #{evt.RequestId}");

            // Tags
            if (evt.Tags != null && evt.Tags.Count > 0)
            {
                sb.Append(" {");
                int i = 0;
                foreach (var kvp in evt.Tags)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append($"{kvp.Key}={kvp.Value}");
                    i++;
                }
                sb.Append("}");
            }

            return sb.ToString();
        }
    }
}
