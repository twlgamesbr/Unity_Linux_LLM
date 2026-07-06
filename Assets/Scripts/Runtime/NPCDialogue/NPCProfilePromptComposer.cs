using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Builds runtime prompts from NPCProfile data so personality, boundaries, and
    /// gameplay-action policy live on profile assets instead of in dialogue managers.
    /// </summary>
    public static class NPCProfilePromptComposer
    {
        public static string BuildSystemPrompt(NPCProfile profile)
        {
            if (profile == null)
                return "You are a helpful in-game NPC.";

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
            return builder.ToString().Trim();
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
    }
}
