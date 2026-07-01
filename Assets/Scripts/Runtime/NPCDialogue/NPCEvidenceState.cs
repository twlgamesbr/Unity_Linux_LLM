using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NPCSystem
{
    [Serializable]
    public struct ClueEntry
    {
        public string npcSlug;
        public string clueText;
        public string category;
        public float gameTime;

        public ClueEntry(string npcSlug, string clueText, string category, float gameTime)
        {
            this.npcSlug = npcSlug;
            this.clueText = clueText;
            this.category = category;
            this.gameTime = gameTime;
        }
    }

    [Serializable]
    public struct DialogueActionResult
    {
        public string actionType;
        public string description;
        public string value;
        public string npcSlug;

        public DialogueActionResult(string actionType, string description, string value, string npcSlug)
        {
            this.actionType = actionType;
            this.description = description;
            this.value = value;
            this.npcSlug = npcSlug;
        }

        public string ToHistoryLine()
        {
            return $"[{actionType}] {description}";
        }
    }

    [Serializable]
    public class NPCEvidenceStateSnapshot
    {
        public List<ClueEntry> discoveredClues = new List<ClueEntry>();
        public List<string> obtainedItems = new List<string>();
        public List<string> visitedLocations = new List<string>();
        public List<string> npcMoodKeys = new List<string>();
        public List<string> npcMoodValues = new List<string>();
        public List<string> npcTrustKeys = new List<string>();
        public List<int> npcTrustValues = new List<int>();

        public NPCEvidenceStateSnapshot Clone()
        {
            return new NPCEvidenceStateSnapshot
            {
                discoveredClues = new List<ClueEntry>(discoveredClues ?? new List<ClueEntry>()),
                obtainedItems = new List<string>(obtainedItems ?? new List<string>()),
                visitedLocations = new List<string>(visitedLocations ?? new List<string>()),
                npcMoodKeys = new List<string>(npcMoodKeys ?? new List<string>()),
                npcMoodValues = new List<string>(npcMoodValues ?? new List<string>()),
                npcTrustKeys = new List<string>(npcTrustKeys ?? new List<string>()),
                npcTrustValues = new List<int>(npcTrustValues ?? new List<int>())
            };
        }
    }

    public class NPCEvidenceState : MonoBehaviour
    {
        [Header("Investigation State")]
        public List<ClueEntry> discoveredClues = new List<ClueEntry>();
        public List<string> obtainedItems = new List<string>();
        public List<string> visitedLocations = new List<string>();

        [Header("NPC States (serialized backing)")]
        public List<string> npcMoodKeys = new List<string>();
        public List<string> npcMoodValues = new List<string>();
        public List<string> npcTrustKeys = new List<string>();
        public List<int> npcTrustValues = new List<int>();

        // Runtime dedup / fast lookup
        HashSet<string> _clueHashes;

        void Awake()
        {
            RebuildRuntimeCaches();
        }

        void WarmLookups()
        {
            // ensure runtime dicts are populated from serialized lists
            var m = new Dictionary<string, string>();
            for (int i = 0; i < Mathf.Min(npcMoodKeys.Count, npcMoodValues.Count); i++)
                if (!string.IsNullOrWhiteSpace(npcMoodKeys[i]))
                    m[npcMoodKeys[i]] = npcMoodValues[i];
            _npcMoods = m;

            var t = new Dictionary<string, int>();
            for (int i = 0; i < Mathf.Min(npcTrustKeys.Count, npcTrustValues.Count); i++)
                if (!string.IsNullOrWhiteSpace(npcTrustKeys[i]))
                    t[npcTrustKeys[i]] = npcTrustValues[i];
            _npcTrust = t;
        }

        // --- Clues ---

        public bool RecordClue(string npcSlug, string clueText, string category)
        {
            if (string.IsNullOrWhiteSpace(clueText)) return false;
            string hash = CanonicalHash(clueText);
            if (_clueHashes != null && _clueHashes.Contains(hash))
                return false;

            discoveredClues.Add(new ClueEntry(npcSlug, clueText.Trim(), category ?? "general", Time.time));
            _clueHashes?.Add(hash);

            Log($"Clue recorded: \"{clueText.Trim()}\" (from {npcSlug}, category: {category})");
            return true;
        }

        public bool HasClue(string clueText)
        {
            if (string.IsNullOrWhiteSpace(clueText)) return false;
            return _clueHashes?.Contains(CanonicalHash(clueText)) ?? false;
        }

        // --- NPC moods ---

        Dictionary<string, string> _npcMoods = new Dictionary<string, string>();
        Dictionary<string, int> _npcTrust = new Dictionary<string, int>();

        public void SetNpcMood(string npcSlug, string mood)
        {
            if (string.IsNullOrWhiteSpace(npcSlug)) return;
            _npcMoods[npcSlug] = mood;
            SyncMoodLists();
            Log($"NPC mood set: {npcSlug} = {mood}");
        }

        public string GetNpcMood(string npcSlug)
        {
            return _npcMoods != null && _npcMoods.TryGetValue(npcSlug, out var m) ? m : "neutral";
        }

        // --- NPC trust ---

        public void AdjustNpcTrust(string npcSlug, int delta)
        {
            if (string.IsNullOrWhiteSpace(npcSlug)) return;
            if (!_npcTrust.ContainsKey(npcSlug)) _npcTrust[npcSlug] = 50;
            _npcTrust[npcSlug] = Mathf.Clamp(_npcTrust[npcSlug] + delta, 0, 100);
            SyncTrustLists();
        }

        public int GetNpcTrust(string npcSlug)
        {
            return _npcTrust != null && _npcTrust.TryGetValue(npcSlug, out var t) ? t : 50;
        }

        public string GetTrustLabel(string npcSlug)
        {
            int t = GetNpcTrust(npcSlug);
            if (t >= 80) return "trusting";
            if (t >= 60) return "cooperative";
            if (t >= 40) return "cautious";
            if (t >= 20) return "guarded";
            return "hostile";
        }

        // --- Items & locations ---

        public bool AddItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || obtainedItems.Contains(itemId)) return false;
            obtainedItems.Add(itemId);
            Log($"Item obtained: {itemId}");
            return true;
        }

        public bool AddLocation(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName) || visitedLocations.Contains(locationName)) return false;
            visitedLocations.Add(locationName);
            Log($"Location noted: {locationName}");
            return true;
        }

        // --- Context for prompt injection ---

        public string BuildStateContextString()
        {
            if (discoveredClues.Count == 0 && obtainedItems.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("\n[Investigation state — what this NPC has previously shared or experienced:]");

            if (discoveredClues.Count > 0)
            {
                int count = Mathf.Min(discoveredClues.Count, 5);
                sb.AppendLine("Clues you have revealed:");
                for (int i = discoveredClues.Count - count; i < discoveredClues.Count; i++)
                {
                    var c = discoveredClues[i];
                    if (string.IsNullOrWhiteSpace(c.category) || c.category == "general")
                        sb.AppendLine($"- {c.clueText}");
                    else
                        sb.AppendLine($"- {c.clueText} [{c.category}]");
                }
            }

            if (obtainedItems.Count > 0)
                sb.AppendLine($"Items you have given the player: {string.Join(", ", obtainedItems)}.");

            sb.AppendLine("[/Investigation state]");
            return sb.ToString();
        }

        public string BuildNpcStateLine(string npcSlug)
        {
            if (string.IsNullOrWhiteSpace(npcSlug)) return string.Empty;
            string mood = GetNpcMood(npcSlug);
            string trust = GetTrustLabel(npcSlug);
            return $"Current state: mood={mood}, trust={trust}.";
        }

        public NPCEvidenceStateSnapshot CreateSnapshot()
        {
            return new NPCEvidenceStateSnapshot
            {
                discoveredClues = new List<ClueEntry>(discoveredClues ?? new List<ClueEntry>()),
                obtainedItems = new List<string>(obtainedItems ?? new List<string>()),
                visitedLocations = new List<string>(visitedLocations ?? new List<string>()),
                npcMoodKeys = new List<string>(npcMoodKeys ?? new List<string>()),
                npcMoodValues = new List<string>(npcMoodValues ?? new List<string>()),
                npcTrustKeys = new List<string>(npcTrustKeys ?? new List<string>()),
                npcTrustValues = new List<int>(npcTrustValues ?? new List<int>())
            };
        }

        public void ApplySnapshot(NPCEvidenceStateSnapshot snapshot)
        {
            NPCEvidenceStateSnapshot source = snapshot?.Clone() ?? new NPCEvidenceStateSnapshot();
            discoveredClues = source.discoveredClues;
            obtainedItems = source.obtainedItems;
            visitedLocations = source.visitedLocations;
            npcMoodKeys = source.npcMoodKeys;
            npcMoodValues = source.npcMoodValues;
            npcTrustKeys = source.npcTrustKeys;
            npcTrustValues = source.npcTrustValues;
            RebuildRuntimeCaches();
        }

        // --- Serialization sync ---

        void SyncMoodLists()
        {
            npcMoodKeys.Clear();
            npcMoodValues.Clear();
            foreach (var kv in _npcMoods)
            {
                npcMoodKeys.Add(kv.Key);
                npcMoodValues.Add(kv.Value);
            }
        }

        void SyncTrustLists()
        {
            npcTrustKeys.Clear();
            npcTrustValues.Clear();
            foreach (var kv in _npcTrust)
            {
                npcTrustKeys.Add(kv.Key);
                npcTrustValues.Add(kv.Value);
            }
        }

        // --- Helpers ---

        static string CanonicalHash(string text)
        {
            return text.Trim().ToLowerInvariant().GetHashCode().ToString();
        }

        void RebuildRuntimeCaches()
        {
            _clueHashes = new HashSet<string>();
            foreach (var c in discoveredClues ?? new List<ClueEntry>())
            {
                _clueHashes.Add(CanonicalHash(c.clueText));
            }

            WarmLookups();
        }

        static void Log(string message)
        {
            NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.ActionExecution, NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug, message, source: nameof(NPCEvidenceState));
        }
    }
}
