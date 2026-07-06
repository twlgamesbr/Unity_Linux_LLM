using System;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine;

namespace NPCSystem
{
    [DisallowMultipleComponent]
    public class NPCNetworkSessionManager : MonoBehaviour
    {
        [Title("NPC Network Session Manager")]
        [HelpBox(
            "Server-side session cache for per-client dialogue state, selected NPC, evidence snapshot, and resolved player display name.",
            MessageMode.Log,
            drawAbove: true
        )]
        [ShowInInspector, ReadOnly]
        string InspectorSummary => "Per-client dialogue/session state cache.";

        class NPCClientDialogueSession
        {
            public string playerDisplayName = string.Empty;
            public string selectedNpcSlug = string.Empty;
            public Dictionary<string, List<DialogueEntry>> historyByNpc = new Dictionary<
                string,
                List<DialogueEntry>
            >(StringComparer.OrdinalIgnoreCase);
            public NPCEvidenceStateSnapshot evidenceSnapshot = new NPCEvidenceStateSnapshot();
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

            GetOrCreateSession(clientId).selectedNpcSlug = normalizedSlug;
        }

        public bool TryGetSelectedNpcSlug(ulong clientId, out string npcSlug)
        {
            npcSlug = string.Empty;
            return _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                && !string.IsNullOrWhiteSpace(session.selectedNpcSlug)
                && ((npcSlug = session.selectedNpcSlug) != null);
        }

        public bool HasSession(ulong clientId)
        {
            return _sessionsByClientId.ContainsKey(clientId);
        }

        public void SetPlayerDisplayName(ulong clientId, string playerDisplayName)
        {
            NPCClientDialogueSession session = GetOrCreateSession(clientId);
            session.playerDisplayName = string.IsNullOrWhiteSpace(playerDisplayName)
                ? string.Empty
                : playerDisplayName.Trim();
        }

        public string GetPlayerDisplayName(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(session.playerDisplayName)
                ? string.Empty
                : session.playerDisplayName;
        }

        public void SetHistorySnapshot(ulong clientId, string npcSlug, List<DialogueEntry> history)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
                return;

            GetOrCreateSession(clientId).historyByNpc[normalizedSlug] = CloneEntries(history);
        }

        public List<DialogueEntry> GetHistorySnapshot(ulong clientId, string npcSlug)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
                return new List<DialogueEntry>();

            return
                _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                && session.historyByNpc.TryGetValue(normalizedSlug, out List<DialogueEntry> history)
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
            foreach (var pair in session.historyByNpc)
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
            session.historyByNpc.Clear();

            if (historyByNpc == null)
                return;

            foreach (var pair in historyByNpc)
            {
                string normalizedSlug = NormalizeNpcSlug(pair.Key);
                if (string.IsNullOrWhiteSpace(normalizedSlug))
                    continue;
                session.historyByNpc[normalizedSlug] = CloneEntries(pair.Value);
            }
        }

        public void SetEvidenceSnapshot(ulong clientId, NPCEvidenceStateSnapshot snapshot)
        {
            GetOrCreateSession(clientId).evidenceSnapshot =
                snapshot?.Clone() ?? new NPCEvidenceStateSnapshot();
        }

        public bool AddInventoryItem(ulong clientId, string itemId)
        {
            string normalizedItemId = NormalizeItemId(itemId);
            if (string.IsNullOrWhiteSpace(normalizedItemId))
            {
                return false;
            }

            NPCEvidenceStateSnapshot snapshot = GetEvidenceSnapshot(clientId);
            if (snapshot.obtainedItems.Contains(normalizedItemId))
            {
                return false;
            }

            snapshot.obtainedItems.Add(normalizedItemId);
            SetEvidenceSnapshot(clientId, snapshot);
            return true;
        }

        public bool RemoveInventoryItem(ulong clientId, string itemId)
        {
            string normalizedItemId = NormalizeItemId(itemId);
            if (string.IsNullOrWhiteSpace(normalizedItemId))
            {
                return false;
            }

            NPCEvidenceStateSnapshot snapshot = GetEvidenceSnapshot(clientId);
            bool removed = snapshot.obtainedItems.Remove(normalizedItemId);
            if (removed)
            {
                SetEvidenceSnapshot(clientId, snapshot);
            }

            return removed;
        }

        public NPCEvidenceStateSnapshot GetEvidenceSnapshot(ulong clientId)
        {
            return _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                ? session.evidenceSnapshot?.Clone() ?? new NPCEvidenceStateSnapshot()
                : new NPCEvidenceStateSnapshot();
        }

        public void ClearClientSession(ulong clientId)
        {
            _sessionsByClientId.Remove(clientId);
        }

        public void ClearAllSessions()
        {
            _sessionsByClientId.Clear();
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

        static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId)
                ? string.Empty
                : itemId.Trim().ToLowerInvariant();
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
                        role = entry.role,
                        content = entry.content,
                        timestampUtc = entry.timestampUtc,
                    }
                );
            }

            return clone;
        }
    }
}
