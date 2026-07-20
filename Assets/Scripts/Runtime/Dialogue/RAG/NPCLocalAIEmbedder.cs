using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NPCSystem.Auth;
using NPCSystem.Character.NPC;
using NPCSystem.Character.Player;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Initialization;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Monitoring;
using NPCSystem.Network.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace NPCSystem.Dialogue.RAG
{
    [DefaultExecutionOrder(-2)]
    public class NPCLocalAIEmbedder : MonoBehaviour
    {
        [Header("LocalAI Embedding Endpoint")]
        [FormerlySerializedAs("host")]
        [SerializeField]
        string _host = "localhost";
        public string Host
        {
            get => _host;
            set => _host = value;
        }

        [FormerlySerializedAs("port")]
        [SerializeField]
        int _port = 8080;
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        [FormerlySerializedAs("apiKey")]
        [SerializeField]
        string _apiKey = "";
        public string ApiKey => _apiKey;

        [FormerlySerializedAs("model")]
        [SerializeField]
        string _model = "nomic-embed-text-v1.5";
        public string Model => _model;

        [Header("Settings")]
        [FormerlySerializedAs("numRetries")]
        [SerializeField]
        int _numRetries = 3;
        public int NumRetries
        {
            get => _numRetries;
            set => _numRetries = value;
        }

        NPCFlowLogger _logger;

        void Awake()
        {
            _logger = NPCFlowLogger.FindOrCreate();
        }

        /// <summary>
        /// Embed a query string using LocalAI's /v1/embeddings endpoint.
        /// </summary>
        public async Task<List<float>> Embeddings(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<float>();

            string uri = $"http://{_host}:{_port}/v1/embeddings";

            _logger?.Log(
                NPCFlowStage.QdrantEmbedding,
                NPCFlowStatus.Start,
                NPCFlowLogLevel.Info,
                $"Embedding query ({query.Length} chars).",
                source: nameof(NPCLocalAIEmbedder),
                data: new Dictionary<string, object> { ["queryLength"] = query.Length, ["uri"] = uri }
            );

            for (int attempt = 0; attempt <= _numRetries; attempt++)
            {
                try
                {
                    var payload = new LocalAIEmbeddingRequest
                    {
                        model = string.IsNullOrWhiteSpace(Model) ? "default-embedding" : Model.Trim(),
                        input = query,
                    };

                    string json = JsonUtility.ToJson(payload);
                    string responseText = await SendEmbeddingRequestAsync(uri, json);

                    if (string.IsNullOrEmpty(responseText))
                    {
                        if (attempt < _numRetries)
                        {
                            _logger?.Log(
                                NPCFlowStage.QdrantEmbedding,
                                NPCFlowStatus.Warning,
                                NPCFlowLogLevel.Warning,
                                $"Empty embedding response (attempt {attempt + 1}/{_numRetries + 1}), retrying...",
                                source: nameof(NPCLocalAIEmbedder),
                                data: new Dictionary<string, object>
                                {
                                    ["attempt"] = attempt,
                                    ["maxAttempts"] = _numRetries,
                                }
                            );
                            await Task.Delay(500 * (attempt + 1));
                            continue;
                        }
                        _logger?.Log(
                            NPCFlowStage.QdrantEmbedding,
                            NPCFlowStatus.Error,
                            NPCFlowLogLevel.Error,
                            $"All retries exhausted — empty embedding response from {uri}",
                            source: nameof(NPCLocalAIEmbedder)
                        );
                        return new List<float>();
                    }

                    var response = JsonUtility.FromJson<LocalAIEmbeddingResponse>(responseText);

                    if (response?.data != null && response.data.Length > 0 && response.data[0].embedding != null)
                    {
                        int dims = response.data[0].embedding.Length;
                        _logger?.Log(
                            NPCFlowStage.QdrantEmbedding,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Info,
                            $"Embedding generated ({dims} dims).",
                            source: nameof(NPCLocalAIEmbedder),
                            durationMs: 0,
                            data: new Dictionary<string, object> { ["dimensions"] = dims, ["attempt"] = attempt }
                        );
                        return new List<float>(response.data[0].embedding);
                    }

                    _logger?.Log(
                        NPCFlowStage.QdrantEmbedding,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        $"Unexpected response format from {uri}",
                        source: nameof(NPCLocalAIEmbedder)
                    );
                    return new List<float>();
                }
                catch (Exception ex)
                {
                    _logger?.Log(
                        NPCFlowStage.QdrantEmbedding,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        $"Exception (attempt {attempt + 1}/{_numRetries + 1}): {ex.Message}",
                        source: nameof(NPCLocalAIEmbedder),
                        data: new Dictionary<string, object>
                        {
                            ["exceptionType"] = ex.GetType().Name,
                            ["attempt"] = attempt,
                        }
                    );
                    if (attempt < _numRetries)
                    {
                        await Task.Delay(500 * (attempt + 1));
                        continue;
                    }
                }
            }

            return new List<float>();
        }

        protected virtual async Task<string> SendEmbeddingRequestAsync(string uri, string json)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(uri, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(_apiKey))
                    request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (
                    request.result == UnityWebRequest.Result.ConnectionError
                    || request.result == UnityWebRequest.Result.ProtocolError
                )
                {
                    _logger?.Log(
                        NPCFlowStage.QdrantEmbedding,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        $"Request failed: {request.error}",
                        source: nameof(NPCLocalAIEmbedder),
                        data: new Dictionary<string, object>
                        {
                            ["responseCode"] = (int)request.responseCode,
                            ["uri"] = uri,
                        }
                    );
                    return null;
                }

                return request.downloadHandler.text;
            }
        }

        [Serializable]
        class LocalAIEmbeddingRequest
        {
            public string model;
            public string input;
        }

        [Serializable]
        class LocalAIEmbeddingResponse
        {
            public LocalAIEmbeddingData[] data;
        }

        [Serializable]
        class LocalAIEmbeddingData
        {
            public float[] embedding;
            public int index;
        }
    }
}
