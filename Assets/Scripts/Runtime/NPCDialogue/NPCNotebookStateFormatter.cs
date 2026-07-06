using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NPCSystem
{
    public static class NPCNotebookStateFormatter
    {
        public static NPCNotebookStateMessage Build(
            NPCEvidenceStateSnapshot snapshot,
            string npcSlug
        )
        {
            string normalizedSlug = string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
            NPCEvidenceStateSnapshot safeSnapshot =
                snapshot?.Clone() ?? new NPCEvidenceStateSnapshot();

            var message = new NPCNotebookStateMessage
            {
                npcSlug = normalizedSlug,
                notesPageLeft = BuildStatusPage(safeSnapshot, normalizedSlug),
                notesPageRight = BuildInvestigationPage(safeSnapshot),
            };
            message.SanitizeInPlace();
            return message;
        }

        static string BuildStatusPage(NPCEvidenceStateSnapshot snapshot, string npcSlug)
        {
            string mood = LookupMood(snapshot, npcSlug);
            string trust = LookupTrustLabel(snapshot, npcSlug);

            var sb = new StringBuilder();
            sb.AppendLine("Case notebook");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(npcSlug) ? "npc=unknown" : $"npc={npcSlug}");
            sb.AppendLine($"mood={mood}");
            sb.AppendLine($"trust={trust}");
            sb.AppendLine();
            sb.AppendLine($"clues={snapshot.discoveredClues.Count}");
            sb.AppendLine($"items={snapshot.obtainedItems.Count}");
            sb.AppendLine($"locations={snapshot.visitedLocations.Count}");
            return sb.ToString().Trim();
        }

        static string BuildInvestigationPage(NPCEvidenceStateSnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Investigation notes");
            sb.AppendLine();

            if (snapshot.discoveredClues.Count == 0)
            {
                sb.AppendLine("Clues: none yet");
            }
            else
            {
                sb.AppendLine("Clues:");
                foreach (ClueEntry clue in snapshot.discoveredClues.TakeLast(5))
                {
                    sb.AppendLine($"- {clue.clueText}");
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                snapshot.obtainedItems.Count == 0
                    ? "Items: none"
                    : $"Items: {string.Join(", ", snapshot.obtainedItems)}"
            );
            sb.AppendLine(
                snapshot.visitedLocations.Count == 0
                    ? "Locations: none"
                    : $"Locations: {string.Join(", ", snapshot.visitedLocations)}"
            );
            return sb.ToString().Trim();
        }

        static string LookupMood(NPCEvidenceStateSnapshot snapshot, string npcSlug)
        {
            if (string.IsNullOrWhiteSpace(npcSlug))
                return "neutral";

            for (
                int i = 0;
                i < System.Math.Min(snapshot.npcMoodKeys.Count, snapshot.npcMoodValues.Count);
                i++
            )
            {
                if (
                    string.Equals(
                        snapshot.npcMoodKeys[i],
                        npcSlug,
                        System.StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return string.IsNullOrWhiteSpace(snapshot.npcMoodValues[i])
                        ? "neutral"
                        : snapshot.npcMoodValues[i].Trim();
                }
            }

            return "neutral";
        }

        static string LookupTrustLabel(NPCEvidenceStateSnapshot snapshot, string npcSlug)
        {
            int trustValue = 50;
            if (!string.IsNullOrWhiteSpace(npcSlug))
            {
                for (
                    int i = 0;
                    i < System.Math.Min(snapshot.npcTrustKeys.Count, snapshot.npcTrustValues.Count);
                    i++
                )
                {
                    if (
                        string.Equals(
                            snapshot.npcTrustKeys[i],
                            npcSlug,
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        trustValue = snapshot.npcTrustValues[i];
                        break;
                    }
                }
            }

            if (trustValue >= 80)
                return "trusting";
            if (trustValue >= 60)
                return "cooperative";
            if (trustValue >= 40)
                return "cautious";
            if (trustValue >= 20)
                return "guarded";
            return "hostile";
        }
    }
}
