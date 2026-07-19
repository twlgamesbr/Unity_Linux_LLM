using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NPCSystem
{
    /// <summary>
    /// Encapsulates NPC dialogue display logic: portrait management, text rendering, and error formatting.
    /// Separated from NPCDialogueUIController to keep orchestration and presentation concerns distinct.
    /// </summary>
    public static class DialogueDisplayHelper
    {
        // \u2500\u2500 Portrait Mapping \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        /// <summary>
        /// Known NPC slug \u2192 portrait index mapping.
        /// </summary>
        static readonly Dictionary<string, int> PortraitIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["butler"] = 0,
            ["maid"] = 1,
            ["chef"] = 2,
        };

        /// <summary>
        /// Crossfades portraits to show only the active NPC's portrait.
        /// </summary>
        public static void UpdatePortrait(NPCProfile profile, RawImage butler, RawImage maid, RawImage chef)
        {
            RawImage[] portraits = new[] { butler, maid, chef };
            if (profile == null)
            {
                foreach (RawImage img in portraits)
                {
                    if (img != null)
                        img.CrossFadeAlpha(0f, 0.15f, true);
                }
                return;
            }

            string slug = profile.GetNpcSlug();
            for (int i = 0; i < portraits.Length; i++)
            {
                if (portraits[i] == null)
                    continue;

                if (PortraitIndex.TryGetValue(slug, out int activeIndex) && i == activeIndex)
                {
                    if (profile.PortraitTexture != null)
                        portraits[i].texture = profile.PortraitTexture;
                    portraits[i].CrossFadeAlpha(1f, 0.15f, true);
                }
                else
                {
                    portraits[i].CrossFadeAlpha(0f, 0.15f, true);
                }
            }
        }

        // \u2500\u2500 Text Rendering \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        /// <summary>
        /// Sets the AI response text, handling null TextMeshPro components.
        /// </summary>
        public static void SetAIText(TMP_Text aiText, string text)
        {
            if (aiText != null)
                aiText.text = text;
        }

        /// <summary>
        /// Enables or disables player input field and stop button interactability.
        /// </summary>
        public static void SetInputEnabled(TMP_InputField playerInput, Button stopButton, bool enabled)
        {
            if (playerInput != null)
                playerInput.interactable = enabled;
            if (stopButton != null)
                stopButton.interactable = enabled;
        }

        // \u2500\u2500 Error Formatting \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        /// <summary>
        /// Normalizes error messages for consistent display, returning "Unknown dialogue error." for null/empty input.
        /// </summary>
        public static string NormalizeError(string error)
        {
            return string.IsNullOrWhiteSpace(error)
                ? "Unknown dialogue error."
                : error.Trim();
        }

        /// <summary>
        /// Formats an error for display with "Error: " prefix.
        /// </summary>
        public static string FormatErrorForDisplay(string error)
        {
            return $"Error: {NormalizeError(error)}";
        }

        // \u2500\u2500 Relationship UI Update \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

        /// <summary>
        /// Refreshes the relationship UI panel with current evidence state and dialogue count.
        /// </summary>
        public static void UpdateRelationshipUI(
            NPCRelationshipUIController relationshipUI,
            NPCEvidenceState evidence,
            string npcSlug,
            int dialogueCount)
        {
            if (relationshipUI != null && !string.IsNullOrWhiteSpace(npcSlug) && evidence != null)
            {
                relationshipUI.Refresh(evidence, npcSlug, dialogueCount);
            }
        }

        /// <summary>
        /// Finds evidence state on the GameObject or searches the scene.
        /// </summary>
        public static NPCEvidenceState FindEvidenceState(MonoBehaviour host)
        {
            return host.GetComponentInParent<NPCEvidenceState>()
                ?? Object.FindAnyObjectByType<NPCEvidenceState>(FindObjectsInactive.Include);
        }
    }
}