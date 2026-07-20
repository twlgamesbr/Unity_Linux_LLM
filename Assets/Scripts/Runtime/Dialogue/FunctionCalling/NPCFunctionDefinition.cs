using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCSystem.Dialogue.FunctionCalling
{
    /// <summary>
    /// Represents a function that an NPC can call during dialogue.
    /// Functions are defined in NPCProfile assets and executed by the FunctionExecutor.
    /// </summary>
    [Serializable]
    public class NPCFunctionDefinition
    {
        [Tooltip("Unique identifier for this function (e.g., 'give_item', 'change_mood', 'spawn_object')")]
        public string functionName;

        [Tooltip("Human-readable description shown to the LLM for function calling")]
        public string description;

        [Tooltip("JSON schema for the function parameters")]
        public string parametersJsonSchema;

        [Tooltip("Category for organization (e.g., 'inventory', 'scene', 'mood', 'quest')")]
        public string category = "general";

        [Tooltip("Whether this function requires player trust above a threshold")]
        public int requiredTrustLevel = 0;

        [Tooltip("Cooldown in seconds before this function can be called again")]
        public float cooldownSeconds = 0f;

        [Tooltip("Maximum number of times this function can be called per session")]
        public int maxCallsPerSession = -1; // -1 = unlimited

        /// <summary>
        /// Validates that the function definition is well-formed.
        /// </summary>
        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                error = "Function name is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                error = "Function description is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parametersJsonSchema))
            {
                error = "Parameters JSON schema is required";
                return false;
            }

            // Validate JSON schema is parseable
            try
            {
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(parametersJsonSchema);
                if (parsed["type"]?.ToString() != "object")
                {
                    error = "Parameters schema must be an object type";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Invalid JSON schema: {ex.Message}";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Creates a function definition for giving an item to the player.
        /// </summary>
        public static NPCFunctionDefinition CreateGiveItemFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "give_item",
                description = "Give an item from the NPC's inventory to the player",
                category = "inventory",
                requiredTrustLevel = 30,
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""itemId"": { ""type"": ""string"", ""description"": ""The ID of the item to give"" },
                        ""quantity"": { ""type"": ""integer"", ""minimum"": 1, ""default"": 1, ""description"": ""Number of items to give"" },
                        ""reason"": { ""type"": ""string"", ""description"": ""Why the NPC is giving this item"" }
                    },
                    ""required"": [""itemId""]
                }",
            };
        }

        /// <summary>
        /// Creates a function definition for changing NPC mood.
        /// </summary>
        public static NPCFunctionDefinition CreateChangeMoodFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "change_mood",
                description = "Change the NPC's current mood, affecting future dialogue tone",
                category = "mood",
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""mood"": { ""type"": ""string"", ""enum"": [""happy"", ""sad"", ""angry"", ""neutral"", ""excited"", ""suspicious"", ""friendly"", ""hostile""], ""description"": ""The new mood state"" },
                        ""reason"": { ""type"": ""string"", ""description"": ""Why the mood changed"" },
                        ""duration"": { ""type"": ""integer"", ""minimum"": 0, ""default"": 0, ""description"": ""Duration in seconds (0 = permanent until changed again)"" }
                    },
                    ""required"": [""mood"", ""reason""]
                }",
            };
        }

        /// <summary>
        /// Creates a function definition for modifying scene objects.
        /// </summary>
        public static NPCFunctionDefinition CreateModifySceneObjectFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "modify_scene_object",
                description = "Modify a GameObject in the scene (enable/disable, move, change color, etc.)",
                category = "scene",
                requiredTrustLevel = 50,
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""objectName"": { ""type"": ""string"", ""description"": ""Name or path of the GameObject to modify"" },
                        ""action"": { ""type"": ""string"", ""enum"": [""enable"", ""disable"", ""move"", ""rotate"", ""scale"", ""change_color"", ""play_animation"", ""set_active""], ""description"": ""Action to perform"" },
                        ""position"": { ""type"": ""object"", ""properties"": { ""x"": { ""type"": ""number"" }, ""y"": { ""type"": ""number"" }, ""z"": { ""type"": ""number"" } }, ""description"": ""New position (for move action)"" },
                        ""rotation"": { ""type"": ""object"", ""properties"": { ""x"": { ""type"": ""number"" }, ""y"": { ""type"": ""number"" }, ""z"": { ""type"": ""number"" } }, ""description"": ""New rotation in euler angles (for rotate action)"" },
                        ""scale"": { ""type"": ""object"", ""properties"": { ""x"": { ""type"": ""number"" }, ""y"": { ""type"": ""number"" }, ""z"": { ""type"": ""number"" } }, ""description"": ""New scale (for scale action)"" },
                        ""color"": { ""type"": ""object"", ""properties"": { ""r"": { ""type"": ""number"" }, ""g"": { ""type"": ""number"" }, ""b"": { ""type"": ""number"" }, ""a"": { ""type"": ""number"" } }, ""description"": ""New color (for change_color action)"" },
                        ""animationName"": { ""type"": ""string"", ""description"": ""Animation to play (for play_animation action)"" }
                    },
                    ""required"": [""objectName"", ""action""]
                }",
            };
        }

        /// <summary>
        /// Creates a function definition for updating player trust.
        /// </summary>
        public static NPCFunctionDefinition CreateUpdateTrustFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "update_trust",
                description = "Modify the player's trust level with this NPC",
                category = "relationship",
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""change"": { ""type"": ""integer"", ""description"": ""Amount to change trust by (positive or negative)"" },
                        ""reason"": { ""type"": ""string"", ""description"": ""Why trust changed"" }
                    },
                    ""required"": [""change"", ""reason""]
                }",
            };
        }

        /// <summary>
        /// Creates a function definition for unlocking dialogue topics.
        /// </summary>
        public static NPCFunctionDefinition CreateUnlockTopicFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "unlock_dialogue_topic",
                description = "Unlock a new dialogue topic or quest for the player",
                category = "quest",
                requiredTrustLevel = 40,
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""topicId"": { ""type"": ""string"", ""description"": ""Unique identifier for the topic/quest"" },
                        ""topicName"": { ""type"": ""string"", ""description"": ""Display name of the topic"" },
                        ""description"": { ""type"": ""string"", ""description"": ""What this topic is about"" }
                    },
                    ""required"": [""topicId"", ""topicName""]
                }",
            };
        }

        /// <summary>
        /// Creates a function definition for spawning objects.
        /// </summary>
        public static NPCFunctionDefinition CreateSpawnObjectFunction()
        {
            return new NPCFunctionDefinition
            {
                functionName = "spawn_object",
                description = "Spawn a new GameObject in the scene near the NPC",
                category = "scene",
                requiredTrustLevel = 60,
                parametersJsonSchema =
                    @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""prefabName"": { ""type"": ""string"", ""description"": ""Name of the prefab to spawn"" },
                        ""offset"": { ""type"": ""object"", ""properties"": { ""x"": { ""type"": ""number"" }, ""y"": { ""type"": ""number"" }, ""z"": { ""type"": ""number"" } }, ""description"": ""Offset from NPC position"" },
                        ""parentToNPC"": { ""type"": ""boolean"", ""default"": false, ""description"": ""Whether to parent the spawned object to the NPC"" }
                    },
                    ""required"": [""prefabName""]
                }",
            };
        }
    }

    /// <summary>
    /// Represents a function call request from the LLM.
    /// </summary>
    [Serializable]
    public class NPCFunctionCall
    {
        public string name;
        public string arguments; // JSON string
        public string callId; // Unique ID for this call

        public T GetArguments<T>()
            where T : class
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return null;
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(arguments);
        }
    }

    /// <summary>
    /// Result of a function execution.
    /// </summary>
    [Serializable]
    public class NPCFunctionResult
    {
        public string callId;
        public string functionName;
        public bool success;
        public string result; // JSON string or plain text
        public string error;

        public static NPCFunctionResult Success(string callId, string functionName, string result)
        {
            return new NPCFunctionResult
            {
                callId = callId,
                functionName = functionName,
                success = true,
                result = result,
            };
        }

        public static NPCFunctionResult Failure(string callId, string functionName, string error)
        {
            return new NPCFunctionResult
            {
                callId = callId,
                functionName = functionName,
                success = false,
                error = error,
            };
        }
    }
}
