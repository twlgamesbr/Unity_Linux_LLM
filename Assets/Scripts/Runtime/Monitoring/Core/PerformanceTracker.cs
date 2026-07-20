using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Lightweight <c>using</c> scope that tracks elapsed time and emits
    /// start/end <see cref="TelemetryEvent"/> pairs via <see cref="TelemetryRouter"/>.
    ///
    /// Usage:
    /// <code>
    /// using var perf = PerformanceTracker.Start("rag.search", requestId, tags: ...);
    /// // ... work ...
    /// perf.Tag("resultCount", 5);
    /// // On dispose: emits timed TelemetryEvent with all tags
    /// </code>
    /// </summary>
    public sealed class PerformanceTracker : IDisposable
    {
        readonly string _requestId;
        readonly string _source;
        readonly string _category;
        readonly Stopwatch _stopwatch;
        readonly Dictionary<string, object> _tags;
        bool _completed;

        PerformanceTracker(string requestId, string source, string category, Dictionary<string, object> initialTags)
        {
            _requestId = requestId ?? string.Empty;
            _source = source ?? string.Empty;
            _category = category ?? string.Empty;
            _stopwatch = Stopwatch.StartNew();
            _tags = initialTags != null
                ? new Dictionary<string, object>(initialTags)
                : new Dictionary<string, object>();

            TelemetryRouter.Point(requestId, source, category, "start", $"{source} started.", _tags);
        }

        /// <summary>
        /// Start a new performance scope. Emits a "start" event immediately.
        /// </summary>
        public static PerformanceTracker Start(
            string source,
            string requestId = null,
            string category = "General",
            Dictionary<string, object> tags = null)
        {
            return new PerformanceTracker(requestId, source, category, tags);
        }

        /// <summary>
        /// Add a tag to the completion event. Thread-safe before dispose.
        /// </summary>
        public void Tag(string key, object value)
        {
            if (!_completed)
            {
                lock (_tags)
                {
                    if (!_completed)
                        _tags[key] = value;
                }
            }
        }

        /// <summary>
        /// Mark the scope as succeeded (default on dispose if not called).
        /// </summary>
        public void MarkSuccess(string message = null)
        {
            Complete("success", message ?? $"{_source} completed.");
        }

        /// <summary>
        /// Mark the scope as failed.
        /// </summary>
        public void MarkError(string message = null, Exception exception = null)
        {
            if (exception != null)
            {
                lock (_tags)
                    _tags["exceptionType"] = exception.GetType().Name;
            }
            Complete("error", message ?? $"{_source} failed.");
        }

        /// <summary>
        /// Mark the scope as fallback (e.g., secondary path succeeded after primary failed).
        /// </summary>
        public void MarkFallback(string message = null)
        {
            Complete("fallback", message ?? $"{_source} used fallback.");
        }

        void Complete(string status, string message)
        {
            if (_completed)
                return;
            lock (_tags)
            {
                if (_completed)
                    return;
                _completed = true;
                _stopwatch.Stop();

                TelemetryRouter.Timed(
                    _requestId, _source, _category, status,
                    _stopwatch.ElapsedMilliseconds, message,
                    new Dictionary<string, object>(_tags)
                );
            }
        }

        void IDisposable.Dispose()
        {
            if (!_completed)
                MarkSuccess();
        }
    }
}
