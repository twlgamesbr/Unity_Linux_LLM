using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCSystem
{
    [DisallowMultipleComponent]
    public class NPCNetworkSessionManager : MonoBehaviour
    {
        class NPCClientDialogueSession
        {
            public string selectedNpcSlug = string.Empty;
            public Dictionary<string, List<DialogueEntry>> historyByNpc = new Dictionary<string, List<DialogueEntry>>(StringComparer.OrdinalIgnoreCase);
            public NPCEvidenceStateSnapshot evidenceSnapshot = new NPCEvidenceStateSnapshot();
        }

        readonly Dictionary<ulong, NPCClientDialogueSession> _sessionsByClientId = new Dictionary<ulong, NPCClientDialogueSession>();

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

        public void SetHistorySnapshot(ulong clientId, string npcSlug, List<DialogueEntry> history)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug)) return;

            GetOrCreateSession(clientId).historyByNpc[normalizedSlug] = CloneEntries(history);
        }

        public List<DialogueEntry> GetHistorySnapshot(ulong clientId, string npcSlug)
        {
            string normalizedSlug = NormalizeNpcSlug(npcSlug);
            if (string.IsNullOrWhiteSpace(normalizedSlug)) return new List<DialogueEntry>();

            return _sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session)
                   && session.historyByNpc.TryGetValue(normalizedSlug, out List<DialogueEntry> history)
                ? CloneEntries(history)
                : new List<DialogueEntry>();
        }

        public Dictionary<string, List<DialogueEntry>> GetAllHistorySnapshots(ulong clientId)
        {
            if (!_sessionsByClientId.TryGetValue(clientId, out NPCClientDialogueSession session))
            {
                return new Dictionary<string, List<DialogueEntry>>(StringComparer.OrdinalIgnoreCase);
            }

            var clone = new Dictionary<string, List<DialogueEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in session.historyByNpc)
            {
                clone[pair.Key] = CloneEntries(pair.Value);
            }

            return clone;
        }

        public void SetAllHistorySnapshots(ulong clientId, Dictionary<string, List<DialogueEntry>> historyByNpc)
        {
            NPCClientDialogueSession session = GetOrCreateSession(clientId);
            session.historyByNpc.Clear();

            if (historyByNpc == null) return;

            foreach (var pair in historyByNpc)
            {
                string normalizedSlug = NormalizeNpcSlug(pair.Key);
                if (string.IsNullOrWhiteSpace(normalizedSlug)) continue;
                session.historyByNpc[normalizedSlug] = CloneEntries(pair.Value);
            }
        }

        public void SetEvidenceSnapshot(ulong clientId, NPCEvidenceStateSnapshot snapshot)
        {
            GetOrCreateSession(clientId).evidenceSnapshot = snapshot?.Clone() ?? new NPCEvidenceStateSnapshot();
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
                if (entry == null) continue;
                clone.Add(new DialogueEntry
                {
                    role = entry.role,
                    content = entry.content,
                    timestampUtc = entry.timestampUtc
                });
            }

            return clone;
        }
    }
}
