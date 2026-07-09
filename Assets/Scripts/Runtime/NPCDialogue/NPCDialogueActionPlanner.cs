using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NPCSystem
{
    public enum NPCDialogueActionType
    {
        None,
        PuzzleHint,
        ShowNotes,
        ShowMap,
        ShowSolve,
        ShowHelp,
        PressSuspect,
        RecallEvidence,
    }

    [Serializable]
    public sealed class NPCDialogueActionPlan
    {
        public NPCDialogueActionType actionType = NPCDialogueActionType.None;
        public string reason = string.Empty;
        public string contextPrompt = string.Empty;

        public static NPCDialogueActionPlan None(string reason = "No action planning rule matched.")
        {
            return new NPCDialogueActionPlan
            {
                actionType = NPCDialogueActionType.None,
                reason = reason,
                contextPrompt = string.Empty,
            };
        }
    }

    public class NPCDialogueActionPlanner : MonoBehaviour
    {
        static readonly string[] HintKeywords =
        {
            "hint",
            "stuck",
            "investigate",
            "clue",
            "what should i do",
            "next",
        };
        static readonly string[] NotesKeywords = { "notes", "notebook", "journal" };
        static readonly string[] MapKeywords = { "map", "where", "room", "location" };
        static readonly string[] SolveKeywords = { "solve", "accuse", "who did it", "solution" };
        static readonly string[] HelpKeywords = { "help", "how do i", "what can i do" };
        static readonly string[] EvidenceKeywords =
        {
            "remember",
            "evidence",
            "suspect",
            "proof",
            "alibi",
        };

        public NPCDialogueActionPlan Plan(string playerMessage, NPCProfile profile)
        {
            if (string.IsNullOrWhiteSpace(playerMessage))
                return NPCDialogueActionPlan.None("Player message was empty.");

            string lower = playerMessage.Trim().ToLowerInvariant();
            if (Matches(lower, NotesKeywords))
            {
                return BuildPlan(
                    NPCDialogueActionType.ShowNotes,
                    "Player referenced notes/notebook.",
                    "If useful, guide the player toward their notes and summarize which written clues matter most right now."
                );
            }

            if (Matches(lower, MapKeywords))
            {
                return BuildPlan(
                    NPCDialogueActionType.ShowMap,
                    "Player referenced map/location.",
                    "If useful, anchor the reply in mansion geography, rooms, or where the player should look next."
                );
            }

            if (Matches(lower, SolveKeywords))
            {
                return BuildPlan(
                    NPCDialogueActionType.ShowSolve,
                    "Player asked about solving/accusing.",
                    "If useful, frame the response around what evidence is still missing before a final accusation."
                );
            }

            if (Matches(lower, HelpKeywords))
            {
                return BuildPlan(
                    NPCDialogueActionType.ShowHelp,
                    "Player asked for help.",
                    "If useful, explain available investigation options in-character without breaking the mystery tone."
                );
            }

            if (Matches(lower, HintKeywords) && (profile == null || profile.canGivePuzzleHints))
            {
                return BuildPlan(
                    NPCDialogueActionType.PuzzleHint,
                    "Player seems stuck and this NPC may give hints.",
                    "Offer one subtle investigation hint. Prefer nudging toward evidence, suspect behavior, or room-specific clues instead of revealing the answer outright."
                );
            }

            if (Matches(lower, EvidenceKeywords))
            {
                return BuildPlan(
                    NPCDialogueActionType.RecallEvidence,
                    "Player asked about memory/evidence/suspects.",
                    "Summarize the strongest known clues, uncertainties, and contradictions from this NPC's perspective."
                );
            }

            if (
                profile != null
                && profile.canAccuseSuspects
                && (lower.Contains("who") || lower.Contains("suspect") || lower.Contains("culprit"))
            )
            {
                return BuildPlan(
                    NPCDialogueActionType.PressSuspect,
                    "Player asked for suspect judgment and NPC may accuse suspects.",
                    "If you name a suspect, explain the suspicion as a hypothesis grounded in clues rather than certainty."
                );
            }

            return NPCDialogueActionPlan.None();
        }

        public static string BuildPromptHint(NPCDialogueActionPlan plan)
        {
            if (
                plan == null
                || plan.actionType == NPCDialogueActionType.None
                || string.IsNullOrWhiteSpace(plan.contextPrompt)
            )
                return string.Empty;
            return $"Action guidance ({plan.actionType}): {plan.contextPrompt}";
        }

        static NPCDialogueActionPlan BuildPlan(
            NPCDialogueActionType actionType,
            string reason,
            string contextPrompt
        )
        {
            return new NPCDialogueActionPlan
            {
                actionType = actionType,
                reason = reason,
                contextPrompt = contextPrompt,
            };
        }

        static bool Matches(string text, IEnumerable<string> keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword));
        }
    }
}
