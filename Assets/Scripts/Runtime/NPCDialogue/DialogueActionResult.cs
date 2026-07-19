using System;
using System.Collections.Generic;

namespace NPCSystem
{
    [Serializable]
    public class DialogueActionResult
    {
        public string ActionType { get; private set; }
        public string Description { get; private set; }
        public string Data { get; private set; }
        public string NpcSlug { get; private set; }

        public DialogueActionResult(string actionType, string description, string data, string npcSlug)
        {
            ActionType = actionType;
            Description = description;
            Data = data;
            NpcSlug = npcSlug;
        }

        public string ToHistoryLine()
        {
            return $"[{ActionType}] {Description}";
        }
    }
}
