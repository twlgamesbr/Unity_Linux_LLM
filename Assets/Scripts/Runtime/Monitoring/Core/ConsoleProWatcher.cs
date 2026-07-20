using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Tracks live telemetry counters via Console Pro's Watch() CPAPI protocol.
    /// Updates the Watch panel each frame with real-time metrics such as
    /// LLM request duration, Qdrant query latency, active sessions, etc.
    ///
    /// Call UpdateWatches() from a MonoBehaviour's Update() to keep values fresh.
    /// Watch entries are grouped hierarchically in Console Pro's Watch panel.
    ///
    /// Uses CPAPI magic strings directly (no Console Pro assembly reference needed).
    /// </summary>
    public static class ConsoleProWatcher
    {
        // ── Named slots that appear in Console Pro's Watch panel ────────────
        // Grouped by category for the hierarchical Watch view (slashes = nesting).

        public static readonly Dictionary<string, string> Watches = new Dictionary<string, string>
        {
            // Performance
            { "Performance/FPS", "0" },
            { "Performance/Memory (MB)", "0" },
            { "Performance/Frame Time (ms)", "0" },
            { "Performance/GC Alloc (MB/frame)", "0" },

            // LLM
            { "LLM/Request Duration (ms)", "—" },
            { "LLM/Requests Total", "0" },
            { "LLM/Requests This Session", "0" },
            { "LLM/Last Token Count", "0" },
            { "LLM/Last Model", "—" },

            // Qdrant / RAG
            { "RAG/Query Duration (ms)", "—" },
            { "RAG/Queries Total", "0" },
            { "RAG/Results Per Query", "0" },
            { "RAG/Last Collection", "—" },

            // Dialogue
            { "Dialogue/Active Sessions", "0" },
            { "Dialogue/Messages Sent", "0" },
            { "Dialogue/Messages Received", "0" },
            { "Dialogue/Current Speaker", "—" },

            // Auth
            { "Auth/Logins Total", "0" },
            { "Auth/Active Users", "0" },
            { "Auth/Last Login", "—" },

            // Network
            { "Network/Ping (ms)", "—" },
            { "Network/Connected Clients", "0" },
            { "Network/RPCs Sent", "0" },
            { "Network/Transport Mode", "—" },

            // Items
            { "Items/Total Traded", "0" },
            { "Items/Catalog Size", "0" },
            { "Items/Last Trade", "—" },
        };

        // ── Runtime counters ────────────────────────────────────────────────
        static int _frameCount;

        // ── Public API ──────────────────────────────────────────────────────

        public static void SetWatch(string name, string value)
        {
            Watches[name] = value;
        }

        public static void SetWatch(string name, float value)
        {
            Watches[name] = value.ToString("F1");
        }

        public static void SetWatch(string name, int value)
        {
            Watches[name] = value.ToString();
        }

        /// <summary>
        /// Increment a numeric watch value by delta.
        /// </summary>
        public static void IncrementWatch(string name, int delta = 1)
        {
            if (Watches.TryGetValue(name, out var current)
                && int.TryParse(current, out var val))
            {
                Watches[name] = (val + delta).ToString();
            }
        }

        /// <summary>
        /// Push all tracked values to Console Pro Watch panel via CPAPI.
        /// Call this from Update() on a persistent MonoBehaviour.
        /// The CPAPI Watch command only produces one log entry per watch name
        /// regardless of how many times logged — Console Pro replaces the value.
        /// </summary>
        public static void UpdateWatches()
        {
            // Auto-update performance metrics every frame
            float dt = Time.deltaTime;
            _frameCount++;

            Watches["Performance/FPS"] = dt > 0.001f ? (1.0f / dt).ToString("F0") : "—";
            Watches["Performance/Frame Time (ms)"] = (dt * 1000f).ToString("F1");
            Watches["Performance/Memory (MB)"] = (Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L)).ToString();

            // GC allocation per frame (sampled every 30 frames)
            if (_frameCount % 30 == 0)
            {
                Watches["Performance/GC Alloc (MB/frame)"] =
                    (Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L)).ToString("F2");
            }

            // Push all watches to Console Pro via CPAPI Watch protocol
            foreach (var kvp in Watches)
            {
                // CPAPI Watch: name : value with \nCPAPI:{"cmd":"Watch","name":"Name"}
                // Console Pro collapses all logs with the same watch name into one entry.
                UnityEngine.Debug.Log($"{kvp.Key} : {kvp.Value}\nCPAPI:{{\"cmd\":\"Watch\",\"name\":\"{kvp.Key}\"}}");
            }
        }
    }
}
