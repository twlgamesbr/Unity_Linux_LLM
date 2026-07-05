using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NPCSystem
{
    [DefaultExecutionOrder(-2)]
    public class NPCLocalAIEmbedder : MonoBehaviour
    {
        [Header("LocalAI Embedding Endpoint")]
        public string host = "localhost";
        public int port = 8080;
        public string apiKey = "";
        public string model = "default-embedding";

        [Header("Settings")]
        public int numRetries = 3;

        /// <summary>
        /// Embed a query string using LocalAI's /v1/embeddings endpoint.
        /// </summary>
        public async Task<List<float>> Embeddings(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<float>();

            string uri = $"http://{host}:{port}/v1/embeddings";

            for (int attempt = 0; attempt <= numRetries; attempt++)
            {
                try
                {
                    var payload = new LocalAIEmbeddingRequest
                    {
                        model = string.IsNullOrWhiteSpace(model) ? "default-embedding" : model.Trim(),
                        input = query
                    };

                    string json = JsonUtility.ToJson(payload);
                    string responseText = await SendEmbeddingRequestAsync(uri, json);

                    if (string.IsNullOrEmpty(responseText))
                    {
                        if (attempt < numRetries)
                        {
                            await Task.Delay(500 * (attempt + 1));
                            continue;
                        }
                        return new List<float>();
                    }

                    var response = JsonUtility.FromJson<LocalAIEmbeddingResponse>(responseText);

                    if (response?.data != null && response.data.Length > 0 && response.data[0].embedding != null)
                    {
                        return new List<float>(response.data[0].embedding);
                    }

                    Debug.LogError($"[NPCLocalAIEmbedder] Unexpected response format from {uri}");
                    return new List<float>();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NPCLocalAIEmbedder] Exception (attempt {attempt + 1}/{numRetries + 1}): {ex.Message}");
                    if (attempt < numRetries)
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
                if (!string.IsNullOrEmpty(apiKey))
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[NPCLocalAIEmbedder] Request failed: {request.error}");
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
