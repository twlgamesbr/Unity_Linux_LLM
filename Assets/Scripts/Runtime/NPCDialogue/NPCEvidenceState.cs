using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCSystem
{
    [Serializable]
    public class ClueEntry
    {
        public string clueText;
        public string category;
        public string npcSlug;
        public float confidence;

        public ClueEntry() { }

        public ClueEntry(string npcSlug, string clueText, string category, float confidence)
        {
            this.npcSlug = npcSlug;
            this.clueText = clueText;
            this.category = category;
            this.confidence = confidence;
        }
    }

    public class NPCEvidenceState : MonoBehaviour
    {
        [SerializeField] public List<ClueEntry> discoveredClues = new List<ClueEntry>();
        [SerializeField] public List<string> obtainedItems = new List<string>();
        [SerializeField] public List<string> visitedLocations = new List<string>();

        [Header("NPC Relationships")]
        [SerializeField] private List<string> npcSlugs = new List<string>();
        [SerializeField] private List<int> trustValues = new List<int>();
        [SerializeField] private List<string> moodValues = new List<string>();

        public int GetNpcTrust(string npcSlug)
        {
            int idx = npcSlugs.IndexOf(npcSlug);
            return idx >= 0 ? trustValues[idx] : 0;
        }

        public string GetNpcMood(string npcSlug)
        {
            int idx = npcSlugs.IndexOf(npcSlug);
            return idx >= 0 ? moodValues[idx] : "neutral";
        }

        public void SetNpcMood(string npcSlug, string mood)
        {
            int idx = npcSlugs.IndexOf(npcSlug);
            if (idx < 0)
            {
                npcSlugs.Add(npcSlug);
                trustValues.Add(0);
                moodValues.Add(mood);
            }
            else
            {
                moodValues[idx] = mood;
            }
        }

        public void AdjustNpcTrust(string npcSlug, int delta)
        {
            int idx = npcSlugs.IndexOf(npcSlug);
            if (idx < 0)
            {
                npcSlugs.Add(npcSlug);
                trustValues.Add(delta);
                moodValues.Add("neutral");
            }
            else
            {
                trustValues[idx] += delta;
            }
        }

        public string GetTrustLabel(string npcSlug)
        {
            int trust = GetNpcTrust(npcSlug);
            if (trust >= 50) return "High";
            if (trust >= 25) return "Medium";
            return "Low";
        }

        public bool HasClue(string clueText)
        {
            for (int i = 0; i < discoveredClues.Count; i++)
            {
                if (discoveredClues[i].clueText == clueText) return true;
            }
            return false;
        }

        public bool RecordClue(string npcSlug, string clueText, string category)
        {
            if (HasClue(clueText)) return false;
            discoveredClues.Add(new ClueEntry
            {
                clueText = clueText,
                category = category,
                npcSlug = npcSlug
            });
            return true;
        }

        public bool AddItem(string item)
        {
            if (obtainedItems.Contains(item)) return false;
            obtainedItems.Add(item);
            return true;
        }

        public bool AddLocation(string location)
        {
            if (visitedLocations.Contains(location)) return false;
            visitedLocations.Add(location);
            return true;
        }

        public List<ClueEntry> GetDiscoveredClues() => discoveredClues;
        public List<string> GetObtainedItems() => obtainedItems;
        public List<string> GetVisitedLocations() => visitedLocations;

        public NPCEvidenceStateSnapshot CaptureSnapshot()
        {
            return new NPCEvidenceStateSnapshot
            {
                discoveredClues = new List<ClueEntry>(discoveredClues),
                obtainedItems = new List<string>(obtainedItems),
                visitedLocations = new List<string>(visitedLocations),
                npcMoodKeys = new List<string>(npcSlugs),
                npcMoodValues = new List<string>(moodValues),
                npcTrustKeys = new List<string>(npcSlugs),
                npcTrustValues = new List<int>(trustValues),
            };
        }

        public void ApplySnapshot(NPCEvidenceStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            discoveredClues = snapshot.discoveredClues ?? new List<ClueEntry>();
            obtainedItems = snapshot.obtainedItems ?? new List<string>();
            visitedLocations = snapshot.visitedLocations ?? new List<string>();
            npcSlugs = snapshot.npcTrustKeys ?? new List<string>();
            trustValues = snapshot.npcTrustValues ?? new List<int>();
            moodValues = snapshot.npcMoodValues ?? new List<string>();
        }
    }
}
