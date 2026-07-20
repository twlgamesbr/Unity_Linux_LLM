using System;
using Newtonsoft.Json;
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
using Postgrest.Attributes;
using Postgrest.Models;

namespace NPCSystem.Dialogue.Persistence
{
    /// <summary>
    /// Maps to the <c>dialogue_turns</c> table.
    /// Used by SupabaseDialogueRepository for CRUD operations.
    /// </summary>
    [Table("dialogue_turns")]
    public class DialogueTurnRecord : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        [Column("player_id")]
        public string PlayerId { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("npc_mood_snapshot")]
        public string NpcMoodSnapshot { get; set; }

        [Column("npc_trust_snapshot")]
        public int? NpcTrustSnapshot { get; set; }

        [Column("action_type")]
        public string ActionType { get; set; }

        [Column("action_result")]
        public string ActionResult { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Maps to <c>dialogue_turn_vectors</c> for pgvector similarity search.
    /// </summary>
    [Table("dialogue_turn_vectors")]
    public class DialogueTurnVectorRecord : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("turn_id")]
        public string TurnId { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("npc_slug")]
        public string NpcSlug { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("content_hash")]
        public string ContentHash { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Wraps the JSON result from <c>get_session_analytics</c> RPC.
    /// </summary>
    [Serializable]
    public class SessionAnalyticsResult
    {
        [JsonProperty("session_summary")]
        public SessionSummaryData SessionSummary;

        [JsonProperty("turn_totals")]
        public TurnTotalsData TurnTotals;

        [JsonProperty("recent_sessions")]
        public RecentSessionData[] RecentSessions;
    }

    [Serializable]
    public class SessionSummaryData
    {
        [JsonProperty("total_sessions")]
        public int TotalSessions;

        [JsonProperty("total_turns")]
        public int TotalTurns;

        [JsonProperty("avg_turns_per_session")]
        public float AvgTurnsPerSession;

        [JsonProperty("first_session_at")]
        public DateTime? FirstSessionAt;

        [JsonProperty("last_session_at")]
        public DateTime? LastSessionAt;
    }

    [Serializable]
    public class TurnTotalsData
    {
        [JsonProperty("user_turns")]
        public int UserTurns;

        [JsonProperty("assistant_turns")]
        public int AssistantTurns;

        [JsonProperty("system_turns")]
        public int SystemTurns;

        [JsonProperty("total_turns")]
        public int TotalTurns;

        public string ToPromptLine()
        {
            return $"You have exchanged {UserTurns} player messages and {AssistantTurns} NPC responses with this player so far.";
        }
    }

    [Serializable]
    public class RecentSessionData
    {
        [JsonProperty("session_id")]
        public string SessionId;

        [JsonProperty("npc_slug")]
        public string NpcSlug;

        [JsonProperty("turn_count")]
        public int TurnCount;

        [JsonProperty("started_at")]
        public DateTime? StartedAt;

        [JsonProperty("ended_at")]
        public DateTime? EndedAt;

        [JsonProperty("summary")]
        public string Summary;
    }
}
