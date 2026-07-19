using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EditorAttributes;
using Newtonsoft.Json.Linq;
using UnityEngine;


using NPCSystem.Monitoring;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Dialogue.Session
{
    /// <summary>
    /// Tracks dialogue session analytics by calling Supabase RPCs after
    /// each turn is saved.  Exposes aggregated stats (turn counts,
    /// session summaries) that can be injected into NPC prompts.
    ///
    /// Hooks into <see cref="SupabaseDialogueRepository.SaveTurnAsync"/>
    /// to automatically update session turn_count metadata.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class SessionAnalyticsService : MonoBehaviour
    {
        [FoldoutGroup("References", true, nameof(_authService), nameof(_dialogueRepository))]
        [SerializeField]
        EditorAttributes.Void referencesGroup;

        [SerializeField, HideProperty]
        PlayerAuthService _authService;

        [SerializeField, HideProperty]
        SupabaseDialogueRepository _dialogueRepository;

        [FoldoutGroup("Behaviour", true, nameof(_enableServerAnalytics))]
        [SerializeField]
        EditorAttributes.Void behaviourGroup;

        [SerializeField, HideProperty, Tooltip(
            "When true (default), turn-count metadata is synced to "
            + "Supabase after each saved turn. When false, only local "
            + "state is tracked."
        )]
        bool _enableServerAnalytics = true;

        [FoldoutGroup("Debug", true, nameof(_activeSessionId), nameof(_currentNpcSlug))]
        [SerializeField]
        EditorAttributes.Void debugGroup;

        [SerializeField, HideProperty, ReadOnly]
        string _activeSessionId = string.Empty;

        [SerializeField, HideProperty, ReadOnly]
        string _currentNpcSlug = string.Empty;

        [SerializeField, HideProperty, ReadOnly]
        string _cachedAnalyticsPreview = string.Empty;

        // ── State ────────────────────────────────────────────────

        readonly Dictionary<string, int> _localTurnCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        SessionAnalyticsResult _cachedAnalytics;
        NPCFlowLogger _logger;

        // ── Events ───────────────────────────────────────────────

        /// <summary>
        /// Fired when session analytics are refreshed from the server.
        /// </summary>
        public event Action<string, SessionAnalyticsResult> OnAnalyticsRefreshed;

        // ── Properties ────────────────────────────────────────────

        /// <summary>
        /// Current active session ID (set after the first turn is saved).
        /// </summary>
        public string ActiveSessionId => _activeSessionId;

        /// <summary>
        /// The most recently fetched analytics result (may be null).
        /// </summary>
        public SessionAnalyticsResult CachedAnalytics => _cachedAnalytics;

        // ── Lifecycle ─────────────────────────────────────────────

        void Awake()
        {
            _logger = NPCFlowLogger.FindOrCreate();
        }

        void Reset()
        {
            _authService = GetComponent<PlayerAuthService>();
            if (_authService == null)
                _authService = FindAnyObjectByType<PlayerAuthService>(FindObjectsInactive.Include);

            _dialogueRepository = GetComponent<SupabaseDialogueRepository>();
            if (_dialogueRepository == null)
                _dialogueRepository = FindAnyObjectByType<SupabaseDialogueRepository>(
                    FindObjectsInactive.Include
                );
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Call after successfully saving a dialogue turn to Supabase.
        /// Increments the server-side turn_count via
        /// <c>update_session_turn_count</c> RPC.
        /// </summary>
        public async Task OnTurnSavedAsync(string sessionId, string npcSlug)
        {
            _activeSessionId = sessionId;
            _currentNpcSlug = npcSlug;

            // Local tracking
            if (!_localTurnCounts.ContainsKey(npcSlug))
                _localTurnCounts[npcSlug] = 0;
            _localTurnCounts[npcSlug]++;

            // Server sync
            if (_enableServerAnalytics && IsReady)
            {
                try
                {
                    JObject result = await _authService.SupabaseClient.Rpc<JObject>(
                        "update_session_turn_count",
                        new Dictionary<string, object>
                        {
                            ["p_session_id"] = Guid.Parse(sessionId),
                        }
                    );

                    bool success = result?.Value<bool>("success") ?? false;
                    if (success)
                    {
                        int serverCount = result.Value<int>("turn_count");
                        _localTurnCounts[npcSlug] = serverCount;

                        _logger?.Log(
                            NPCFlowStage.HistoryPersist,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Debug,
                            $"Session turn_count updated: {serverCount} (session {sessionId})",
                            source: nameof(SessionAnalyticsService),
                            data: new Dictionary<string, object>
                            {
                                ["sessionId"] = sessionId,
                                ["npcSlug"] = npcSlug,
                                ["turnCount"] = serverCount,
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Failed to update session turn_count: {ex.Message}",
                        source: nameof(SessionAnalyticsService)
                    );
                }
            }
        }

        /// <summary>
        /// Fetch session analytics from the server for the current player.
        /// Results are cached and returned.
        /// </summary>
        public async Task<SessionAnalyticsResult> GetAnalyticsAsync(
            string npcSlug = null,
            bool forceRefresh = false
        )
        {
            if (!forceRefresh && _cachedAnalytics != null)
                return _cachedAnalytics;

            if (!IsReady)
                return null;

            try
            {
                JObject raw = await _authService.SupabaseClient.Rpc<JObject>(
                    "get_session_analytics",
                    new Dictionary<string, object>
                    {
                        ["p_user_id"] = Guid.Parse(_authService.CurrentSession.playerId),
                        ["p_npc_slug"] = npcSlug ?? (object)DBNull.Value,
                    }
                );

                if (raw != null)
                {
                    _cachedAnalytics = raw.ToObject<SessionAnalyticsResult>();
                    OnAnalyticsRefreshed?.Invoke(npcSlug, _cachedAnalytics);
                }
            }
            catch (Exception ex)
            {
                _logger?.Log(
                    NPCFlowStage.ContextRetrieval,
                    NPCFlowStatus.Warning,
                    NPCFlowLogLevel.Warning,
                    $"Failed to load session analytics: {ex.Message}",
                    source: nameof(SessionAnalyticsService)
                );
            }

            return _cachedAnalytics;
        }

        /// <summary>
        /// Build a one-line summary of session activity for prompt injection.
        /// </summary>
        public string BuildAnalyticsPromptLine(string npcSlug)
        {
            if (_cachedAnalytics?.TurnTotals != null)
                return _cachedAnalytics.TurnTotals.ToPromptLine();

            // Fall back to local count
            if (_localTurnCounts.TryGetValue(npcSlug, out int localCount) && localCount > 0)
                return $"You have exchanged {localCount} messages so far this session.";

            return string.Empty;
        }

        /// <summary>
        /// Close the current session for a player+NPC pair.
        /// </summary>
        public async Task CloseSessionAsync(string npcSlug)
        {
            if (!IsReady)
                return;

            try
            {
                JObject result = await _authService.SupabaseClient.Rpc<JObject>(
                    "close_dialogue_session",
                    new Dictionary<string, object>
                    {
                        ["p_player_id"] = Guid.Parse(_authService.CurrentSession.playerId),
                        ["p_npc_slug"] = npcSlug,
                    }
                );

                bool success = result?.Value<bool>("success") ?? false;
                _logger?.Log(
                    NPCFlowStage.HistoryPersist,
                    success ? NPCFlowStatus.Success : NPCFlowStatus.Warning,
                    NPCFlowLogLevel.Info,
                    success
                        ? $"Session closed for NPC '{npcSlug}'."
                        : $"No open session to close for NPC '{npcSlug}'.",
                    source: nameof(SessionAnalyticsService)
                );

                _activeSessionId = string.Empty;
                _localTurnCounts.Remove(npcSlug);
            }
            catch (Exception ex)
            {
                _logger?.Log(
                    NPCFlowStage.HistoryPersist,
                    NPCFlowStatus.Warning,
                    NPCFlowLogLevel.Warning,
                    $"Failed to close session: {ex.Message}",
                    source: nameof(SessionAnalyticsService)
                );
            }
        }

        /// <summary>
        /// Clear cached analytics and local turn counts (e.g., on logout).
        /// </summary>
        public void ResetState()
        {
            _activeSessionId = string.Empty;
            _currentNpcSlug = string.Empty;
            _cachedAnalytics = null;
            _cachedAnalyticsPreview = string.Empty;
            _localTurnCounts.Clear();
        }

        // ── Internal ──────────────────────────────────────────────

        bool IsReady =>
            _authService != null
            && _authService.SupabaseClient != null
            && _authService.IsAuthenticated;

        // ── Editor Validation ─────────────────────────────────────

        [Button("Validate Settings")]
        void ValidateSettings()
        {
            bool ready = IsReady;
            _logger?.Log(
                NPCFlowStage.ConfigurationValidation,
                ready ? NPCFlowStatus.Success : NPCFlowStatus.Warning,
                NPCFlowLogLevel.Info,
                $"SessionAnalyticsService: ready={ready}, activeSession={_activeSessionId ?? "<none>"}",
                source: nameof(SessionAnalyticsService)
            );
        }
    }
}
