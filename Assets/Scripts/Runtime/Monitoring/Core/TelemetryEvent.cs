using System;
using System.Collections.Generic;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Unified event envelope for all telemetry in the NPC system.
    /// Every operation (auth, spawn, dialogue, RAG, LLM, item trade, network)
    /// emits one of these with a correlation requestId for cross-flow stitching.
    /// </summary>
    public readonly struct TelemetryEvent
    {
        /// <summary>Schema version for forward compatibility.</summary>
        public int SchemaVersion { get; }

        /// <summary>Correlation ID stitching this event into a request trace.</summary>
        public string RequestId { get; }

        /// <summary>Source component name (e.g. "NPCDialogueSessionService", "QdrantRAGService").</summary>
        public string Source { get; }

        /// <summary>Semantic category for routing and filtering.</summary>
        public string Category { get; }

        /// <summary>Outcome: "start", "success", "error", "fallback", "warning".</summary>
        public string Status { get; }

        /// <summary>Elapsed milliseconds for the operation (0 for point-in-time events).</summary>
        public long DurationMs { get; }

        /// <summary>UTC timestamp when the event was created.</summary>
        public DateTime Timestamp { get; }

        /// <summary>Arbitrary key-value tags for dimensional analysis.</summary>
        public IReadOnlyDictionary<string, object> Tags { get; }

        /// <summary>Human-readable message.</summary>
        public string Message { get; }

        public TelemetryEvent(
            int schemaVersion,
            string requestId,
            string source,
            string category,
            string status,
            long durationMs,
            DateTime timestamp,
            IReadOnlyDictionary<string, object> tags,
            string message
        )
        {
            SchemaVersion = schemaVersion;
            RequestId = requestId ?? string.Empty;
            Source = source ?? string.Empty;
            Category = category ?? string.Empty;
            Status = status ?? string.Empty;
            DurationMs = durationMs;
            Timestamp = timestamp;
            Tags = tags ?? TelemetryEvent.EmptyTags;
            Message = message ?? string.Empty;
        }

        static readonly Dictionary<string, object> EmptyTags = new Dictionary<string, object>(0);

        public static TelemetryEvent Point(
            string requestId,
            string source,
            string category,
            string status,
            string message = null,
            Dictionary<string, object> tags = null
        )
        {
            return new TelemetryEvent(
                schemaVersion: 1,
                requestId: requestId,
                source: source,
                category: category,
                status: status,
                durationMs: 0,
                timestamp: DateTime.UtcNow,
                tags: tags,
                message: message
            );
        }

        public static TelemetryEvent Timed(
            string requestId,
            string source,
            string category,
            string status,
            long durationMs,
            string message = null,
            Dictionary<string, object> tags = null
        )
        {
            return new TelemetryEvent(
                schemaVersion: 1,
                requestId: requestId,
                source: source,
                category: category,
                status: status,
                durationMs: durationMs,
                timestamp: DateTime.UtcNow,
                tags: tags,
                message: message
            );
        }
    }
}
