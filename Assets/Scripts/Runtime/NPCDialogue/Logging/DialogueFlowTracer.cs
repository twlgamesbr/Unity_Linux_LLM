using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NPCSystem
{
    /// <summary>
    /// Traces the complete dialogue pipeline: input → context → prompt → llm → response.
    /// Emits structured logs via NPCFlowLogger for each stage with timing and status.
    /// Used to debug dialogue failures instantly by showing exactly where time is spent
    /// and which stage failed.
    /// </summary>
    public sealed class DialogueFlowTracer
    {
        readonly NPCFlowLogger _logger;
        readonly string _requestId;
        readonly string _npcSlug;
        readonly Stopwatch _totalStopwatch;
        readonly Dictionary<string, long> _stageTimings;
        string _currentStage;

        public string RequestId => _requestId;
        public string NpcSlug => _npcSlug;
        public long TotalElapsedMs => _totalStopwatch.ElapsedMilliseconds;

        DialogueFlowTracer(NPCFlowLogger logger, string requestId, string npcSlug)
        {
            _logger = logger ?? NPCFlowLogger.FindOrCreate();
            _requestId = requestId ?? string.Empty;
            _npcSlug = npcSlug ?? string.Empty;
            _totalStopwatch = Stopwatch.StartNew();
            _stageTimings = new Dictionary<string, long>();
        }

        /// <summary>
        /// Start tracing a new dialogue turn. Returns a tracer that should be
        /// disposed when the turn completes (or use Complete/Failed explicitly).
        /// </summary>
        public static DialogueFlowTracer Start(
            string npcSlug,
            string requestId = null,
            NPCFlowLogger logger = null
        )
        {
            logger ??= NPCFlowLogger.FindOrCreate();
            requestId ??= logger.NextRequestId();

            var tracer = new DialogueFlowTracer(logger, requestId, npcSlug);

            tracer._logger.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Start,
                NPCFlowLogLevel.Info,
                $"Dialogue turn started for NPC '{npcSlug}'.",
                source: nameof(DialogueFlowTracer),
                requestId: requestId,
                npcSlug: npcSlug,
                data: new Dictionary<string, object>
                {
                    ["pipeline"] = "input→context→prompt→llm→response",
                }
            );

            return tracer;
        }

        /// <summary>
        /// Trace the input stage (player message received).
        /// </summary>
        public void TraceInput(string playerMessage)
        {
            _currentStage = "input";
            var sw = Stopwatch.StartNew();

            _logger.Log(
                NPCFlowStage.UIInput,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug,
                $"Player input received: {_logger.SummarizeText("player", playerMessage)}.",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                data: new Dictionary<string, object>
                {
                    ["stage"] = "input",
                    ["messageLength"] = playerMessage?.Length ?? 0,
                }
            );

            sw.Stop();
            _stageTimings["input"] = sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// Trace the context loading stage (player context loaded from Supabase/local).
        /// </summary>
        public void TraceContextLoad(PlayerDialogueContext context, bool fromServer, long durationMs)
        {
            _currentStage = "context";

            _logger.Log(
                NPCFlowStage.ContextRetrieval,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug,
                $"Player context loaded (server={fromServer}, trust={context.TrustScore}, mood={context.CurrentMood}).",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: durationMs,
                data: new Dictionary<string, object>
                {
                    ["stage"] = "context",
                    ["loadedFromServer"] = fromServer,
                    ["trustScore"] = context.TrustScore,
                    ["mood"] = context.CurrentMood,
                    ["clueCount"] = context.KnownClues.Count,
                    ["itemCount"] = context.Inventory.Count,
                }
            );

            _stageTimings["context"] = durationMs;
        }

        /// <summary>
        /// Trace the prompt building stage (system prompt + context block assembled).
        /// </summary>
        public void TracePromptBuild(int promptLength, int historyTurns, long durationMs)
        {
            _currentStage = "prompt";

            _logger.Log(
                NPCFlowStage.PromptBuild,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug,
                $"Prompt built: {promptLength} chars, {historyTurns} history turns.",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: durationMs,
                data: new Dictionary<string, object>
                {
                    ["stage"] = "prompt",
                    ["promptLength"] = promptLength,
                    ["historyTurns"] = historyTurns,
                }
            );

            _stageTimings["prompt"] = durationMs;
        }

        /// <summary>
        /// Trace the LLM call stage (LocalAI request sent and response received).
        /// </summary>
        public void TraceLLMCall(string model, int responseLength, bool success, long durationMs)
        {
            _currentStage = "llm";

            _logger.Log(
                NPCFlowStage.BackendRequest,
                success ? NPCFlowStatus.Success : NPCFlowStatus.Error,
                success ? NPCFlowLogLevel.Debug : NPCFlowLogLevel.Error,
                success
                    ? $"LLM response received: {responseLength} chars from '{model}'."
                    : $"LLM call failed for model '{model}'.",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: durationMs,
                data: new Dictionary<string, object>
                {
                    ["stage"] = "llm",
                    ["model"] = model,
                    ["responseLength"] = responseLength,
                    ["success"] = success,
                }
            );

            _stageTimings["llm"] = durationMs;
        }

        /// <summary>
        /// Trace the response stage (dialogue message parsed and history updated).
        /// </summary>
        public void TraceResponse(string responseSummary, long durationMs)
        {
            _currentStage = "response";

            _logger.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug,
                $"Response complete: {_logger.SummarizeText("npc", responseSummary)}.",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: durationMs,
                data: new Dictionary<string, object>
                {
                    ["stage"] = "response",
                    ["responseLength"] = responseSummary?.Length ?? 0,
                }
            );

            _stageTimings["response"] = durationMs;
        }

        /// <summary>
        /// Mark the dialogue turn as successfully completed.
        /// Logs the full pipeline summary with per-stage timings.
        /// </summary>
        public void Complete()
        {
            _totalStopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["stage"] = "complete",
                ["totalMs"] = _totalStopwatch.ElapsedMilliseconds,
            };
            foreach (var kvp in _stageTimings)
            {
                data[$"ms_{kvp.Key}"] = kvp.Value;
            }

            _logger.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                $"Dialogue turn complete for NPC '{_npcSlug}' in {_totalStopwatch.ElapsedMilliseconds}ms "
                + $"(context={GetStageMs("context")}ms, prompt={GetStageMs("prompt")}ms, "
                + $"llm={GetStageMs("llm")}ms, response={GetStageMs("response")}ms).",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: _totalStopwatch.ElapsedMilliseconds,
                data: data
            );
        }

        /// <summary>
        /// Mark the dialogue turn as failed at the current stage.
        /// Logs the failure with per-stage timings up to the failure point.
        /// </summary>
        public void Failed(Exception ex)
        {
            _totalStopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["stage"] = "failed",
                ["failedAt"] = _currentStage ?? "unknown",
                ["totalMs"] = _totalStopwatch.ElapsedMilliseconds,
                ["exceptionType"] = ex?.GetType().Name ?? "Unknown",
                ["exceptionMessage"] = ex?.Message ?? string.Empty,
            };
            foreach (var kvp in _stageTimings)
            {
                data[$"ms_{kvp.Key}"] = kvp.Value;
            }

            _logger.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Error,
                NPCFlowLogLevel.Error,
                $"Dialogue turn failed for NPC '{_npcSlug}' at stage '{_currentStage}' "
                + $"after {_totalStopwatch.ElapsedMilliseconds}ms: {ex?.Message}",
                source: nameof(DialogueFlowTracer),
                requestId: _requestId,
                npcSlug: _npcSlug,
                elapsedMs: _totalStopwatch.ElapsedMilliseconds,
                data: data
            );
        }

        long GetStageMs(string stage)
        {
            return _stageTimings.TryGetValue(stage, out long ms) ? ms : 0;
        }
    }
}
