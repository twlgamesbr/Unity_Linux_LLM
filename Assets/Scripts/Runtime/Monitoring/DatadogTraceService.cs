// <copyright file="DatadogTraceService.cs" company="NPC System">
// Copyright (c) NPC System. All rights reserved.
// </copyright>

namespace NPCSystem
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Sends APM trace spans from Unity to the Datadog Trace Agent (port 8126).
    ///
    /// Because Unity uses IL2CPP/Mono (not the standard .NET CLR), the automatic
    /// Datadog .NET Tracer cannot hook into its runtime. This service provides
    /// manual instrumentation: create spans with <c>StartSpan()</c>, dispose them
    /// when done, and they are batched and POSTed to the Trace Agent as JSON.
    ///
    /// The Trace Agent (part of dd-agent) listens on port 8126 and forwards spans
    /// to Datadog.
    ///
    /// Usage:
    /// <code>
    /// DatadogTracer.Initialize();
    ///
    /// using (var span = DatadogTracer.StartSpan("dialogue.turn",
    ///     resource: "/api/chat", tags: new[] {"npc:butler"}))
    /// {
    ///     // ... do work ...
    ///     span.SetTag("tokens", 42);
    /// }
    /// </code>
    /// </summary>
    public static class DatadogTracer
    {
        private const string DefaultAgentHost = "127.0.0.1";
        private const int DefaultTracePort = 8126;
        private const int FlushIntervalMs = 5000;
        private const int MaxBatchSize = 50;

        private static HttpClient _httpClient;
        private static Timer _flushTimer;
        private static readonly List<SpanData> _pendingSpans = new List<SpanData>();
        private static readonly object _lock = new object();
        private static long _nextSpanId = 1;
        private static long _nextTraceId;
        private static bool _initialized;
        private static string _traceEndpoint;

        /// <summary>
        /// Initializes the APM tracer. Call once at server startup
        /// (alongside <c>DatadogMetricsService.Initialize()</c>).
        /// </summary>
        public static void Initialize(
            string agentHost = DefaultAgentHost,
            int tracePort = DefaultTracePort
        )
        {
            if (_initialized)
                return;

            try
            {
                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

                _traceEndpoint = $"http://{agentHost}:{tracePort}/v0.5/traces";

                // Seed trace ID from process start ticks for uniqueness
                _nextTraceId =
                    (Stopwatch.GetTimestamp() & 0x7FFFFFFFFFFFFFFF)
                    ^ (long)(DateTime.UtcNow.Ticks & 0x7FFFFFFFFFFFFFFF);

                _flushTimer = new Timer(
                    async _ =>
                    {
                        try
                        {
                            await FlushAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[DatadogTracer] Flush error: {ex.Message}"
                            );
                        }
                    },
                    null,
                    FlushIntervalMs,
                    FlushIntervalMs
                );

                _initialized = true;
                UnityEngine.Debug.Log(
                    $"[DatadogTracer] Initialized — sending to {agentHost}:{tracePort}"
                );
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[DatadogTracer] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuts down the tracer, flushes pending spans, and releases resources.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _flushTimer?.Dispose();
            FlushAsync().GetAwaiter().GetResult();
            _httpClient?.Dispose();
            _initialized = false;

            UnityEngine.Debug.Log("[DatadogTracer] Shutdown complete.");
        }

        /// <summary>
        /// Starts a new APM span. Wrap in a <c>using</c> block to auto-finish.
        /// </summary>
        /// <param name="operationName">The span name (e.g. "dialogue.turn", "llm.request").</param>
        /// <param name="service">Service name (default: "unity-dedicated-server").</param>
        /// <param name="resource">Resource being operated on (e.g. "/api/chat", "npc:butler").</param>
        /// <param name="type">Span type: "custom", "web", "db", "cache", etc.</param>
        /// <param name="parent">Parent span for distributed tracing, or null for a root span.</param>
        /// <param name="tags">Optional tags as "key:value" strings.</param>
        /// <returns>A disposable <see cref="Span"/> that records its duration on dispose.</returns>
        public static Span StartSpan(
            string operationName,
            string service = "unity-dedicated-server",
            string resource = null,
            string type = "custom",
            Span parent = null,
            string[] tags = null
        )
        {
            long traceId;
            long parentId = 0;

            if (parent != null)
            {
                traceId = parent.TraceId;
                parentId = parent.SpanId;
            }
            else
            {
                traceId = Interlocked.Increment(ref _nextTraceId);
            }

            long spanId = Interlocked.Increment(ref _nextSpanId);
            long start = NanosSinceEpoch();

            return new Span(
                traceId,
                spanId,
                parentId,
                start,
                operationName,
                service,
                resource ?? operationName,
                type,
                tags
            );
        }

        // ──────────────────────────────────────────────
        //  Internal
        // ──────────────────────────────────────────────

        /// <summary>Enqueue a finished span for the next flush.</summary>
        internal static void EnqueueSpan(SpanData data)
        {
            if (!_initialized)
                return;

            lock (_lock)
            {
                _pendingSpans.Add(data);

                // Flush eagerly if batch is large enough
                if (_pendingSpans.Count >= MaxBatchSize)
                {
                    var snapshot = new List<SpanData>(_pendingSpans);
                    _pendingSpans.Clear();
                    ThreadPool.QueueUserWorkItem(_ => SendTraces(snapshot));
                }
            }
        }

        /// <summary>Flush all pending spans to the Trace Agent.</summary>
        private static async Task FlushAsync()
        {
            List<SpanData> snapshot;
            lock (_lock)
            {
                if (_pendingSpans.Count == 0)
                    return;
                snapshot = new List<SpanData>(_pendingSpans);
                _pendingSpans.Clear();
            }

            await SendTracesAsync(snapshot).ConfigureAwait(false);
        }

        private static void SendTraces(List<SpanData> spans)
        {
            try
            {
                string json = BuildPayload(spans);
                byte[] data = Encoding.UTF8.GetBytes(json);

                using var request = new HttpRequestMessage(HttpMethod.Post, _traceEndpoint)
                {
                    Content = new ByteArrayContent(data),
                };
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = _httpClient
                    .SendAsync(request)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                _ = response.StatusCode; // swallow — we just care it was sent
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DatadogTracer] Send error: {ex.Message}");
            }
        }

        private static async System.Threading.Tasks.Task SendTracesAsync(List<SpanData> spans)
        {
            try
            {
                string json = BuildPayload(spans);
                byte[] data = Encoding.UTF8.GetBytes(json);

                using var request = new HttpRequestMessage(HttpMethod.Post, _traceEndpoint)
                {
                    Content = new ByteArrayContent(data),
                };
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                _ = response.StatusCode;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[DatadogTracer] Send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds the Datadog Trace Agent payload.
        /// Format: [ [ span1, span2 ], [ span3 ] ] — an array of trace arrays.
        /// If all spans share the same trace_id, they go in one trace array.
        /// </summary>
        private static string BuildPayload(List<SpanData> spans)
        {
            // Group spans by trace_id
            var traces = new Dictionary<long, List<Dictionary<string, object>>>();

            foreach (var span in spans)
            {
                if (!traces.TryGetValue(span.TraceId, out var traceSpans))
                {
                    traceSpans = new List<Dictionary<string, object>>();
                    traces[span.TraceId] = traceSpans;
                }

                traceSpans.Add(
                    new Dictionary<string, object>
                    {
                        ["trace_id"] = span.TraceId,
                        ["span_id"] = span.SpanId,
                        ["parent_id"] = span.ParentId,
                        ["start"] = span.StartNanos,
                        ["duration"] = span.DurationNanos,
                        ["service"] = span.Service,
                        ["name"] = span.OperationName,
                        ["resource"] = span.Resource,
                        ["type"] = span.Type,
                        ["meta"] = span.Tags,
                        ["metrics"] = new Dictionary<string, object>(),
                    }
                );
            }

            // Build outer array of trace arrays
            var payload = new List<object>();
            foreach (var traceSpans in traces.Values)
            {
                payload.Add(traceSpans);
            }

            return JsonConvert.SerializeObject(
                payload,
                Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
            );
        }

        private static long NanosSinceEpoch()
        {
            return (DateTime.UtcNow.Ticks - 621355968000000000L) * 100;
        }
    }

    // ──────────────────────────────────────────────
    //  Span data record
    // ──────────────────────────────────────────────

    /// <summary>Immutable snapshot of a completed span, ready for serialization.</summary>
    internal readonly struct SpanData
    {
        public long TraceId { get; }
        public long SpanId { get; }
        public long ParentId { get; }
        public long StartNanos { get; }
        public long DurationNanos { get; }
        public string OperationName { get; }
        public string Service { get; }
        public string Resource { get; }
        public string Type { get; }
        public Dictionary<string, string> Tags { get; }

        public SpanData(
            long traceId,
            long spanId,
            long parentId,
            long startNanos,
            long durationNanos,
            string operationName,
            string service,
            string resource,
            string type,
            Dictionary<string, string> tags
        )
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentId = parentId;
            StartNanos = startNanos;
            DurationNanos = durationNanos;
            OperationName = operationName;
            Service = service;
            Resource = resource;
            Type = type;
            Tags = tags;
        }
    }

    // ──────────────────────────────────────────────
    //  Active span (use with `using` block)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Represents an in-flight APM span. Record tags during execution, then
    /// dispose to record duration and enqueue for submission.
    ///
    /// <code>
    /// using (var span = DatadogTracer.StartSpan("my.operation"))
    /// {
    ///     span.SetTag("key", "value");
    ///     // ... work ...
    /// }
    /// </code>
    /// </summary>
    public sealed class Span : IDisposable
    {
        private readonly long _traceId;
        private readonly long _spanId;
        private readonly long _parentId;
        private readonly long _startNanos;
        private readonly string _operationName;
        private readonly string _service;
        private readonly string _resource;
        private readonly string _type;
        private readonly Dictionary<string, string> _tags;
        private volatile bool _finished;

        internal Span(
            long traceId,
            long spanId,
            long parentId,
            long startNanos,
            string operationName,
            string service,
            string resource,
            string type,
            string[] tags
        )
        {
            _traceId = traceId;
            _spanId = spanId;
            _parentId = parentId;
            _startNanos = startNanos;
            _operationName = operationName;
            _service = service;
            _resource = resource;
            _type = type;
            _tags = new Dictionary<string, string>();

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    int colon = tag.IndexOf(':');
                    if (colon > 0)
                    {
                        _tags[tag.Substring(0, colon)] = tag.Substring(colon + 1);
                    }
                }
            }
        }

        /// <summary>This span's trace ID (for parent/child linking).</summary>
        public long TraceId => _traceId;

        /// <summary>This span's unique ID.</summary>
        public long SpanId => _spanId;

        /// <summary>Set a tag on this span (thread-safe before dispose).</summary>
        public void SetTag(string key, string value)
        {
            if (!_finished)
            {
                lock (_tags)
                {
                    _tags[key] = value;
                }
            }
        }

        /// <summary>Set an error tag + message to mark this span as failed.</summary>
        public void SetError(string errorMessage = null)
        {
            if (!_finished)
            {
                lock (_tags)
                {
                    _tags["error"] = "true";
                    if (errorMessage != null)
                        _tags["error.message"] = errorMessage;
                }
            }
        }

        /// <summary>Finishes the span and enqueues it for submission.</summary>
        public void Dispose()
        {
            if (_finished)
                return;
            _finished = true;

            long durationNanos = NanosSinceEpoch() - _startNanos;
            if (durationNanos < 1)
                durationNanos = 1;

            var data = new SpanData(
                _traceId,
                _spanId,
                _parentId,
                _startNanos,
                durationNanos,
                _operationName,
                _service,
                _resource,
                _type,
                _tags
            );

            DatadogTracer.EnqueueSpan(data);
        }

        private static long NanosSinceEpoch()
        {
            return (DateTime.UtcNow.Ticks - 621355968000000000L) * 100;
        }
    }
}
