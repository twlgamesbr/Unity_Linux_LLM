using System;
using NPCSystem.Monitoring;
using NPCSystem.Monitoring.Datadog;
using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Bootstraps the <see cref="TelemetryRouter"/> with registered sinks.
    /// Call once during game initialization (NPCFlowLogger.Awake at order -3000).
    /// </summary>
    public static class TelemetryBootstrapper
    {
        static bool _initialized;

        public static void Initialize(
            string sessionId = null,
            string metricPrefix = "npc",
            bool enableFileSink = true,
            bool enableDatadogSink = true
        )
        {
            if (_initialized)
            {
                Debug.Log("[TelemetryBootstrapper] Already initialized — skipping.");
                return;
            }
            _initialized = true;

            try
            {
                TelemetryRouter router = TelemetryRouter.Instance;

                ConsoleProLogInterceptor.Initialize();

                if (enableFileSink)
                {
                    var fileSink = new FileTelemetrySink(
                        sessionId: sessionId ?? Guid.NewGuid().ToString("N")
                    );
                    router.Register(fileSink);
                    Debug.Log(
                        $"[TelemetryBootstrapper] FileTelemetrySink registered → {Application.persistentDataPath}/Telemetry/"
                    );
                }

                {
                    var consoleSink = new ConsoleProTelemetrySink(filterPrefix: metricPrefix);
                    router.Register(consoleSink);
                    Debug.Log("[TelemetryBootstrapper] ConsoleProTelemetrySink registered.");
                }

                if (enableDatadogSink)
                {
                    var datadogSink = new DatadogTelemetrySink(metricPrefix: metricPrefix);
                    router.Register(datadogSink);
                    Debug.Log("[TelemetryBootstrapper] DatadogTelemetrySink registered.");
                }

                router.Freeze();

                Debug.Log(
                    $"[TelemetryBootstrapper] Telemetry router initialized with {router.SinkCount} sink(s)."
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryBootstrapper] Initialization failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}
