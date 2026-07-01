using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GladeAgenticAI.Core.Memory
{
    /// <summary>
    /// Boilerplate client for interacting with the local Cognee instance.
    /// Handles episodic and semantic memory for the NPC factory.
    /// 
    /// Backend: SQLite (Metadata), LanceDB (Vectors), Kuzu (Graphs).
    /// </summary>
    public class CogneeMemoryService : MonoBehaviour
    {
        /// <summary>
        /// Cross-assembly diagnostic hook. Set by NPCSystem.NPCFlowLogger to log without
        /// a direct assembly reference (avoids circular dependency).
        /// Parameters: logLevel(0=Debug,1=Info,2=Warning,3=Error), stageOrdinal, statusOrdinal,
        /// message, source, data
        /// </summary>
        public static Action<int, int, int, string, string, Dictionary<string, object>> OnDiagnostic;

        // LogLevel ordinals matching NPCSystem.NPCFlowLogLevel
        const int _debug = 1;
        const int _info = 2;
        const int _warning = 3;
        const int _error = 4;

        // Stage ordinals matching NPCSystem.NPCFlowStage
        const int _stageSceneBootstrap = 0;
        const int _stageCogneeSearch = 18;
        const int _stageCogneeWrite = 19;

        // Status ordinals matching NPCSystem.NPCFlowStatus
        const int _statusStart = 0;
        const int _statusSuccess = 1;
        const int _statusSkipped = 2;
        const int _statusFallback = 3;
        const int _statusWarning = 4;
        const int _statusError = 5;

        [Header("Cognee Configuration")]
        [Tooltip("The local endpoint where the Cognee REST API or Python bridge is hosted.")]
        public string CogneeEndpoint = "http://localhost:8000/api/v1";

        public static CogneeMemoryService Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                OnDiagnostic?.Invoke(_debug, _stageSceneBootstrap, _statusSuccess,
                    "CogneeMemoryService instance created.",
                    nameof(CogneeMemoryService),
                    new Dictionary<string, object> { ["endpoint"] = CogneeEndpoint });
            }
            else
            {
                OnDiagnostic?.Invoke(_debug, _stageSceneBootstrap, _statusSkipped,
                    "Duplicate CogneeMemoryService destroyed.",
                    nameof(CogneeMemoryService), null);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Pushes a single interaction or document to Cognee for ingestion.
        /// </summary>
        /// <param name="userId">The ID of the user or player.</param>
        /// <param name="data">The text or JSON data to store.</param>
        /// <param name="onSuccess">Callback on success.</param>
        /// <param name="onError">Callback on error.</param>
        public void AddMemory(string userId, string data, Action onSuccess, Action<string> onError)
        {
            StartCoroutine(AddMemoryCoroutine(userId, data, onSuccess, onError));
        }

        private IEnumerator AddMemoryCoroutine(string userId, string data, Action onSuccess, Action<string> onError)
        {
            string logSource = nameof(CogneeMemoryService) + ".AddMemory";
            OnDiagnostic?.Invoke(_debug, _stageCogneeWrite, _statusStart,
                "Cognee add memory started.",
                logSource,
                new Dictionary<string, object> { ["userId"] = userId, ["dataLength"] = data?.Length ?? 0 });

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection("datasetName", "unity_npc_memory"));
            formData.Add(new MultipartFormFileSection("data", Encoding.UTF8.GetBytes(data), "memory.txt", "text/plain"));

            using (UnityWebRequest request = UnityWebRequest.Post($"{CogneeEndpoint}/add", formData))
            {
                request.timeout = 2; // Fail fast if cognee is not running

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    OnDiagnostic?.Invoke(_warning, _stageCogneeWrite, _statusFallback,
                        $"Cognee add memory failed: {request.error}. Gracefully continuing.",
                        logSource,
                        new Dictionary<string, object>
                        {
                            ["endpoint"] = CogneeEndpoint,
                            ["error"] = request.error,
                            ["userId"] = userId
                        });
                    // Gracefully fail and just pretend it succeeded instead of breaking the dialogue flow
                    onSuccess?.Invoke();
                    yield break;
                }
            }

            // After adding, trigger cognify
            string cognifyJson = "{\"datasets\": [\"unity_npc_memory\"]}";
            using (UnityWebRequest cognifyRequest = new UnityWebRequest($"{CogneeEndpoint}/cognify", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(cognifyJson);
                cognifyRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                cognifyRequest.downloadHandler = new DownloadHandlerBuffer();
                cognifyRequest.SetRequestHeader("Content-Type", "application/json");
                cognifyRequest.timeout = 2;

                yield return cognifyRequest.SendWebRequest();

                OnDiagnostic?.Invoke(_info, _stageCogneeWrite, _statusSuccess,
                    "Cognee add memory completed.",
                    logSource,
                    new Dictionary<string, object>
                    {
                        ["dataLength"] = data?.Length ?? 0,
                        ["userId"] = userId
                    });
                onSuccess?.Invoke();
            }
        }

        /// <summary>
        /// Retrieves context from Cognee's search endpoint (vector + graph).
        /// </summary>
        public void SearchMemory(string query, Action<string> onSuccess, Action<string> onError)
        {
            StartCoroutine(SearchMemoryCoroutine(query, onSuccess, onError));
        }

        private IEnumerator SearchMemoryCoroutine(string query, Action<string> onSuccess, Action<string> onError)
        {
            string logSource = nameof(CogneeMemoryService) + ".SearchMemory";
            OnDiagnostic?.Invoke(_debug, _stageCogneeSearch, _statusStart,
                "Cognee search started.",
                logSource,
                new Dictionary<string, object> { ["queryLength"] = query?.Length ?? 0 });

            // Example GET request for search
            string uri = $"{CogneeEndpoint}/search?query={UnityWebRequest.EscapeURL(query)}";

            using (UnityWebRequest request = UnityWebRequest.Get(uri))
            {
                request.timeout = 2; // Fail fast if cognee is not running
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    OnDiagnostic?.Invoke(_warning, _stageCogneeSearch, _statusFallback,
                        $"Cognee search failed: {request.error}. Returning empty.",
                        logSource,
                        new Dictionary<string, object>
                        {
                            ["endpoint"] = CogneeEndpoint,
                            ["error"] = request.error
                        });
                    // Instead of failing the task and spamming the console, just return empty gracefully
                    onSuccess?.Invoke("");
                }
                else
                {
                    string resultText = request.downloadHandler.text;
                    OnDiagnostic?.Invoke(_info, _stageCogneeSearch, _statusSuccess,
                        "Cognee search completed.",
                        logSource,
                        new Dictionary<string, object>
                        {
                            ["resultLength"] = resultText?.Length ?? 0
                        });
                    onSuccess?.Invoke(resultText);
                }
            }
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        public Task AddMemoryAsync(string userId, string data)
        {
            var tcs = new TaskCompletionSource<bool>();
            AddMemory(userId, data, () => tcs.SetResult(true), error => tcs.SetException(new Exception(error)));
            return tcs.Task;
        }

        public Task<string> SearchMemoryAsync(string query)
        {
            var tcs = new TaskCompletionSource<string>();
            SearchMemory(query, result => tcs.SetResult(result), error => tcs.SetException(new Exception(error)));
            return tcs.Task;
        }
    }
}
