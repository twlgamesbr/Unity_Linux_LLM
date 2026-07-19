using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Writes <see cref="TelemetryEvent"/> instances to a structured JSONL file.
    /// One JSON object per line, rotated daily.
    ///
    /// Path: <c>{Application.persistentDataPath}/Telemetry/{sessionId}/flow_{yyyyMMdd}.jsonl</c>
    ///
    /// Thread-safe via lock. Safe for WebGL (writes to persistent data path).
    /// On WebGL, files persist in the browser's IndexedDB-backed persistent storage.
    /// </summary>
    public sealed class FileTelemetrySink : ITelemetrySink, IDisposable
    {
        public string DisplayName => "File (JSONL)";

        readonly string _basePath;
        readonly string _sessionId;
        readonly object _lock = new object();
        StreamWriter _writer;
        string _currentDate;

        /// <summary>
        /// Create a file sink that writes to a subdirectory under <paramref name="basePath"/>.
        /// </summary>
        /// <param name="sessionId">Logical session identifier used as a subfolder name.
        /// If null/empty, a GUID is generated.</param>
        /// <param name="basePath">Root directory. If null, defaults to
        /// <c>Application.persistentDataPath/Telemetry</c>.</param>
        public FileTelemetrySink(string sessionId = null, string basePath = null)
        {
            _sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId;

            _basePath = basePath
                ?? Path.Combine(Application.persistentDataPath, "Telemetry");

            Directory.CreateDirectory(_basePath);
        }

        public void Emit(in TelemetryEvent evt)
        {
            string today = DateTime.UtcNow.ToString("yyyyMMdd");

            lock (_lock)
            {
                // Rotate file on date change
                if (_writer == null || _currentDate != today)
                {
                    _writer?.Dispose();
                    string datePath = Path.Combine(_basePath, _sessionId);
                    Directory.CreateDirectory(datePath);
                    string filePath = Path.Combine(datePath, $"flow_{today}.jsonl");
                    _writer = new StreamWriter(filePath, append: true);
                    _currentDate = today;
                }

                var entry = new Dictionary<string, object>
                {
                    ["schemaVersion"] = evt.SchemaVersion,
                    ["timestampUtc"] = evt.Timestamp.ToString("O"),
                    ["requestId"] = evt.RequestId,
                    ["source"] = evt.Source,
                    ["category"] = evt.Category,
                    ["status"] = evt.Status,
                    ["durationMs"] = evt.DurationMs,
                    ["message"] = evt.Message,
                };

                // Include tags if present
                if (evt.Tags != null && evt.Tags.Count > 0)
                {
                    entry["tags"] = new Dictionary<string, object>(evt.Tags);
                }

                string json = UnityEngine.JsonUtility.ToJson(
                    new TelemetryJsonWrapper { entry = entry }
                );

                // JsonUtility wraps in an object — strip the wrapper to get raw JSON line
                // {"entry":{...}} → {...}
                json = ExtractInnerJson(json);

                _writer.WriteLine(json);
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        /// <summary>
        /// Hack: strip JsonUtility's outer {"entry":...} wrapper.
        /// We use JsonUtility because it's the only serializer available
        /// in all Unity build targets including WebGL without extra dependencies.
        /// </summary>
        static string ExtractInnerJson(string wrapperJson)
        {
            if (string.IsNullOrEmpty(wrapperJson))
                return "{}";

            const string prefix = "{\"entry\":";
            if (wrapperJson.StartsWith(prefix) && wrapperJson.EndsWith("}"))
            {
                return wrapperJson.Substring(prefix.Length, wrapperJson.Length - prefix.Length - 1);
            }

            return wrapperJson;
        }

        [Serializable]
        struct TelemetryJsonWrapper
        {
            public Dictionary<string, object> entry;
        }
    }
}
