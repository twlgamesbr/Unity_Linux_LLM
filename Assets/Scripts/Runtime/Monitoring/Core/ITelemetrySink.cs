namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Pluggable sink that receives telemetry events from <see cref="TelemetryRouter"/>.
    /// Implementations write to Datadog, JSONL files, Unity Console, etc.
    /// </summary>
    public interface ITelemetrySink
    {
        /// <summary>
        /// Emit a telemetry event. Called synchronously from the router's emit loop.
        /// Implementations should be thread-safe and non-blocking where possible.
        /// </summary>
        void Emit(in TelemetryEvent evt);

        /// <summary>
        /// Optional display name for diagnostics.
        /// </summary>
        string DisplayName { get; }
    }
}
