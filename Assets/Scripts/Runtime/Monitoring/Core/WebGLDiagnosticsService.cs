using System.Collections.Generic;
using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Monitors WebGL runtime health — FPS, memory, network quality — and
    /// emits periodic <see cref="TelemetryEvent"/>s via <see cref="TelemetryRouter"/>.
    ///
    /// Uses <c>PerformanceTracker</c> scopes for diagnostics snapshots.
    /// On non-WebGL platforms it reports diagnostic info via SystemInfo.
    ///
    /// Attach to a persistent GameObject (e.g. the Init system root).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class WebGLDiagnosticsService : MonoBehaviour
    {
        [Header("Polling")]
        [SerializeField]
        [Tooltip("Seconds between health-check snapshots (0 = disabled).")]
        float _pollIntervalSeconds = 30f;

        [Header("Thresholds")]
        [SerializeField]
        [Tooltip("FPS below this triggers a warning event.")]
        int _lowFpsThreshold = 20;

        [SerializeField]
        [Tooltip("Memory above this (MB) triggers a warning event.")]
        int _highMemoryMbThreshold = 800;

        float _nextPollTime;
        int _lastFrameCount;
        float _lastFrameTime;
        bool _warnedLowFps;
        bool _warnedHighMemory;

        // FPS smoothed average
        const int FpsSmoothingFrames = 60;
        readonly Queue<float> _frameDeltas = new Queue<float>(FpsSmoothingFrames);

        public float CurrentFps { get; private set; } = 60f;
        public long CurrentMemoryMb { get; private set; }
        public bool IsWebGL => Application.platform == RuntimePlatform.WebGLPlayer;

        void Start()
        {
            _nextPollTime = Time.unscaledTime + _pollIntervalSeconds;
            _lastFrameCount = Time.frameCount;
            _lastFrameTime = Time.unscaledTime;
        }

        void Update()
        {
            // Compute smoothed FPS
            float delta = Time.unscaledDeltaTime;
            if (delta > 0f && delta < 0.5f) // ignore huge deltas (pauses, load screens)
            {
                _frameDeltas.Enqueue(delta);
                if (_frameDeltas.Count > FpsSmoothingFrames)
                    _frameDeltas.Dequeue();

                float sum = 0f;
                foreach (float d in _frameDeltas)
                    sum += d;
                CurrentFps = _frameDeltas.Count / sum;
            }

            // Track memory (polling every frame is fine — it's a cached property)
            CurrentMemoryMb = SystemInfo.graphicsMemorySize;

            // Periodic health snapshot
            if (_pollIntervalSeconds > 0f && Time.unscaledTime >= _nextPollTime)
            {
                _nextPollTime = Time.unscaledTime + _pollIntervalSeconds;
                EmitHealthSnapshot();
            }
        }

        void EmitHealthSnapshot()
        {
            float fps = CurrentFps;
            long memMb = CurrentMemoryMb;
            int processorCount = SystemInfo.processorCount;
            int processorFrequencyMhz = SystemInfo.processorFrequency;
            string platform = Application.platform.ToString();
            string qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()];

            using var perf = PerformanceTracker.Start(
                source: nameof(WebGLDiagnosticsService),
                category: "SystemHealth",
                tags: new Dictionary<string, object>
                {
                    ["fps"] = Mathf.RoundToInt(fps),
                    ["memoryMb"] = memMb,
                    ["processorCount"] = processorCount,
                    ["processorFrequencyMhz"] = processorFrequencyMhz,
                    ["platform"] = platform,
                    ["qualityLevel"] = qualityLevel,
                    ["isWebGL"] = IsWebGL ? "true" : "false",
                }
            );

            // Check thresholds and emit warnings
            if (fps < _lowFpsThreshold)
            {
                if (!_warnedLowFps)
                {
                    _warnedLowFps = true;
                    Debug.LogWarning(
                        $"[WebGLDiagnostics] Low FPS detected: {fps:F1}. Consider reducing quality settings."
                    );
                }
                // Keep emitting warning events
                TelemetryRouter.Point(
                    requestId: null,
                    source: nameof(WebGLDiagnosticsService),
                    category: "SystemHealth",
                    status: "warning",
                    message: $"Low FPS: {fps:F1}",
                    tags: new Dictionary<string, object>
                    {
                        ["fps"] = Mathf.RoundToInt(fps),
                        ["threshold"] = _lowFpsThreshold,
                    }
                );
            }
            else
            {
                _warnedLowFps = false;
            }

            if (memMb > _highMemoryMbThreshold)
            {
                if (!_warnedHighMemory)
                {
                    _warnedHighMemory = true;
                    Debug.LogWarning($"[WebGLDiagnostics] High memory: {memMb}MB. Check asset streaming.");
                }
                TelemetryRouter.Point(
                    requestId: null,
                    source: nameof(WebGLDiagnosticsService),
                    category: "SystemHealth",
                    status: "warning",
                    message: $"High memory: {memMb}MB",
                    tags: new Dictionary<string, object>
                    {
                        ["memoryMb"] = memMb,
                        ["threshold"] = _highMemoryMbThreshold,
                    }
                );
            }
            else
            {
                _warnedHighMemory = false;
            }

            // Network quality check (best effort)
            if (IsWebGL && Application.internetReachability != NetworkReachability.NotReachable)
            {
                TelemetryRouter.Point(
                    requestId: null,
                    source: nameof(WebGLDiagnosticsService),
                    category: "NetworkHealth",
                    status: "info",
                    message: $"Network reachable: {Application.internetReachability}",
                    tags: new Dictionary<string, object>
                    {
                        ["reachability"] = Application.internetReachability.ToString(),
                    }
                );
            }
        }

        /// <summary>
        /// Call when the user changes quality settings to reset FPS thresholds.
        /// </summary>
        public void ResetWarnings()
        {
            _warnedLowFps = false;
            _warnedHighMemory = false;
        }
    }
}
