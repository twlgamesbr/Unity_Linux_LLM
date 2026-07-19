using System;
using System.Collections.Generic;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Singleton router that distributes <see cref="TelemetryEvent"/> instances
    /// to all registered <see cref="ITelemetrySink"/> implementations.
    ///
    /// Thread-safe for concurrent registration and emit.
    /// Sinks are called synchronously on the emit thread.
    /// </summary>
    public sealed class TelemetryRouter
    {
        public static TelemetryRouter Instance { get; } = new TelemetryRouter();

        readonly object _lock = new object();
        readonly List<ITelemetrySink> _sinks = new List<ITelemetrySink>();
        bool _frozen;

        TelemetryRouter() { }

        /// <summary>
        /// Register a sink. Throws if the router is frozen (after game init).
        /// </summary>
        public void Register(ITelemetrySink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            lock (_lock)
            {
                if (_frozen)
                    throw new InvalidOperationException("TelemetryRouter is frozen — register sinks during initialization only.");
                if (!_sinks.Contains(sink))
                    _sinks.Add(sink);
            }
        }

        /// <summary>
        /// Unregister a sink. Safe to call at any time.
        /// </summary>
        public void Unregister(ITelemetrySink sink)
        {
            if (sink == null) return;
            lock (_lock)
            {
                _sinks.Remove(sink);
            }
        }

        /// <summary>
        /// Freeze the router — no more sinks can be registered.
        /// Call after initialization is complete to prevent misconfiguration.
        /// </summary>
        public void Freeze()
        {
            lock (_lock) _frozen = true;
        }

        /// <summary>
        /// Emit a telemetry event to all registered sinks.
        /// Sinks are called in registration order.
        /// A failing sink does not prevent other sinks from receiving the event.
        /// </summary>
        public void Emit(in TelemetryEvent evt)
        {
            ITelemetrySink[] snapshot;
            lock (_lock)
            {
                if (_sinks.Count == 0) return;
                snapshot = _sinks.ToArray();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Emit(evt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[TelemetryRouter] Sink '{snapshot[i].DisplayName}' failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Convenience: emit a point-in-time event.
        /// </summary>
        public static void Point(
            string requestId, string source, string category, string status,
            string message = null, Dictionary<string, object> tags = null)
        {
            Instance.Emit(TelemetryEvent.Point(requestId, source, category, status, message, tags));
        }

        /// <summary>
        /// Convenience: emit a timed event.
        /// </summary>
        public static void Timed(
            string requestId, string source, string category, string status, long durationMs,
            string message = null, Dictionary<string, object> tags = null)
        {
            Instance.Emit(TelemetryEvent.Timed(requestId, source, category, status, durationMs, message, tags));
        }

        /// <summary>
        /// Number of registered sinks (diagnostic).
        /// </summary>
        public int SinkCount
        {
            get { lock (_lock) return _sinks.Count; }
        }
    }
}
