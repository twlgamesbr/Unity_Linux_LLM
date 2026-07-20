// <copyright file="DatadogTelemetrySink.cs" company="NPC System">
// Copyright (c) NPC System. All rights reserved.
// </copyright>

namespace NPCSystem.Monitoring.Datadog
{
    using System;
    using System.Collections.Generic;
    using NPCSystem.Monitoring;

    /// <summary>
    /// Bridges <see cref="TelemetryRouter"/> events into the existing
    /// <see cref="DatadogMetricsService"/> and <see cref="DatadogTracer"/>.
    /// Timed events → DatadogMetricsService.Timer() with metric name derived from category+source.
    /// Point events with "start" status → ignore (DatadogTracer handles scopes via NPCFlowScope callback).
    /// Register during game initialization:
    /// <code>
    /// TelemetryRouter.Instance.Register(new DatadogTelemetrySink());
    /// </code>
    /// </summary>
    public sealed class DatadogTelemetrySink : ITelemetrySink
    {
        public string DisplayName => "Datadog";

        /// <summary>
        /// Optional prefix for all Datadog metric names (default "npc").
        /// </summary>
        readonly string _metricPrefix;

        public DatadogTelemetrySink(string metricPrefix = "npc")
        {
            _metricPrefix = string.IsNullOrWhiteSpace(metricPrefix)
                ? "npc"
                : metricPrefix.Trim().TrimEnd('.');
        }

        public void Emit(in TelemetryEvent evt)
        {
            // Skip "start" events — they are metadata for PerformanceTracker scopes,
            // not meaningful Datadog metrics on their own.
            if (evt.Status == "start")
                return;

            string metricName = $"{_metricPrefix}.{evt.Category}.{evt.Source}"
                .ToLowerInvariant()
                .Replace(' ', '_');

            var tags = new List<string> { $"category:{evt.Category}", $"source:{evt.Source}" };

            if (!string.IsNullOrEmpty(evt.RequestId))
                tags.Add($"request_id:{evt.RequestId}");

            if (evt.DurationMs > 0)
                // Timed event → histogram
                DatadogMetricsService.Timer(
                    metricName,
                    (double)evt.DurationMs,
                    1.0,
                    tags.ToArray()
                );
            else
                // Point event → counter
                DatadogMetricsService.Increment(metricName, 1.0, 1.0, tags.ToArray());
        }
    }
}
