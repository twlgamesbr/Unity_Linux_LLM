using System.Collections.Generic;
using System.Text;

namespace NPCSystem
{
    public static class NPCNotebookStateFormatter
    {
        public static NPCNotebookStateMessage Build(NPCEvidenceStateSnapshot snapshot, string npcSlug)
        {
            string notesLeft = BuildLeftPage(snapshot, npcSlug);
            string notesRight = BuildRightPage(snapshot);

            return new NPCNotebookStateMessage
            {
                npcSlug = npcSlug ?? string.Empty,
                notesPageLeft = notesLeft,
                notesPageRight = notesRight,
            };
        }

        static string BuildLeftPage(NPCEvidenceStateSnapshot snapshot, string npcSlug)
        {
            if (snapshot == null)
                return string.Empty;

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(npcSlug)
                && snapshot.npcTrustKeys != null
                && snapshot.npcTrustValues != null)
            {
                int trustIdx = snapshot.npcTrustKeys.IndexOf(npcSlug);
                if (trustIdx >= 0 && trustIdx < snapshot.npcTrustValues.Count)
                {
                    int trust = snapshot.npcTrustValues[trustIdx];
                    sb.AppendLine($"trust={FormatTrustLabel(trust)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(npcSlug)
                && snapshot.npcMoodKeys != null
                && snapshot.npcMoodValues != null)
            {
                int moodIdx = snapshot.npcMoodKeys.IndexOf(npcSlug);
                if (moodIdx >= 0 && moodIdx < snapshot.npcMoodValues.Count)
                {
                    string mood = snapshot.npcMoodValues[moodIdx];
                    sb.AppendLine($"mood={mood}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        static string BuildRightPage(NPCEvidenceStateSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            var sb = new StringBuilder();

            if (snapshot.discoveredClues != null)
            {
                foreach (ClueEntry clue in snapshot.discoveredClues)
                {
                    if (clue != null && !string.IsNullOrWhiteSpace(clue.clueText))
                    {
                        sb.AppendLine(clue.clueText);
                    }
                }
            }

            if (snapshot.obtainedItems != null)
            {
                foreach (string item in snapshot.obtainedItems)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        sb.AppendLine($"Item: {item}");
                    }
                }
            }

            if (snapshot.visitedLocations != null)
            {
                foreach (string loc in snapshot.visitedLocations)
                {
                    if (!string.IsNullOrWhiteSpace(loc))
                    {
                        sb.AppendLine(loc);
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        static string FormatTrustLabel(int trust)
        {
            if (trust >= 50) return "cooperative";
            if (trust >= 25) return "neutral";
            return "suspicious";
        }
    }
}
