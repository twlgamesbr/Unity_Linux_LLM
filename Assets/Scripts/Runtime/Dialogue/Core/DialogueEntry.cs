using System;


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
namespace NPCSystem.Dialogue.Core
{
    /// <summary>
    /// A single dialogue turn (user or assistant) in a per-NPC conversation history.
    /// </summary>
    [Serializable]
    public class DialogueEntry
    {
        public string Role;
        public string Content;
        public string TimestampUtc;

        public DialogueEntry() { }

        public DialogueEntry(string role, string content)
        {
            this.Role = role;
            this.Content = content;
            this.TimestampUtc = DateTime.UtcNow.ToString("o");
        }
    }
}
