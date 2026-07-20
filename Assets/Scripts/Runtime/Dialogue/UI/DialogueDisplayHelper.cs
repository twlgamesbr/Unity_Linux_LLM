using TMPro;
using UnityEngine;
using UnityEngine.UI;


using NPCSystem.Monitoring;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Dialogue.UI
{
    /// <summary>
    /// Static helper for common NPCDialogueUI operations.
    /// Provides input state management, text formatting, error normalization,
    /// and portrait updates for the NPC dialogue UI.
    /// </summary>
    public static class DialogueDisplayHelper
    {
        /// <summary>
        /// Enable or disable player input controls.
        /// </summary>
        public static void SetInputEnabled(TMP_InputField playerInput, Button stopButton, bool enabled)
        {
            if (playerInput != null)
                playerInput.interactable = enabled;
            if (stopButton != null)
                stopButton.interactable = !enabled;
        }

        /// <summary>
        /// Set the AI response text display.
        /// </summary>
        public static void SetAIText(TMP_Text aiText, string text)
        {
            if (aiText != null)
                aiText.text = text ?? string.Empty;
        }

        /// <summary>
        /// Update NPC portrait raw images. Shows only the active profile's portrait,
        /// hides all others.
        /// </summary>
        public static void UpdatePortrait(NPCProfile activeProfile, RawImage portrait1, RawImage portrait2, RawImage portrait3)
        {
            // Hide all portraits
            if (portrait1 != null)
                portrait1.gameObject.SetActive(false);
            if (portrait2 != null)
                portrait2.gameObject.SetActive(false);
            if (portrait3 != null)
                portrait3.gameObject.SetActive(false);

            if (activeProfile == null || activeProfile.PortraitTexture == null)
                return;

            // Show the active profile's portrait on the first available slot
            if (portrait1 != null)
            {
                portrait1.texture = activeProfile.PortraitTexture;
                portrait1.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Normalize an error message for safe display and logging.
        /// Strips stack traces and common noise patterns.
        /// </summary>
        public static string NormalizeError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return "Unknown error";

            string normalized = error.Trim();

            // Strip stack trace lines (start with space/tab or contain "at ")
            int stackTraceIdx = normalized.IndexOf("\n  at ", System.StringComparison.Ordinal);
            if (stackTraceIdx >= 0)
                normalized = normalized.Substring(0, stackTraceIdx);

            // Truncate long messages
            if (normalized.Length > 300)
                normalized = normalized.Substring(0, 300) + "...";

            return normalized;
        }

        /// <summary>
        /// Format an error for display to the player.
        /// Returns a user-friendly message rather than raw exception text.
        /// </summary>
        public static string FormatErrorForDisplay(string error)
        {
            string normalized = NormalizeError(error);

            if (normalized.Contains("timeout", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("timed out", System.StringComparison.OrdinalIgnoreCase))
                return "The NPC is thinking... try again in a moment.";

            if (normalized.Contains("connection", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("network", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("refused", System.StringComparison.OrdinalIgnoreCase))
                return "Connection issue. Please check your network and try again.";

            if (normalized.Contains("401") || normalized.Contains("unauthorized", System.StringComparison.OrdinalIgnoreCase))
                return "Session expired. Please log in again.";

            return $"Something went wrong: {normalized}";
        }
    }
}
