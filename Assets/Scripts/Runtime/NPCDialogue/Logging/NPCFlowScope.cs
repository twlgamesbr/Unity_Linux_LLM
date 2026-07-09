using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NPCSystem
{
    public sealed class NPCFlowScope : IDisposable
    {
        readonly NPCFlowLogger _logger;
        readonly NPCFlowStage _stage;
        readonly string _source;
        readonly string _requestId;
        readonly string _npcSlug;
        readonly Stopwatch _stopwatch;
        bool _completed;

        public string RequestId => _requestId;
        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        NPCFlowScope(
            NPCFlowLogger logger,
            NPCFlowStage stage,
            string source,
            string requestId,
            string npcSlug
        )
        {
            _logger = logger;
            _stage = stage;
            _source = source ?? string.Empty;
            _requestId = requestId ?? string.Empty;
            _npcSlug = npcSlug ?? string.Empty;
            _stopwatch = Stopwatch.StartNew();
        }

        public static NPCFlowScope Start(
            NPCFlowLogger logger,
            NPCFlowStage stage,
            string source,
            string requestId = null,
            string npcSlug = null,
            Dictionary<string, object> data = null
        )
        {
            logger ??= NPCFlowLogger.FindOrCreate();
            var scope = new NPCFlowScope(logger, stage, source, requestId, npcSlug);
            logger.Log(
                stage,
                NPCFlowStatus.Start,
                NPCFlowLogLevel.Info,
                $"{stage} started.",
                source,
                requestId,
                npcSlug,
                0,
                data
            );
            return scope;
        }

        public void Success(string message = null, Dictionary<string, object> data = null)
        {
            Complete(
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                message ?? $"{_stage} completed.",
                data
            );
        }

        public void Fallback(string message = null, Dictionary<string, object> data = null)
        {
            Complete(
                NPCFlowStatus.Fallback,
                NPCFlowLogLevel.Warning,
                message ?? $"{_stage} fell back.",
                data
            );
        }

        public void Skipped(string message = null, Dictionary<string, object> data = null)
        {
            Complete(
                NPCFlowStatus.Skipped,
                NPCFlowLogLevel.Info,
                message ?? $"{_stage} skipped.",
                data
            );
        }

        public void Warning(string message = null, Dictionary<string, object> data = null)
        {
            Complete(
                NPCFlowStatus.Warning,
                NPCFlowLogLevel.Warning,
                message ?? $"{_stage} warning.",
                data
            );
        }

        public void Error(
            Exception exception,
            string message = null,
            Dictionary<string, object> data = null
        )
        {
            data ??= new Dictionary<string, object>();
            if (exception != null)
            {
                data["exceptionType"] = exception.GetType().Name;
                data["exceptionMessage"] = exception.Message;
            }
            Complete(
                NPCFlowStatus.Error,
                NPCFlowLogLevel.Error,
                message ?? $"{_stage} failed.",
                data
            );
        }

        public void Dispose()
        {
            _stopwatch.Stop();
        }

        void Complete(
            NPCFlowStatus status,
            NPCFlowLogLevel level,
            string message,
            Dictionary<string, object> data
        )
        {
            if (_completed)
                return;
            _completed = true;
            _stopwatch.Stop();
            _logger?.Log(
                _stage,
                status,
                level,
                message,
                _source,
                _requestId,
                _npcSlug,
                _stopwatch.ElapsedMilliseconds,
                data
            );
        }
    }
}
