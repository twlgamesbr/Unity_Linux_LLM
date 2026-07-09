using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NPCSystem
{
    [Serializable]
    public class NPCFlowEvent
    {
        public int schemaVersion = 1;
        public string timestampUtc = DateTime.UtcNow.ToString("o");
        public string sessionId = string.Empty;
        public string requestId = string.Empty;
        public string conversationId = string.Empty;
        public string npcSlug = string.Empty;
        public string source = string.Empty;
        public NPCFlowStage stage;
        public NPCFlowStatus status;
        public NPCFlowLogLevel level = NPCFlowLogLevel.Info;
        public string message = string.Empty;
        public long durationMs;

        [JsonProperty("data")]
        public Dictionary<string, object> data { get; set; } = new Dictionary<string, object>();

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
