using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Networking;

namespace NPCSystem
{
    public class QdrantRAGService : MonoBehaviour
    {
        [Title("Qdrant Endpoint")]
        [HelpBox("Qdrant is the primary vector database for NPC knowledge. Uses NPCLocalAIEmbedder for query encoding.", MessageMode.Log, drawAbove: true)]
        public string qdrantUrl = "http://localhost:6333";

        public string collectionName = "npc_knowledge";

        [Title("Embedder")]
        [HelpBox("Assign the NPCLocalAIEmbedder used to encode queries before searching Qdrant.", MessageMode.Log)]
        public NPCLocalAIEmbedder embedder;

        [SerializeField, ReadOnly]
        string inspectorStatus = "Not validated yet.";

        [ShowInInspector]
        string SearchEndpointPreview => BuildSearchEndpoint();

        [Button("Validate Qdrant Inspector Settings")]
        void ValidateInspectorSettings()
        {
            inspectorStatus = HasValidQdrantUrl() && HasValidCollectionName()
                ? $"Qdrant settings look valid: {BuildSearchEndpoint()}"
                : "Qdrant settings are invalid. Check URL and collection name.";
        }

        [Button("Log Qdrant Configuration")]
        void LogQdrantConfiguration()
        {
            NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.ConfigurationValidation, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                "Qdrant inspector configuration logged.",
                source: nameof(QdrantRAGService),
                data: new Dictionary<string, object>
                {
                    ["qdrantUrl"] = qdrantUrl,
                    ["collectionName"] = collectionName,
                    ["searchEndpoint"] = BuildSearchEndpoint()
                });
            inspectorStatus = $"Logged Qdrant configuration: {BuildSearchEndpoint()}";
        }

        void Awake()
        {
            NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Success, NPCFlowLogLevel.Debug,
                "QdrantRAGService initialized.",
                source: nameof(QdrantRAGService),
                data: new Dictionary<string, object>
                {
                    ["qdrantUrl"] = qdrantUrl,
                    ["collectionName"] = collectionName
                });
        }

        [System.Serializable]
        private class QdrantSearchRequest
        {
            public List<float> vector;
            public int limit = 3;
            public bool with_payload = true;
        }

        [System.Serializable]
        private class QdrantSearchResponse
        {
            public List<QdrantPoint> result;
        }

        [System.Serializable]
        private class QdrantPoint
        {
            public float score;
            public QdrantPayload payload;
        }

        [System.Serializable]
        private class QdrantPayload
        {
            public string text;
        }

        public async Task<string> SearchMemoryAsync(string query, int limit = 3, string requestId = null, string npcSlug = null)
        {
            using var scope = NPCFlowScope.Start(NPCFlowLogger.Instance, NPCFlowStage.QdrantSearch, source: nameof(QdrantRAGService), requestId: requestId, npcSlug: npcSlug, data: new Dictionary<string, object>
            {
                ["endpoint"] = qdrantUrl,
                ["collection"] = collectionName,
                ["limit"] = limit
            });

            if (embedder == null)
            {
                embedder = FindAnyObjectByType<NPCLocalAIEmbedder>(FindObjectsInactive.Include);
                if (embedder == null)
                {
                    NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.QdrantSearch, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                        "NPCLocalAIEmbedder not found; Qdrant search skipped.",
                        source: nameof(QdrantRAGService), requestId: requestId, npcSlug: npcSlug);
                    scope.Skipped("Embedder missing; Qdrant skipped.");
                    return string.Empty;
                }
            }

            try
            {
                List<float> queryVector = await embedder.Embeddings(query);
                if (queryVector == null || queryVector.Count == 0)
                {
                    NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.QdrantEmbedding, NPCFlowStatus.Fallback, NPCFlowLogLevel.Warning,
                        "Failed to generate embeddings for query.",
                        source: nameof(QdrantRAGService), requestId: requestId, npcSlug: npcSlug,
                        data: new Dictionary<string, object> { ["queryLength"] = query?.Length ?? 0 });
                    scope.Fallback("Embeddings generation failed.");
                    return string.Empty;
                }

                QdrantSearchRequest requestBody = new QdrantSearchRequest
                {
                    vector = queryVector,
                    limit = limit,
                    with_payload = true
                };

                string jsonRequest = JsonUtility.ToJson(requestBody);
                string searchEndpoint = $"{qdrantUrl}/collections/{collectionName}/points/search";

                string responseText = await SendSearchRequestAsync(searchEndpoint, jsonRequest);

                if (responseText == null)
                {
                    NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.QdrantSearch, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                        "Qdrant request failed.",
                        source: nameof(QdrantRAGService), requestId: requestId, npcSlug: npcSlug,
                        data: new Dictionary<string, object>
                        {
                            ["endpoint"] = searchEndpoint,
                            ["vectorLength"] = queryVector.Count
                        });
                    scope.Error(null, "Qdrant request failed.", new Dictionary<string, object>
                    {
                        ["endpoint"] = searchEndpoint,
                        ["vectorLength"] = queryVector.Count
                    });
                    return string.Empty;
                }

                QdrantSearchResponse response = JsonUtility.FromJson<QdrantSearchResponse>(responseText);

                if (response != null && response.result != null && response.result.Count > 0)
                {
                    List<string> results = new List<string>();
                    foreach (var point in response.result)
                    {
                        if (point.payload != null && !string.IsNullOrEmpty(point.payload.text))
                        {
                            results.Add(point.payload.text);
                        }
                    }
                    scope.Success("Qdrant results retrieved.", new Dictionary<string, object>
                    {
                        ["resultCount"] = results.Count,
                        ["vectorLength"] = queryVector.Count,
                        ["endpoint"] = searchEndpoint
                    });
                    return string.Join("\n", results);
                }
                scope.Skipped("Qdrant returned empty.", new Dictionary<string, object>
                {
                    ["vectorLength"] = queryVector.Count,
                    ["endpoint"] = searchEndpoint
                });
            }
            catch (Exception e)
            {
                NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.QdrantSearch, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    $"Exception during Qdrant search: {e.Message}",
                    source: nameof(QdrantRAGService), requestId: requestId, npcSlug: npcSlug,
                    data: new Dictionary<string, object>
                    {
                        ["exceptionType"] = e.GetType().Name,
                        ["exceptionMessage"] = e.Message
                    });
                scope.Error(e, "Exception during Qdrant search.");
            }

            return string.Empty;
        }

        protected virtual async Task<string> SendSearchRequestAsync(string endpoint, string json)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[QdrantRAGService] Request failed: {request.error}");
                    return null;
                }

                return request.downloadHandler.text;
            }
        }

        public bool HasValidQdrantUrl()
        {
            return !string.IsNullOrWhiteSpace(qdrantUrl)
                && (qdrantUrl.StartsWith("http://") || qdrantUrl.StartsWith("https://"));
        }

        public bool HasValidCollectionName()
        {
            return !string.IsNullOrWhiteSpace(collectionName) && !collectionName.Contains(" ");
        }

        public string BuildSearchEndpoint()
        {
            string baseUrl = string.IsNullOrWhiteSpace(qdrantUrl) ? "<missing-qdrant-url>" : qdrantUrl.TrimEnd('/');
            string collection = string.IsNullOrWhiteSpace(collectionName) ? "<missing-collection>" : collectionName;
            return $"{baseUrl}/collections/{collection}/points/search";
        }
    }
}
