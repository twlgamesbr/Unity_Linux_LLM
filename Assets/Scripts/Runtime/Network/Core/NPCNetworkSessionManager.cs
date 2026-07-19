using System;
using System.Collections.Generic;
using EditorAttributes;
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
namespace NPCSystem.Network.Core
{
    [DisallowMultipleComponent]
    public class NPCNetworkSessionManager : MonoBehaviour
    {
        [Title("NPC Network Session Manager")]
        [HelpBox(
            "Server-side session cache for per-client dialogue state, selected NPC, and resolved player display name.",
            MessageMode.Log,
            drawAbove: true
        )]
        [ShowInInspector, ReadOnly]
        string InspectorSummary => "Per-client dialogue/session state cache.";

        class NPCClientDialogueSession
        {
            public string PlayerDisplayName = string.Empty;
            public string SelectedNpcSlug = string.Empty;
            public Dictionary<string, List<DialogueEntry>> HistoryByNpc = new Dictionary<
                string,
                List<DialogueEntry>
            >(StringComparer.OrdinalIgnoreCase);
            public List<string> InventoryItems = new List<string>();
        }

        readonly Dictionary<ulong, NPCClientDialogueSession> _sessionsByClientId =
            new Dictionary<ulong, NPCClientDialogueSession>();

        [ShowInInspector]
        int ActiveSessionCount => _sessionsByClientId.Count;

        public void SetSelectedNpcSlug(ulong clientId, string npcSlug)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
            {
                ClearClientSession(clientId);
                return;
            }

            GetOrCreateSession(clientId).SelectedNpcSlug = normalizedSlug;
        }

        public bool TryGetSelectedNpcSlug(ulong clientId, out string npcSlug)
        {
            npcSlug = string.Empty;
            return _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                && !string.IsNullOrWhiteSpace(session.SelectedNpcSlug)
                && ((npcSlug = session.SelectedNpcSlug) != null);
        }

        public bool HasSession(ulong clientId)
        {
            return _sessionsByClientId.ContainsKey(clientId);
        }

        public void SetPlayerDisplayName(ulong clientId, string playerDisplayName)
        {
            NPCClientDialogueSession session = GetOrCreateSession(clientId);
            session.PlayerDisplayName = string.IsNullOrWhiteSpace(playerDisplayName)
                ? string.Empty
                : playerDisplayName.Trim();
        }

        public string GetPlayerDisplayName(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(session.PlayerDisplayName)
                ? string.Empty
                : session.PlayerDisplayName;
        }

        public void SetHistorySnapshot(ulong clientId, string npcSlug, List<DialogueEntry> history)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
                return;

            GetOrCreateSession(clientId).HistoryByNpc[normalizedSlug] = CloneEntries(history);
        }

        public List<DialogueEntry> GetHistorySnapshot(ulong clientId, string npcSlug)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
                return new List<DialogueEntry>();

            return
                _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                && session.HistoryByNpc.TryGetValue(normalizedSlug, out List<DialogueEntry> history)
                ? CloneEntries(history)
                : new List<DialogueEntry>();
        }

        public Dictionary<string, List<DialogueEntry>> GetAllHistorySnapshots(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                return new Dictionary<string, List<DialogueEntry>>(
                    StringComparer.OrdinalIgnoreCase
                );
            }

            var clone = new Dictionary<string, List<DialogueEntry>>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var pair in session.HistoryByNpc)
            {
                clone[pair.Key] = CloneEntries(pair.Value);
            }

            return clone;
        }

        public void SetAllHistorySnapshots(
            ulong clientId,
            Dictionary<string, List<DialogueEntry>> historyByNpc
        )
        {
            NPCClientDialogueSession session = GetOrCreateSession(clientId);
            session.HistoryByNpc.Clear();

            if (historyByNpc == null)
                return;

            foreach (var pair in historyByNpc)
            {
                string normalizedSlug = NormalizeNpcSlug(pair.Key);
                if (string.IsNullOrWhiteSpace(normalizedSlug))
                    continue;
                session.HistoryByNpc[normalizedSlug] = CloneEntries(pair.Value);
            }
        }

        public void ClearClientSession(ulong clientId)
        {
            _sessionsByClientId.Remove(clientId);
        }

        public void ClearAllSessions()
        {
            _sessionsByClientId.Clear();
        }

        /// <summary>
        /// Record an inventory item for a client's session (server-only).
        /// Used by NPCNetworkItemInteractor after successful pickup.
        /// </summary>
        public void AddInventoryItem(ulong clientId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;
            NPCClientDialogueSession session = GetOrCreateSession(clientId);
            // Store in session for persistence tracking — actual inventory is in NPCPlayerInventory NetworkList
            if (!session.InventoryItems.Contains(itemId))
                session.InventoryItems.Add(itemId);
        }

        /// <summary>
        /// Remove an inventory item from a client's session (server-only).
        /// Used by NPCNetworkItemInteractor after successful transfer.
        /// </summary>
        public void RemoveInventoryItem(ulong clientId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;
            if (_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                session.InventoryItems.Remove(itemId);
            }
        }

        [Button("Log Active Sessions")]
        void LogActiveSessions()
        {
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.ClientSession,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    $"Session manager currently holds {_sessionsByClientId.Count} client session(s).",
                    source: nameof(NPCNetworkSessionManager),
                    data: new Dictionary<string, object>
                    {
                        ["activeSessionCount"] = _sessionsByClientId.Count,
                    }
                );
        }

        /// <summary>
        /// Ensure a session exists for the given client, creating one if needed.
        /// </summary>
        public void EnsureSession(ulong clientId)
        {
            GetOrCreateSession(clientId);
        }

        NPCClientDialogueSession GetOrCreateSession(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                session = new NPCClientDialogueSession();
                _sessionsByClientId[clientId] = session;
            }

            return session;
        }

        static string NormalizeNpcSlug(string npcSlug)
        {
            return string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
        }

        static List<DialogueEntry> CloneEntries(List<DialogueEntry> history)
        {
            List<DialogueEntry> clone = new List<DialogueEntry>();
            foreach (DialogueEntry entry in history ?? new List<DialogueEntry>())
            {
                if (entry == null)
                    continue;
                clone.Add(
                    new DialogueEntry
                    {
                        Role = entry.Role,
                        Content = entry.Content,
                        TimestampUtc = entry.TimestampUtc,
                    }
                );
            }

            return clone;
        }
    }
}
