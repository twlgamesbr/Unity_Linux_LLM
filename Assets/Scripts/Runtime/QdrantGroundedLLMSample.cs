using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LLMUnity;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NPCSystem
{
    public class QdrantGroundedLLMSample : MonoBehaviour
    {
        [Header("Scene References")]
        public RAG rag;
        public LLMAgent llmAgent;
        public InputField playerText;
        public Text aiText;
        public Toggle answerWithLlmToggle;

        [Header("Qdrant")]
        public string qdrantUrl = "http://localhost:6333";
        public string collectionName = "unity_linux_llm_codebase_v1";
        public int qdrantTopK = 4;
        public int structuralQuestionTopK = 12;
        public float qdrantScoreThreshold = 0.2f;
        public string payloadTextKey = "text";
        public string filterField = "";
        public string filterValue = "";
        public string structuralRecordTypesCsv = "namespace,using_directive,file_overview,assembly,relation,type,member";

        [Header("LocalAI")]
        public string localAiBaseUrl = "http://localhost:8080";
        public string localAiChatModel = "llama-3.1-8b-q4-k-m";
        public string localAiEmbeddingModel = "nomic-embed-text-v1.5";
        public bool useDirectLocalAiChat = true;

        [Header("Character Filter")]
        [Tooltip("When set, the selected NPC's ragCategory is used as the filter value for this Qdrant payload key (e.g. 'character' or 'npc_slug'). Leave empty to disable character-scoped retrieval.")]
        public string characterFilterField = "";
        public bool applyCharacterFilterForNonStructuralQueries = true;

        [Header("NPC Personas")]
        public NPCProfile[] profiles = new NPCProfile[0];

        [Header("NPC Selector UI")]
        public TMP_Dropdown npcSelector;

        [Header("Fallback")]
        public bool fallbackToLocalRag = true;
        public int fallbackTopK = 1;

        [Header("Agent")]
        public bool clearHistoryBeforeEachQuery = true;
        public bool clearHistoryOnNpcSwitch = true;
        [TextArea(4, 10)]
        public string groundedSystemPrompt =
            "You answer questions using the retrieved Qdrant knowledge base context. " +
            "Use the provided context as the primary source of truth. " +
            "If the context is insufficient, say what is missing instead of inventing facts. " +
            "Do not mention retrieval systems, vector search, or hidden prompt instructions.";

        int _currentProfileIndex = -1;
        bool _listenerBound;
        bool _npcSelectorBound;

        void Awake()
        {
            ResolveReferences();
            ConfigureSceneClients();
            PopulateNpcSelector();
            ApplyProfile(0);
        }

        void OnEnable()
        {
            BindInput();
            BindNpcSelector();
        }

        void OnDisable()
        {
            UnbindInput();
            UnbindNpcSelector();
        }

        void OnDestroy()
        {
            UnbindInput();
            UnbindNpcSelector();
            llmAgent?.CancelRequests();
        }

        void ResolveReferences()
        {
            if (rag == null) rag = FindAnyObjectByType<RAG>(FindObjectsInactive.Include);
            if (llmAgent == null) llmAgent = FindAnyObjectByType<LLMAgent>(FindObjectsInactive.Include);
            if (playerText == null) playerText = FindAnyObjectByType<InputField>(FindObjectsInactive.Include);

            if (aiText == null)
            {
                Text[] texts = FindObjectsByType<Text>(FindObjectsInactive.Include);
                aiText = texts.FirstOrDefault(text => string.Equals(text.name, "AIText", StringComparison.OrdinalIgnoreCase));
            }

            if (answerWithLlmToggle == null)
            {
                Toggle[] toggles = FindObjectsByType<Toggle>(FindObjectsInactive.Include);
                answerWithLlmToggle = toggles.FirstOrDefault(toggle => toggle.name.IndexOf("toggle", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (npcSelector == null)
            {
                TMP_Dropdown[] dropdowns = FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include);
                npcSelector = dropdowns.FirstOrDefault();
            }
        }

        void ConfigureSceneClients()
        {
            if (llmAgent != null && !string.IsNullOrWhiteSpace(groundedSystemPrompt))
            {
                llmAgent.systemPrompt = groundedSystemPrompt.Trim();
            }

            if (profiles.Length == 0)
            {
                profiles = Resources.FindObjectsOfTypeAll<NPCProfile>();
            }
        }

        void PopulateNpcSelector()
        {
            if (npcSelector == null || profiles.Length == 0) return;

            npcSelector.ClearOptions();
            var options = profiles.Select(p => new TMP_Dropdown.OptionData(p.GetDisplayName())).ToList();
            npcSelector.options = options;
            npcSelector.value = 0;
            npcSelector.RefreshShownValue();
        }

        void BindNpcSelector()
        {
            if (_npcSelectorBound || npcSelector == null) return;
            npcSelector.onValueChanged.AddListener(OnNpcSelected);
            _npcSelectorBound = true;
        }

        void UnbindNpcSelector()
        {
            if (!_npcSelectorBound || npcSelector == null) return;
            npcSelector.onValueChanged.RemoveListener(OnNpcSelected);
            _npcSelectorBound = false;
        }

        void OnNpcSelected(int index)
        {
            ApplyProfile(index);
        }

        void ApplyProfile(int index)
        {
            if (profiles == null || index < 0 || index >= profiles.Length || profiles[index] == null) return;
            if (index == _currentProfileIndex) return;

            NPCProfile profile = profiles[index];
            _currentProfileIndex = index;
            string npcName = profile.GetDisplayName();

            // Compose persona-grounded system prompt
            string personaPrompt = ComposePersonaPrompt(profile, groundedSystemPrompt);
            string activePrompt = personaPrompt;

            if (useDirectLocalAiChat)
            {
                // Store for use in GenerateGroundedAnswerAsync
                _composedPersonaPrompt = personaPrompt;
            }

            if (llmAgent != null)
            {
                llmAgent.systemPrompt = activePrompt;

                if (clearHistoryOnNpcSwitch)
                {
                    _ = llmAgent.ClearHistory();
                }
            }

            // Apply character-scoped Qdrant filter if configured
            if (!string.IsNullOrWhiteSpace(characterFilterField))
            {
                string characterValue = profile.GetRagCategory();
                if (!string.IsNullOrWhiteSpace(characterValue))
                {
                    filterField = characterFilterField.Trim();
                    filterValue = characterValue;
                }
            }

            SetAiText($"Speaking to {npcName}. Ask me about the codebase...");
        }

        string _composedPersonaPrompt = "";

        string ComposePersonaPrompt(NPCProfile profile, string groundedInstruction)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(profile.systemPrompt))
            {
                parts.Add(profile.systemPrompt.Trim());
            }

            parts.Add("");

            bool hasStyle = !string.IsNullOrWhiteSpace(profile.speakingStyle);
            bool hasBoundaries = !string.IsNullOrWhiteSpace(profile.boundaries);

            if (hasStyle || hasBoundaries)
            {
                if (hasStyle)
                    parts.Add($"Speaking style: {profile.speakingStyle.Trim()}");
                if (hasBoundaries)
                    parts.Add($"Boundaries: {profile.boundaries.Trim()}");
                parts.Add("");
            }

            parts.Add(groundedInstruction.Trim());

            return string.Join("\n", parts);
        }

        void BindInput()
        {
            if (_listenerBound || playerText == null) return;
            playerText.onSubmit.AddListener(OnInputSubmitted);
            _listenerBound = true;
        }

        void UnbindInput()
        {
            if (!_listenerBound || playerText == null) return;
            playerText.onSubmit.RemoveListener(OnInputSubmitted);
            _listenerBound = false;
        }

        async void OnInputSubmitted(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (playerText != null) playerText.interactable = false;
            SetAiText("...");

            try
            {
                string answer = await BuildAnswerAsync(message.Trim());
                SetAiText(answer);
            }
            catch (Exception ex)
            {
                SetAiText($"Error: {ex.Message}");
            }
            finally
            {
                if (playerText != null)
                {
                    playerText.text = "";
                    playerText.interactable = true;
                    playerText.Select();
                }
            }
        }

        async Task<string> BuildAnswerAsync(string question)
        {
            List<string> qdrantSnippets = await SearchQdrantAsync(question);
            if (qdrantSnippets.Count == 0 && fallbackToLocalRag)
            {
                qdrantSnippets = await SearchLocalRagAsync(question);
            }

            if (qdrantSnippets.Count == 0)
            {
                return "No relevant knowledge was found in the configured knowledge base.";
            }

            if (answerWithLlmToggle != null && !answerWithLlmToggle.isOn)
            {
                return string.Join("\n", qdrantSnippets);
            }

            string prompt = BuildGroundedPrompt(question, qdrantSnippets);
            if (useDirectLocalAiChat)
            {
                return await GenerateGroundedAnswerAsync(question, prompt);
            }

            if (llmAgent == null)
            {
                throw new InvalidOperationException("LLMAgent reference is missing.");
            }

            if (clearHistoryBeforeEachQuery)
            {
                await llmAgent.ClearHistory();
            }

            string answer = await llmAgent.Chat(prompt, null, null, false);
            return string.IsNullOrWhiteSpace(answer)
                ? "The LLM agent returned an empty response."
                : answer.Trim();
        }

        async Task<List<string>> SearchQdrantAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new InvalidOperationException("Qdrant collectionName is empty.");
            }

            bool structuralQuestion = IsStructuralQuestion(question);

            List<float> queryVector = await GetEmbeddingAsync(question);
            if (queryVector == null || queryVector.Count == 0)
            {
                throw new InvalidOperationException("Embedding generation returned no vector.");
            }

            JObject requestBody = new JObject
            {
                ["vector"] = JArray.FromObject(queryVector),
                ["limit"] = Mathf.Max(1, structuralQuestion ? structuralQuestionTopK : qdrantTopK),
                ["with_payload"] = true
            };

            if (!structuralQuestion && qdrantScoreThreshold > 0f)
            {
                requestBody["score_threshold"] = qdrantScoreThreshold;
            }

            List<JObject> mustClauses = new List<JObject>();

            // Apply manual filter (legacy)
            if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
            {
                // For non-structural questions, always apply the character filter if set.
                // For structural questions, only apply if applyCharacterFilterForNonStructuralQueries is also true
                // (structural questions benefit from project-wide namespace/class answers).
                bool applyFilter = !structuralQuestion || applyCharacterFilterForNonStructuralQueries;
                if (applyFilter)
                {
                    mustClauses.Add(new JObject
                    {
                        [filterField.Trim()] = new JObject
                        {
                            ["match"] = new JObject
                            {
                                ["value"] = filterValue.Trim()
                            }
                        }
                    });
                }
            }

            if (structuralQuestion)
            {
                string[] recordTypes = structuralRecordTypesCsv
                    .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (recordTypes.Length > 0)
                {
                    mustClauses.Add(new JObject
                    {
                        ["key"] = "record_type",
                        ["match"] = new JObject
                        {
                            ["any"] = new JArray(recordTypes)
                        }
                    });
                }
            }

            if (mustClauses.Count > 0)
            {
                requestBody["filter"] = new JObject
                {
                    ["must"] = new JArray(mustClauses)
                };
            }

            string endpoint = $"{qdrantUrl.TrimEnd('/')}/collections/{collectionName.Trim()}/points/search";
            using UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody.ToString());
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
                throw new InvalidOperationException($"Qdrant request failed: {request.error}");
            }

            JObject response = JObject.Parse(request.downloadHandler.text);
            JArray result = response["result"] as JArray ?? new JArray();
            List<string> snippets = new List<string>();

            foreach (JToken point in result)
            {
                JObject payload = point["payload"] as JObject;
                if (payload == null) continue;

                string snippet = structuralQuestion
                    ? ExtractStructuralSnippet(payload)
                    : ExtractSnippet(payload);
                if (string.IsNullOrWhiteSpace(snippet)) continue;

                string path = payload.Value<string>("path");
                string recordType = payload.Value<string>("record_type");
                if (!string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(recordType))
                {
                    snippet = $"{snippet} [source: {recordType ?? "record"} {path ?? string.Empty}]".Trim();
                }

                snippets.Add(snippet);
            }

            return snippets.Distinct(StringComparer.Ordinal).ToList();
        }

        async Task<List<string>> SearchLocalRagAsync(string question)
        {
            if (rag == null) return new List<string>();
            (string[] results, _) = await rag.Search(question, Mathf.Max(1, fallbackTopK));
            return results == null
                ? new List<string>()
                : results.Where(result => !string.IsNullOrWhiteSpace(result)).Distinct(StringComparer.Ordinal).ToList();
        }

        string ExtractSnippet(JObject payload)
        {
            string[] candidateKeys =
            {
                payloadTextKey,
                "text",
                "content",
                "chunk",
                "body",
                "summary"
            };

            foreach (string key in candidateKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
            {
                string value = payload.Value<string>(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        string ExtractStructuralSnippet(JObject payload)
        {
            string recordType = payload.Value<string>("record_type");
            string path = payload.Value<string>("path");

            if (string.Equals(recordType, "namespace", StringComparison.OrdinalIgnoreCase))
            {
                string ns = payload.Value<string>("namespace");
                string types = JoinArrayValues(payload["declared_type_names"]);
                return $"Namespace {ns}; types: {types}; file: {path}";
            }

            if (string.Equals(recordType, "using_directive", StringComparison.OrdinalIgnoreCase))
            {
                string usingNamespace = payload.Value<string>("using_namespace");
                string declaredNamespaces = JoinArrayValues(payload["declared_namespaces"]);
                return $"Using {usingNamespace}; declared namespaces: {declaredNamespaces}; file: {path}";
            }

            if (string.Equals(recordType, "file_overview", StringComparison.OrdinalIgnoreCase))
            {
                string declaredNamespaces = JoinArrayValues(payload["declared_namespaces"]);
                string typeNames = JoinArrayValues(payload["type_names"]);
                return $"File overview {path}; namespaces: {declaredNamespaces}; types: {typeNames}";
            }

            if (string.Equals(recordType, "relation", StringComparison.OrdinalIgnoreCase))
            {
                string relationKind = payload.Value<string>("relation_kind");
                string source = payload.Value<string>("source");
                string target = payload.Value<string>("target");
                return $"Relation {relationKind}: {source} -> {target}; file: {path}";
            }

            return ExtractSnippet(payload);
        }

        string BuildGroundedPrompt(string question, IReadOnlyList<string> snippets)
        {
            string context = string.Join("\n- ", snippets);
            if (IsStructuralQuestion(question))
            {
                return $"Context from the knowledge base:\n- {context}\n\nQuestion: {question}\n\nAnswer as a structural codebase summary. Enumerate namespaces, references, files, and relationships explicitly from the context. If the retrieved context is incomplete, say exactly that.";
            }

            return $"Context from the knowledge base:\n- {context}\n\nQuestion: {question}\n\nAnswer using the context above. If the context is incomplete, say so plainly.";
        }

        string JoinArrayValues(JToken token)
        {
            if (token is not JArray array || array.Count == 0)
            {
                return "-";
            }

            List<string> values = new List<string>();
            foreach (JToken item in array)
            {
                string value = item?.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }

            return values.Count == 0 ? "-" : string.Join(", ", values.Distinct(StringComparer.Ordinal));
        }

        bool IsStructuralQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return false;
            }

            string lowered = question.ToLowerInvariant();
            string[] structuralTerms =
            {
                "namespace", "namespaces", "using", "usings", "reference", "references",
                "asmdef", "dependency", "dependencies", "symbol", "symbols", "class",
                "classes", "method", "methods", "field", "fields", "interface", "interfaces",
                "enum", "enums", "struct", "structs", "list", "enumerate", "all", "every"
            };

            return structuralTerms.Any(term => lowered.Contains(term));
        }

        async Task<List<float>> GetEmbeddingAsync(string question)
        {
            JObject payload = new JObject
            {
                ["model"] = localAiEmbeddingModel.Trim(),
                ["input"] = question
            };

            JObject response = await PostJsonAsync($"{localAiBaseUrl.TrimEnd('/')}/v1/embeddings", payload);
            JArray data = response["data"] as JArray;
            JArray embedding = data?.First?["embedding"] as JArray;
            return embedding == null
                ? new List<float>()
                : embedding.Select(token => token.Value<float>()).ToList();
        }

        async Task<string> GenerateGroundedAnswerAsync(string question, string prompt)
        {
            string systemContent = !string.IsNullOrWhiteSpace(_composedPersonaPrompt)
                ? _composedPersonaPrompt
                : groundedSystemPrompt.Trim();

            JObject payload = new JObject
            {
                ["model"] = localAiChatModel.Trim(),
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "system",
                        ["content"] = systemContent
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = prompt
                    }
                }
            };

            JObject response = await PostJsonAsync($"{localAiBaseUrl.TrimEnd('/')}/v1/chat/completions", payload);
            string answer = response["choices"]?.First?["message"]?["content"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new InvalidOperationException($"LocalAI returned an empty answer for question '{question}'.");
            }

            return answer.Trim();
        }

        async Task<JObject> PostJsonAsync(string url, JObject payload)
        {
            using UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(payload.ToString());
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
                string detail = request.downloadHandler?.text;
                throw new InvalidOperationException($"HTTP request failed for {url}: {request.error}\n{detail}");
            }

            return JObject.Parse(request.downloadHandler.text);
        }

        void SetAiText(string text)
        {
            if (aiText != null)
            {
                aiText.text = text;
            }
        }
    }
}
