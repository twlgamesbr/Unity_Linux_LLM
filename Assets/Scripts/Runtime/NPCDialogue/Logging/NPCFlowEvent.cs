using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NPCSystem
{
    [Serializable]
    public class NPCFlowEvent
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("timestampUtc")]
        public string TimestampUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonProperty("conversationId")]
        public string ConversationId { get; set; } = string.Empty;

        [JsonProperty("npcSlug")]
        public string NpcSlug { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("stage")]
        public NPCFlowStage Stage { get; set; }

        [JsonProperty("status")]
        public NPCFlowStatus Status { get; set; }

        [JsonProperty("level")]
        public NPCFlowLogLevel Level { get; set; } = NPCFlowLogLevel.Info;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }

        [JsonProperty("category")]
        public NPCFlowCategory Category { get; set; } = NPCFlowCategory.Infrastructure;

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, NPCFlowJson.Settings);
        }
    }

    internal static class NPCFlowJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() },
        };
    }
}
