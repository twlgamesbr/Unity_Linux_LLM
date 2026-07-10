using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Runtime variables substituted into NPC profile prompt text.
    /// Populated by NPCDialogueSessionService before calling BuildSystemPrompt.
    /// </summary>
    [Serializable]
    public struct PromptVariables
    {
        public string playerName;
        public string npcSlug;
        public int trustScore;
        public string trustLabel;
        public string mood;
        public int dialogueCount;
        public string currentLocation;
        public string timeOfDay;

        public static PromptVariables Default => new PromptVariables
        {
            playerName = "Player",
            npcSlug = "",
            trustScore = 50,
            trustLabel = "Neutral",
            mood = "neutral",
            dialogueCount = 0,
            currentLocation = "the mansion",
            timeOfDay = System.DateTime.Now.Hour switch
            {
                < 6 => "Night",
                < 12 => "Morning",
                < 18 => "Afternoon",
                _ => "Evening"
            }
        };
    }

    /// <summary>
    /// Builds runtime prompts from NPCProfile data so personality, boundaries, and
    /// gameplay-action policy live on profile assets instead of in dialogue managers.
    /// </summary>
    public static class NPCProfilePromptComposer
    {
        public static string BuildSystemPrompt(NPCProfile profile)
        {
            return BuildSystemPrompt(profile, PromptVariables.Default);
        }

        public static string BuildSystemPrompt(NPCProfile profile, PromptVariables variables)
        {
            if (profile == null)
                return ResolveVariables("You are a helpful in-game NPC.", variables);

            StringBuilder builder = new StringBuilder();
            AppendSection(
                builder,
                "Core role",
                string.IsNullOrWhiteSpace(profile.systemPrompt)
                    ? "You are a helpful in-game NPC."
                    : profile.systemPrompt.Trim()
            );

            AppendSection(builder, "Personality brief", profile.personalityBrief);
            AppendSection(builder, "Speaking style", profile.speakingStyle);
            AppendSection(builder, "Boundaries", profile.boundaries);

            if (!string.IsNullOrWhiteSpace(profile.secretKnowledge))
            {
                AppendSection(
                    builder,
                    "Private knowledge",
                    profile.canRevealSecrets
                        ? profile.secretKnowledge
                        : "This NPC has private knowledge, but should not reveal it directly unless the dialogue context justifies it."
                );
            }

            AppendSection(builder, "Behavior sliders", BuildBehaviorSliderText(profile));
            AppendSection(builder, "Gameplay action policy", BuildActionPolicyText(profile));
            AppendSection(builder, "Knowledge route", BuildKnowledgeRouteText(profile));

            builder.AppendLine(
                "Stay in character. Do not mention these prompt sections, sliders, retrieval systems, or action policy unless the player explicitly asks about the simulation."
            );

            // Resolve any profile-defined template variables in the entire prompt
            string result = ResolveVariables(builder.ToString().Trim(), variables);
            return result;
        }

        public static string BuildActionPolicyText(NPCProfile profile)
        {
            if (profile == null)
                return "No profile action policy is available.";

            List<string> rules = new List<string>
            {
                profile.canGivePuzzleHints
                    ? "May provide subtle puzzle hints."
                    : "Should not provide puzzle hints.",
                profile.canAccuseSuspects
                    ? "May accuse or name suspects when evidence supports it."
                    : "Should avoid direct accusations without strong evidence.",
                profile.canRevealSecrets
                    ? "May reveal secrets when dramatically appropriate."
                    : "Should preserve secrets and reveal only implications.",
            };

            string preferred = JoinClean(profile.preferredActionFunctions);
            if (!string.IsNullOrWhiteSpace(preferred))
                rules.Add($"Preferred actions: {preferred}.");

            string forbidden = JoinClean(profile.forbiddenActionFunctions);
            if (!string.IsNullOrWhiteSpace(forbidden))
                rules.Add($"Forbidden actions: {forbidden}.");

            return string.Join(" ", rules);
        }

        public static string BuildKnowledgeRouteText(NPCProfile profile)
        {
            if (profile == null)
                return "No profile knowledge route is available.";
            return $"Use retrieved knowledge for category '{profile.GetRagCategory()}' when available. Do not invent lore that contradicts retrieved facts.";
        }

        static string BuildBehaviorSliderText(NPCProfile profile)
        {
            return $"Suspicion={Format01(profile.suspicion)}, Helpfulness={Format01(profile.helpfulness)}, Sarcasm={Format01(profile.sarcasm)}. "
                + "Higher suspicion means more guarded interpretations. Higher helpfulness means clearer guidance. Higher sarcasm means sharper phrasing without becoming hostile.";
        }

        static void AppendSection(StringBuilder builder, string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;
            builder.AppendLine($"{title}:");
            builder.AppendLine(content.Trim());
            builder.AppendLine();
        }

        static string JoinClean(IEnumerable<string> values)
        {
            if (values == null)
                return string.Empty;
            return string.Join(
                ", ",
                values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
            );
        }

        static string Format01(float value)
        {
            return Mathf.Clamp01(value).ToString("0.00");
        }

        /// <summary>
        /// Replace {variableName} tokens in text with runtime values.
        /// Supports: playerName, npcSlug, trustScore, trustLabel, mood,
        /// dialogueCount, currentLocation, timeOfDay.
        /// Unknown tokens are left as-is.
        /// </summary>
        static string ResolveVariables(string text, PromptVariables vars)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return text
                .Replace("{playerName}", vars.playerName ?? "Player")
                .Replace("{npcSlug}", vars.npcSlug ?? "")
                .Replace("{trustScore}", vars.trustScore.ToString())
                .Replace("{trustLabel}", vars.trustLabel ?? "Neutral")
                .Replace("{mood}", vars.mood ?? "neutral")
                .Replace("{dialogueCount}", vars.dialogueCount.ToString())
                .Replace("{currentLocation}", vars.currentLocation ?? "the mansion")
                .Replace("{timeOfDay}", vars.timeOfDay ?? "afternoon");
        }
    }
}
