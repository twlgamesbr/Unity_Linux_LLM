using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NPCSystem
{
    /// <summary>
    /// Post-generation dialogue action handler.
    /// After the NPC responds, this analyzes the response text for detectable
    /// game actions (clue revelations, mood shifts, item transfers, location
    /// references) and executes them against the NPCEvidenceState.
    ///
    /// No JSON parsing, no LLM cooperation required — pure pattern detection
    /// on natural dialogue output.
    /// </summary>
    public static class NPCDialogueActionHandler
    {
        // ── Known suspects, locations, items from the mystery case ──
        static readonly string[] KnownSuspects =
        {
            "butler",
            "maid",
            "chef",
            "elara",
            "professor pluot",
            "pluot",
            "mr smith",
            "captain",
        };
        static readonly string[] KnownLocations =
        {
            "study",
            "library",
            "kitchen",
            "dining room",
            "drawing room",
            "ballroom",
            "garden",
            "basement",
            "attic",
            "cellar",
            "pantry",
            "bedroom",
            "hallway",
            "foyer",
            "conservatory",
            "wine cellar",
            "servants quarters",
            "parlour",
            "living room",
            "vestibule",
        };
        static readonly string[] ItemIndicators =
        {
            "key",
            "letter",
            "note",
            "map",
            "diary",
            "book",
            "candle",
            "pocket watch",
            "brooch",
            "ring",
            "photograph",
            "will",
            "document",
            "ledger",
            "account book",
            "journal",
            "manuscript",
        };

        // ── Mood-clue patterns ──
        static readonly (string keyword, string mood)[] MoodTriggers =
        {
            ("nervous", "nervous"),
            ("uneasy", "uneasy"),
            ("afraid", "fearful"),
            ("scared", "fearful"),
            ("worried", "worried"),
            ("anxious", "anxious"),
            ("frightened", "fearful"),
            ("suspicious", "suspicious"),
            ("distrustful", "suspicious"),
            ("relieved", "relieved"),
            ("grateful", "grateful"),
            ("angry", "angry"),
            ("upset", "upset"),
            ("offended", "offended"),
            ("frustrated", "frustrated"),
            ("impatient", "impatient"),
            ("sad", "sad"),
            ("melancholy", "sad"),
            ("confused", "confused"),
            ("puzzled", "confused"),
            ("ashamed", "ashamed"),
            ("guilty", "guilty"),
            ("defensive", "defensive"),
            ("evasive", "defensive"),
        };

        // ── Statement patterns that introduce new information ──
        static readonly Regex SawPattern = new Regex(
            @"\b(?:I\s+(?:saw|heard|noticed|observed|witnessed|spotted|caught\s+(?:a\s+)?glimpse\s+of))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        static readonly Regex KnowPattern = new Regex(
            @"\b(?:I\s+(?:know|remember|recall|believe|think|suspect|realized|discovered|found))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        static readonly Regex GivePattern = new Regex(
            @"\b(?:take\s+(?:this|it)|here\s+(?:you\s+go|is|are)|have\s+this|this\s+is\s+(?:for\s+)?you)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // ── Public API ──

        /// <summary>
        /// Analyze the NPC's response text and produce a list of game actions
        /// that should be executed. Each action is already deduplicated against
        /// the current evidence state.
        /// </summary>
        public static List<DialogueActionResult> AnalyzeResponse(
            string npcResponse,
            string npcSlug,
            NPCEvidenceState evidence,
            NPCProfile profile
        )
        {
            var results = new List<DialogueActionResult>();
            if (string.IsNullOrWhiteSpace(npcResponse) || evidence == null)
                return results;

            string lower = npcResponse.ToLowerInvariant();

            // 1. Detect clue revelations from "I saw/heard/know/suspect" sentences
            DetectClueSentences(npcResponse, lower, npcSlug, evidence, profile, results);

            // 2. Detect mood changes
            DetectMoodShift(lower, npcSlug, evidence, results);

            // 3. Detect item references with giving indicators
            DetectItemGive(lower, npcResponse, npcSlug, evidence, results);

            // 4. Detect location revelations
            DetectLocationRevelation(lower, npcSlug, evidence, results);

            // 5. Detect trust shifts from tone
            DetectTrustShift(lower, npcSlug, evidence, profile, results);

            return results;
        }

        // ── Detectors ──

        static void DetectClueSentences(
            string raw,
            string lower,
            string npcSlug,
            NPCEvidenceState evidence,
            NPCProfile profile,
            List<DialogueActionResult> results
        )
        {
            bool hasObservation = SawPattern.IsMatch(lower);
            bool hasKnowledge = KnowPattern.IsMatch(lower);
            if (!hasObservation && !hasKnowledge)
                return;

            // Find the sentence(s) containing the trigger
            string[] sentences = raw.Split(
                new[] { '.', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries
            );
            foreach (string sentence in sentences)
            {
                string sLower = sentence.ToLowerInvariant().Trim();
                if (string.IsNullOrWhiteSpace(sLower))
                    continue;

                bool triggered = SawPattern.IsMatch(sLower) || KnowPattern.IsMatch(sLower);
                if (!triggered)
                    continue;

                // Build a concise clue text from the sentence
                string clueText = sentence.Trim();

                // Determine category
                string category = CategorizeClue(clueText);

                // Dedup: if we already have a very similar clue, skip
                if (evidence.HasClue(clueText))
                    continue;

                // Check if the NPC is allowed to reveal this
                if (profile != null && !profile.CanRevealSecrets && category == "motive")
                    continue;

                // Record
                if (evidence.RecordClue(npcSlug, clueText, category))
                {
                    results.Add(
                        new DialogueActionResult(
                            "ClueRevealed",
                            $"Clue recorded: \"{ClueSnippet(clueText)}\" [{category}]",
                            clueText,
                            npcSlug
                        )
                    );
                }
            }
        }

        static void DetectMoodShift(
            string lower,
            string npcSlug,
            NPCEvidenceState evidence,
            List<DialogueActionResult> results
        )
        {
            foreach (var (keyword, mood) in MoodTriggers)
            {
                if (lower.Contains(keyword))
                {
                    string current = evidence.GetNpcMood(npcSlug);
                    if (current != mood)
                    {
                        evidence.SetNpcMood(npcSlug, mood);
                        results.Add(
                            new DialogueActionResult(
                                "MoodChanged",
                                $"{npcSlug} mood changed to {mood} (trigger: '{keyword}')",
                                mood,
                                npcSlug
                            )
                        );
                    }
                    // Only apply the first mood match per response
                    break;
                }
            }
        }

        static void DetectItemGive(
            string lower,
            string raw,
            string npcSlug,
            NPCEvidenceState evidence,
            List<DialogueActionResult> results
        )
        {
            if (!GivePattern.IsMatch(lower))
                return;

            foreach (string item in ItemIndicators)
            {
                if (lower.Contains(item))
                {
                    if (evidence.AddItem(item))
                    {
                        results.Add(
                            new DialogueActionResult(
                                "ItemGiven",
                                $"Item obtained: {item} (from {npcSlug})",
                                item,
                                npcSlug
                            )
                        );
                    }
                    break;
                }
            }
        }

        static void DetectLocationRevelation(
            string lower,
            string npcSlug,
            NPCEvidenceState evidence,
            List<DialogueActionResult> results
        )
        {
            foreach (string loc in KnownLocations)
            {
                if (lower.Contains(loc) && !evidence.visitedLocations.Contains(loc))
                {
                    // Only count if the NPC is describing activity there, not just naming it
                    // Heuristic: if preceded by "in the", "to the", "near the", "at the"
                    string pattern =
                        $@"\b(in|to|near|at|behind|outside|inside|around|through)\s+the\s+{Regex.Escape(loc)}\b";
                    if (Regex.IsMatch(lower, pattern, RegexOptions.IgnoreCase))
                    {
                        if (evidence.AddLocation(loc))
                        {
                            results.Add(
                                new DialogueActionResult(
                                    "LocationRevealed",
                                    $"Location noted: the {loc} (mentioned by {npcSlug})",
                                    loc,
                                    npcSlug
                                )
                            );
                        }
                        break;
                    }
                }
            }
        }

        static void DetectTrustShift(
            string lower,
            string npcSlug,
            NPCEvidenceState evidence,
            NPCProfile profile,
            List<DialogueActionResult> results
        )
        {
            // Aggressive/probing questions from the player cause trust to decrease
            // Cooperative/sympathetic language increases trust
            bool aggressive =
                lower.Contains("you're lying")
                || lower.Contains("i don't believe")
                || lower.Contains("prove it")
                || lower.Contains("i doubt that")
                || lower.Contains("you're hiding");

            bool sympathetic =
                lower.Contains("i understand")
                || lower.Contains("i believe you")
                || lower.Contains("tell me more")
                || lower.Contains("i trust you")
                || lower.Contains("thank you");

            if (aggressive)
            {
                evidence.AdjustNpcTrust(npcSlug, -5);
                results.Add(
                    new DialogueActionResult(
                        "TrustChanged",
                        $"Trust decreased: {npcSlug} trust now {evidence.GetNpcTrust(npcSlug)} (aggressive tone)",
                        evidence.GetTrustLabel(npcSlug),
                        npcSlug
                    )
                );
            }
            else if (sympathetic)
            {
                evidence.AdjustNpcTrust(npcSlug, 5);
                results.Add(
                    new DialogueActionResult(
                        "TrustChanged",
                        $"Trust increased: {npcSlug} trust now {evidence.GetNpcTrust(npcSlug)} (sympathetic tone)",
                        evidence.GetTrustLabel(npcSlug),
                        npcSlug
                    )
                );
            }
        }

        // ── Helpers ──

        static string CategorizeClue(string text)
        {
            string lower = text.ToLowerInvariant();

            if (KnownSuspects.Any(s => lower.Contains(s)))
                return "suspect";

            if (KnownLocations.Any(l => lower.Contains(l)))
                return "location";

            if (
                lower.Contains("midnight")
                || lower.Contains("night")
                || lower.Contains("evening")
                || lower.Contains("morning")
                || lower.Contains("afternoon")
                || lower.Contains("o'clock")
                || lower.Contains("pm")
                || lower.Contains("am")
                || lower.Contains("hour")
            )
                return "timeline";

            if (
                lower.Contains("motive")
                || lower.Contains("reason")
                || lower.Contains("jealous")
                || lower.Contains("money")
                || lower.Contains("inheritance")
                || lower.Contains("revenge")
                || lower.Contains("hate")
                || lower.Contains("argued")
                || lower.Contains("quarrel")
            )
                return "motive";

            if (ItemIndicators.Any(i => lower.Contains(i)))
                return "object";

            return "general";
        }

        static string ClueSnippet(string text)
        {
            if (text.Length <= 80)
                return text;
            return text.Substring(0, 77) + "...";
        }
    }
}
