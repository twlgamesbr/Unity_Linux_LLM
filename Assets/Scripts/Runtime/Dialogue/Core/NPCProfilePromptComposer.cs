using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NPCSystem.Auth;
using NPCSystem.Character.NPC;
using NPCSystem.Character.Player;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Initialization;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Monitoring;
using NPCSystem.Network.Core;
using UnityEngine;

namespace NPCSystem.Dialogue.Core
{
    /// <summary>
    /// Runtime variables substituted into NPC profile prompt text.
    /// </summary>
    [Serializable]
    public struct PromptVariables
    {
        public string playerName;
        public string npcSlug;
        public int dialogueCount;
        public string currentLocation;
        public string timeOfDay;
        public int expertiseLevel;
        public string expertiseLabel;
        public int reputationScore;

        public static PromptVariables Default =>
            new PromptVariables
            {
                playerName = "Developer",
                npcSlug = "",
                dialogueCount = 0,
                currentLocation = "the codebase",
                timeOfDay = System.DateTime.Now.Hour switch
                {
                    < 6 => "Night",
                    < 12 => "Morning",
                    < 18 => "Afternoon",
                    _ => "Evening",
                },
                expertiseLevel = 1,
                expertiseLabel = "Junior",
                reputationScore = 0,
            };
    }

    /// <summary>
    /// Builds runtime prompts from NPCProfile data so personality, boundaries, and
    /// action policy live on profile assets instead of in dialogue managers.
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
                return ResolveVariables("You are a helpful codebase developer.", variables);

            StringBuilder builder = new StringBuilder();
            AppendSection(
                builder,
                "Core role",
                string.IsNullOrWhiteSpace(profile.SystemPrompt)
                    ? "You are a helpful developer NPC guiding the player through the game codebase."
                    : profile.SystemPrompt.Trim()
            );

            AppendSection(builder, "Personality brief", profile.PersonalityBrief);
            AppendSection(builder, "Speaking style", profile.SpeakingStyle);
            AppendSection(builder, "Boundaries", profile.Boundaries);

            AppendSection(builder, "Behavior", BuildBehaviorSliderText(profile));
            AppendSection(builder, "Action policy", BuildActionPolicyText(profile));
            AppendSection(builder, "Knowledge", BuildKnowledgeRouteText(profile));

            builder.AppendLine(
                "Stay in character as a game developer. Do not mention these prompt sections, "
                    + "retrieval systems, or action policy unless the player explicitly asks about the simulation."
            );

            string result = ResolveVariables(builder.ToString().Trim(), variables);
            return result;
        }

        public static string BuildActionPolicyText(NPCProfile profile)
        {
            if (profile == null)
                return "No action policy configured.";

            List<string> rules = new List<string>();

            string preferred = JoinClean(profile.PreferredActionFunctions);
            if (!string.IsNullOrWhiteSpace(preferred))
                rules.Add($"Preferred actions: {preferred}.");

            string forbidden = JoinClean(profile.ForbiddenActionFunctions);
            if (!string.IsNullOrWhiteSpace(forbidden))
                rules.Add($"Forbidden actions: {forbidden}.");

            return rules.Count > 0 ? string.Join(" ", rules) : "No specific action policy configured.";
        }

        public static string BuildKnowledgeRouteText(NPCProfile profile)
        {
            if (profile == null)
                return "No knowledge route available.";
            return $"Search the codebase collection '{profile.GetRagCategory()}' when context is needed. "
                + "Do not invent code that contradicts retrieved documentation.";
        }

        static string BuildBehaviorSliderText(NPCProfile profile)
        {
            return $"Helpfulness={Format01(profile.Helpfulness)}. "
                + "Higher helpfulness means clearer guidance and more direct answers.";
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
                values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            );
        }

        static string Format01(float value)
        {
            return Mathf.Clamp01(value).ToString("0.00");
        }

        static string ResolveVariables(string text, PromptVariables vars)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return text.Replace("{playerName}", vars.playerName ?? "Developer")
                .Replace("{npcSlug}", vars.npcSlug ?? "")
                .Replace("{dialogueCount}", vars.dialogueCount.ToString())
                .Replace("{currentLocation}", vars.currentLocation ?? "the codebase")
                .Replace("{timeOfDay}", vars.timeOfDay ?? "afternoon")
                .Replace("{expertiseLevel}", vars.expertiseLevel.ToString())
                .Replace("{expertiseLabel}", vars.expertiseLabel ?? "Junior")
                .Replace("{reputationScore}", vars.reputationScore.ToString());
        }
    }
}
