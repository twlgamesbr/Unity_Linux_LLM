using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace NPCSystem
{
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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
