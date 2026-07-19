using System;
using System.Collections.Generic;

namespace NPCSystem
{
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
                discoveredClues = new List<ClueEntry>(discoveredClues),
                obtainedItems = new List<string>(obtainedItems),
                visitedLocations = new List<string>(visitedLocations),
                npcMoodKeys = new List<string>(npcMoodKeys),
                npcMoodValues = new List<string>(npcMoodValues),
                npcTrustKeys = new List<string>(npcTrustKeys),
                npcTrustValues = new List<int>(npcTrustValues),
            };
        }
    }
}
