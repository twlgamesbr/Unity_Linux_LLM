using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EditorAttributes;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace NPCSystem
{
    [Serializable]
    public class SupabaseDialogueRepository : MonoBehaviour
    {
        [Title("Supabase Dialogue Repository")]
        [HelpBox(
            "Persists NPC dialogue history to Supabase via PostgREST instead of local file-based NPCHistoryStore. "
                + "Uses the player's access_token from PlayerAuthService for RLS-scoped access. "
                + "When the Supabase backend is unreachable, falls back to the local file store.",
            MessageMode.Log,
            drawAbove: true
        )]
        [Header("Supabase REST")]
        [SerializeField]
        string restUrl = "http://localhost:8000";

        [SerializeField]
        string anonKey = "dev-local-anon-key";

        [Header("References")]
        public PlayerAuthService authService;

        [Header("Behaviour")]
        [SerializeField]
        float requestTimeoutSeconds = 10f;

        [Header("Debug")]
        [SerializeField, ReadOnly]
        string lastStatus = "Idle";

        [SerializeField, ReadOnly]
        string lastOperation = string.Empty;

        [SerializeField, ReadOnly]
        long lastOperationDurationMs;

        string _lastSessionId;
        NPCFlowLogger _logger;

        void Awake()
        {
            _logger = NPCFlowLogger.FindOrCreate();
        }

        public bool IsConfigured =>
            authService != null
            && authService.IsAuthenticated
            && !string.IsNullOrWhiteSpace(restUrl)
            && !string.IsNullOrWhiteSpace(anonKey);

        string BearerToken =>
            IsConfigured ? authService.CurrentSession.sessionToken : null;

        static string ToJson(Dictionary<string, object> dict)
        {
            return JObject.FromObject(dict).ToString(Newtonsoft.Json.Formatting.None);
        }

        static JArray ParseJsonArray(string json)
        {
            return JArray.Parse(json);
        }

        static string UnescapeJsonString(string json)
        {
            return JToken.Parse(json).ToString();
        }

        // \u2500\u2500 Load \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        public async Task<List<DialogueEntry>> LoadHistoryAsync(string npcSlug)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(npcSlug))
                return null;

            lastOperation = $"LoadHistoryAsync({npcSlug})";
            long startedAt = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

            try
            {
                // 1. Find the most recent active session for this player+NPC
                string sessionUrl = $"{restUrl.TrimEnd('/')}/rest/v1/rpc/find_or_create_dialogue_session";
                var sessionBody = new Dictionary<string, object>
                {
                    ["p_player_id"] = authService.CurrentSession.playerId,
                    ["p_npc_slug"] = npcSlug,
                };
                string sessionJson = ToJson(sessionBody);

                string sessionId = await PostRpcAndGetStringAsync(
                    sessionUrl,
                    sessionJson
                );

                if (string.IsNullOrWhiteSpace(sessionId))
                    return null;

                _lastSessionId = sessionId;

                // 2. Load turns for this session
                string turnsUrl = $"{restUrl.TrimEnd('/')}/rest/v1/dialogue_turns"
                    + $"?session_id=eq.{sessionId}"
                    + "&order=created_at.asc"
                    + "&select=role,content,created_at";

                string turnsJson = await GetRestJsonAsync(turnsUrl);

                if (string.IsNullOrWhiteSpace(turnsJson) || turnsJson == "[]")
                    return new List<DialogueEntry>();

                // Parse the JSON array of turns into DialogueEntries
                JArray turns = ParseJsonArray(turnsJson);
                var entries = new List<DialogueEntry>(turns.Count);
                foreach (JToken turn in turns)
                {
                    JObject obj = turn as JObject;
                    if (obj == null) continue;

                    string role = obj.Value<string>("role");
                    string content = obj.Value<string>("content");
                    if (string.IsNullOrWhiteSpace(role) || content == null)
                        continue;

                    entries.Add(new DialogueEntry(role, content));
                }

                lastStatus = $"Loaded {entries.Count} turns from session {sessionId} for NPC '{npcSlug}'.";
                Log(NPCFlowStage.HistoryLoad, NPCFlowStatus.Success, lastStatus);

                return entries;
            }
            catch (Exception ex)
            {
                lastStatus = $"Load failed for NPC '{npcSlug}': {ex.Message}";
                Log(NPCFlowStage.HistoryLoad, NPCFlowStatus.Error, lastStatus + $" ({ex.GetType().Name})");
                return null;
            }
            finally
            {
                lastOperationDurationMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - startedAt;
            }
        }

        // \u2500\u2500 Save \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        public async Task<bool> SaveTurnAsync(
            string npcSlug,
            string role,
            string content
        )
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(npcSlug))
                return false;

            lastOperation = $"SaveTurnAsync({npcSlug}, {role})";
            long startedAt = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

            try
            {
                // Ensure session exists
                if (string.IsNullOrWhiteSpace(_lastSessionId))
                {
                    string sessionUrl = $"{restUrl.TrimEnd('/')}/rest/v1/rpc/find_or_create_dialogue_session";
                    var sessionBody = new Dictionary<string, object>
                    {
                        ["p_player_id"] = authService.CurrentSession.playerId,
                        ["p_npc_slug"] = npcSlug,
                    };
                    string sessionId = await PostRpcAndGetStringAsync(
                        sessionUrl,
                        ToJson(sessionBody)
                    );
                    if (string.IsNullOrWhiteSpace(sessionId))
                        return false;
                    _lastSessionId = sessionId;
                }

                // POST the turn
                string turnsUrl = $"{restUrl.TrimEnd('/')}/rest/v1/dialogue_turns";
                var turnBody = new Dictionary<string, object>
                {
                    ["session_id"] = _lastSessionId,
                    ["player_id"] = authService.CurrentSession.playerId,
                    ["role"] = role,
                    ["content"] = content,
                };

                await PostRestJsonAsync(turnsUrl, ToJson(turnBody));

                lastStatus = $"Saved {role} turn for NPC '{npcSlug}' (session {_lastSessionId}).";
                Log(NPCFlowStage.HistoryPersist, NPCFlowStatus.Success, lastStatus);
                return true;
            }
            catch (Exception ex)
            {
                lastStatus = $"Save failed for NPC '{npcSlug}': {ex.Message}";
                Log(NPCFlowStage.HistoryPersist, NPCFlowStatus.Error, lastStatus);
                return false;
            }
            finally
            {
                lastOperationDurationMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - startedAt;
            }
        }

        // \u2500\u2500 Delete \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        public async Task<bool> DeleteHistoryAsync(string npcSlug)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(npcSlug))
                return false;

            lastOperation = $"DeleteHistoryAsync({npcSlug})";
            long startedAt = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

            try
            {
                string closeUrl = $"{restUrl.TrimEnd('/')}/rest/v1/rpc/close_dialogue_session";
                var body = new Dictionary<string, object>
                {
                    ["p_player_id"] = authService.CurrentSession.playerId,
                    ["p_npc_slug"] = npcSlug,
                };
                await PostRpcAsync(closeUrl, ToJson(body));

                _lastSessionId = null;
                lastStatus = $"History deleted for NPC '{npcSlug}'.";
                Log(NPCFlowStage.HistoryPersist, NPCFlowStatus.Success, lastStatus);
                return true;
            }
            catch (Exception ex)
            {
                lastStatus = $"Delete failed for NPC '{npcSlug}': {ex.Message}";
                Log(NPCFlowStage.HistoryPersist, NPCFlowStatus.Error, lastStatus);
                return false;
            }
            finally
            {
                lastOperationDurationMs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - startedAt;
            }
        }

        // \u2500\u2500 HTTP helpers \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        async Task<string> GetRestJsonAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {BearerToken}");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new InvalidOperationException($"GET {url}: {request.error}");

            return request.downloadHandler.text;
        }

        async Task PostRestJsonAsync(string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {BearerToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new InvalidOperationException($"POST {url}: {request.error}");

            // 201 Created or 204 No Content are both success for REST
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                string errorText = !string.IsNullOrWhiteSpace(request.downloadHandler.text)
                    ? request.downloadHandler.text
                    : request.error;
                throw new InvalidOperationException($"POST {url} returned HTTP {request.responseCode}: {errorText}");
            }
        }

        async Task<string> PostRpcAndGetStringAsync(string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {BearerToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new InvalidOperationException($"RPC {url}: {request.error}");

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                string errorText = !string.IsNullOrWhiteSpace(request.downloadHandler.text)
                    ? request.downloadHandler.text
                    : request.error;
                throw new InvalidOperationException($"RPC {url} returned HTTP {request.responseCode}: {errorText}");
            }

            string text = request.downloadHandler.text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // If response starts with ", treat as JSON string literal
            if (text.StartsWith("\""))
            {
                return UnescapeJsonString(text);
            }

            return text;
        }

        async Task PostRpcAsync(string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.SetRequestHeader("apikey", anonKey);
            request.SetRequestHeader("Authorization", $"Bearer {BearerToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new InvalidOperationException($"RPC {url}: {request.error}");
        }

        // \u2500\u2500 Logging \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        void Log(NPCFlowStage stage, NPCFlowStatus status, string message)
        {
            _logger?.Log(stage, status, NPCFlowLogLevel.Debug, message,
                source: nameof(SupabaseDialogueRepository),
                data: new Dictionary<string, object>
                {
                    ["sessionId"] = _lastSessionId ?? string.Empty,
                    ["authPlayerId"] = authService?.CurrentSession?.playerId ?? string.Empty,
                });
        }
    }
}
