using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Session;
using NPCSystem.Items;
using NPCSystem.Monitoring;
using NPCSystem.Network.Core;
using UnityEngine;

namespace NPCSystem.Dialogue.FunctionCalling
{
    /// <summary>
    /// Executes NPC function calls and manages function registry.
    /// Functions are registered by NPCProfile and executed with context.
    /// </summary>
    public class NPCFunctionExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private NPCDialogueManager _dialogueManager;

        [SerializeField]
        private NPCDialogueSessionService _sessionService;

        [SerializeField]
        private NPCDialogueHistoryService _historyService;

        [SerializeField]
        private PlayerDialogueContextService _contextService;

        [SerializeField]
        private NPCNetworkSessionManager _networkSessionManager;

        [SerializeField]
        private ItemCatalog _itemCatalog;

        [Header("Scene References")]
        [SerializeField]
        private Transform _npcTransform;

        [SerializeField]
        private GameObject _sceneObjectsRoot;

        // Function registry: functionName -> handler
        private readonly Dictionary<
            string,
            Func<NPCFunctionCall, NPCProfile, Task<NPCFunctionResult>>
        > _functionHandlers = new Dictionary<string, Func<NPCFunctionCall, NPCProfile, Task<NPCFunctionResult>>>(
            StringComparer.OrdinalIgnoreCase
        );

        // Cooldown tracking
        private readonly Dictionary<string, float> _functionCooldowns = new Dictionary<string, float>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, int> _functionCallCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );

        // Mood state
        private string _currentMood = "neutral";
        private float _moodExpiresAt = 0f;
        private readonly Dictionary<string, int> _unlockedTopics = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );

        // Events
        public event Action<string, NPCFunctionResult> OnFunctionExecuted;
        public event Action<string> OnMoodChanged;

        private NPCFlowLogger _logger;
        private bool _initialized;

        void Awake()
        {
            _logger = NPCFlowLogger.FindOrCreate();
            RegisterBuiltInFunctions();
        }

        void Update()
        {
            // Update mood expiration
            if (_moodExpiresAt > 0f && Time.time >= _moodExpiresAt)
            {
                SetMoodInternal("neutral", "Mood duration expired", false);
            }
        }

        /// <summary>
        /// Initialize with required service references.
        /// </summary>
        public void Initialize(
            NPCDialogueManager dialogueManager,
            NPCDialogueSessionService sessionService,
            NPCDialogueHistoryService historyService,
            PlayerDialogueContextService contextService,
            NPCNetworkSessionManager networkSessionManager,
            ItemCatalog itemCatalog
        )
        {
            _dialogueManager = dialogueManager;
            _sessionService = sessionService;
            _historyService = historyService;
            _contextService = contextService;
            _networkSessionManager = networkSessionManager;
            _itemCatalog = itemCatalog;

            // Try to find NPC transform if not set
            if (_npcTransform == null)
            {
                var npcCharacter = GetComponentInParent<NPCServerCharacter>();
                if (npcCharacter != null)
                    _npcTransform = npcCharacter.transform;
                else
                    _npcTransform = transform;
            }

            _initialized = true;
            _logger?.Log(
                NPCFlowStage.SystemInit,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Debug,
                "NPCFunctionExecutor initialized",
                source: nameof(NPCFunctionExecutor)
            );
        }

        /// <summary>
        /// Register all built-in function handlers.
        /// </summary>
        private void RegisterBuiltInFunctions()
        {
            RegisterFunction("give_item", ExecuteGiveItemAsync);
            RegisterFunction("change_mood", ExecuteChangeMoodAsync);
            RegisterFunction("modify_scene_object", ExecuteModifySceneObjectAsync);
            RegisterFunction("update_trust", ExecuteUpdateTrustAsync);
            RegisterFunction("unlock_dialogue_topic", ExecuteUnlockDialogueTopicAsync);
            RegisterFunction("spawn_object", ExecuteSpawnObjectAsync);
        }

        /// <summary>
        /// Register a function handler.
        /// </summary>
        public void RegisterFunction(
            string functionName,
            Func<NPCFunctionCall, NPCProfile, Task<NPCFunctionResult>> handler
        )
        {
            _functionHandlers[functionName] = handler;
        }

        /// <summary>
        /// Execute a function call from the LLM.
        /// </summary>
        public async Task<NPCFunctionResult> ExecuteFunctionAsync(NPCFunctionCall call, NPCProfile profile)
        {
            if (!_initialized)
            {
                return NPCFunctionResult.Failure(call.callId, call.name, "FunctionExecutor not initialized");
            }

            // Check if function exists
            if (!_functionHandlers.TryGetValue(call.name, out var handler))
            {
                return NPCFunctionResult.Failure(call.callId, call.name, $"Unknown function: {call.name}");
            }

            // Check trust requirement
            var funcDef = profile?.GetFunctionDefinition(call.name);
            if (funcDef != null)
            {
                var playerCtx = await _contextService?.GetOrLoadContextAsync(profile.GetNpcSlug());
                if (playerCtx != null && playerCtx.TrustScore < funcDef.requiredTrustLevel)
                {
                    return NPCFunctionResult.Failure(
                        call.callId,
                        call.name,
                        $"Insufficient trust level. Required: {funcDef.requiredTrustLevel}, Current: {playerCtx.TrustScore}"
                    );
                }

                // Check cooldown
                if (funcDef.cooldownSeconds > 0f)
                {
                    var cooldownKey = $"{profile.GetNpcSlug()}.{call.name}";
                    if (_functionCooldowns.TryGetValue(cooldownKey, out float lastCallTime))
                    {
                        if (Time.time - lastCallTime < funcDef.cooldownSeconds)
                        {
                            return NPCFunctionResult.Failure(
                                call.callId,
                                call.name,
                                $"Function on cooldown. Try again in {funcDef.cooldownSeconds - (Time.time - lastCallTime):F1}s"
                            );
                        }
                    }
                }

                // Check max calls per session
                if (funcDef.maxCallsPerSession > 0)
                {
                    var countKey = $"{profile.GetNpcSlug()}.{call.name}";
                    if (_functionCallCounts.TryGetValue(countKey, out int count) && count >= funcDef.maxCallsPerSession)
                    {
                        return NPCFunctionResult.Failure(
                            call.callId,
                            call.name,
                            $"Maximum calls per session reached ({funcDef.maxCallsPerSession})"
                        );
                    }
                }
            }

            try
            {
                _logger?.Log(
                    NPCFlowStage.DialogueGeneration,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Debug,
                    $"Executing function: {call.name}",
                    source: nameof(NPCFunctionExecutor),
                    data: new Dictionary<string, object> { ["function"] = call.name, ["arguments"] = call.arguments }
                );

                var result = await handler(call, profile);

                // Update cooldown and call count
                if (funcDef != null)
                {
                    if (funcDef.cooldownSeconds > 0f)
                    {
                        var cooldownKey = $"{profile.GetNpcSlug()}.{call.name}";
                        _functionCooldowns[cooldownKey] = Time.time;
                    }
                    if (funcDef.maxCallsPerSession > 0)
                    {
                        var countKey = $"{profile.GetNpcSlug()}.{call.name}";
                        _functionCallCounts[countKey] = _functionCallCounts.GetValueOrDefault(countKey, 0) + 1;
                    }
                }

                OnFunctionExecuted?.Invoke(call.name, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Log(
                    NPCFlowStage.DialogueGeneration,
                    NPCFlowStatus.Error,
                    NPCFlowLogLevel.Error,
                    $"Function execution failed: {ex.Message}",
                    source: nameof(NPCFunctionExecutor),
                    data: new Dictionary<string, object> { ["function"] = call.name, ["exception"] = ex.ToString() }
                );

                return NPCFunctionResult.Failure(call.callId, call.name, ex.Message);
            }
        }

        /// <summary>
        /// Execute multiple function calls in sequence.
        /// </summary>
        public async Task<List<NPCFunctionResult>> ExecuteFunctionsAsync(
            List<NPCFunctionCall> calls,
            NPCProfile profile
        )
        {
            var results = new List<NPCFunctionResult>();
            foreach (var call in calls)
            {
                var result = await ExecuteFunctionAsync(call, profile);
                results.Add(result);

                // Stop on first failure if desired
                if (!result.success)
                {
                    _logger?.Log(
                        NPCFlowStage.DialogueGeneration,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Function {call.name} failed, stopping chain",
                        source: nameof(NPCFunctionExecutor)
                    );
                    break;
                }
            }
            return results;
        }

        // ==================== Built-in Function Implementations ====================

        private async Task<NPCFunctionResult> ExecuteGiveItemAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<GiveItemArgs>();
            if (args == null || string.IsNullOrWhiteSpace(args.itemId))
                return NPCFunctionResult.Failure(call.callId, call.name, "Invalid arguments: itemId required");

            if (_itemCatalog == null)
                return NPCFunctionResult.Failure(call.callId, call.name, "ItemCatalog not available");

            var itemDef = _itemCatalog.GetItem(args.itemId);
            if (itemDef == null)
                return NPCFunctionResult.Failure(call.callId, call.name, $"Item not found: {args.itemId}");

            // Add to player inventory via network session manager
            if (_networkSessionManager != null)
            {
                // This would need to be implemented in NPCNetworkSessionManager
                // For now, we'll log and return success
                _logger?.Log(
                    NPCFlowStage.DialogueGeneration,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    $"NPC {profile.GetDisplayName()} gave {args.quantity}x {itemDef.DisplayName} to player. Reason: {args.reason}",
                    source: nameof(NPCFunctionExecutor)
                );
            }

            var result = new
            {
                itemId = args.itemId,
                itemName = itemDef.DisplayName,
                quantity = args.quantity,
                reason = args.reason,
            };

            return NPCFunctionResult.Success(
                call.callId,
                call.name,
                Newtonsoft.Json.JsonConvert.SerializeObject(result)
            );
        }

        private async Task<NPCFunctionResult> ExecuteChangeMoodAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<ChangeMoodArgs>();
            if (args == null || string.IsNullOrWhiteSpace(args.mood))
                return NPCFunctionResult.Failure(call.callId, call.name, "Invalid arguments: mood required");

            var validMoods = new[]
            {
                "happy",
                "sad",
                "angry",
                "neutral",
                "excited",
                "suspicious",
                "friendly",
                "hostile",
            };
            if (Array.IndexOf(validMoods, args.mood.ToLower()) < 0)
                return NPCFunctionResult.Failure(call.callId, call.name, $"Invalid mood: {args.mood}");

            SetMoodInternal(args.mood.ToLower(), args.reason, true, args.duration);

            var result = new
            {
                previousMood = _currentMood,
                newMood = args.mood.ToLower(),
                reason = args.reason,
                duration = args.duration,
            };

            return NPCFunctionResult.Success(
                call.callId,
                call.name,
                Newtonsoft.Json.JsonConvert.SerializeObject(result)
            );
        }

        private async Task<NPCFunctionResult> ExecuteModifySceneObjectAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<ModifySceneObjectArgs>();
            if (args == null || string.IsNullOrWhiteSpace(args.objectName))
                return NPCFunctionResult.Failure(call.callId, call.name, "Invalid arguments: objectName required");

            // Find the GameObject
            var targetObj = FindSceneObject(args.objectName);
            if (targetObj == null)
                return NPCFunctionResult.Failure(call.callId, call.name, $"GameObject not found: {args.objectName}");

            try
            {
                switch (args.action?.ToLower())
                {
                    case "enable":
                        targetObj.SetActive(true);
                        break;
                    case "disable":
                        targetObj.SetActive(false);
                        break;
                    case "set_active":
                        targetObj.SetActive(args.active ?? true);
                        break;
                    case "move":
                        if (args.position != null)
                            targetObj.transform.position = new Vector3(
                                args.position.x,
                                args.position.y,
                                args.position.z
                            );
                        break;
                    case "rotate":
                        if (args.rotation != null)
                            targetObj.transform.rotation = Quaternion.Euler(
                                args.rotation.x,
                                args.rotation.y,
                                args.rotation.z
                            );
                        break;
                    case "scale":
                        if (args.scale != null)
                            targetObj.transform.localScale = new Vector3(args.scale.x, args.scale.y, args.scale.z);
                        break;
                    case "change_color":
                        if (args.color != null)
                        {
                            var renderer = targetObj.GetComponent<Renderer>();
                            if (renderer != null)
                            {
                                var mat = renderer.material;
                                mat.color = new Color(args.color.r, args.color.g, args.color.b, args.color.a);
                            }
                            else
                            {
                                return NPCFunctionResult.Failure(
                                    call.callId,
                                    call.name,
                                    "No Renderer component on target object"
                                );
                            }
                        }
                        break;
                    case "play_animation":
                        if (!string.IsNullOrWhiteSpace(args.animationName))
                        {
                            var animator = targetObj.GetComponent<Animator>();
                            if (animator != null)
                                animator.Play(args.animationName);
                            else
                                return NPCFunctionResult.Failure(
                                    call.callId,
                                    call.name,
                                    "No Animator component on target object"
                                );
                        }
                        break;
                    default:
                        return NPCFunctionResult.Failure(call.callId, call.name, $"Unknown action: {args.action}");
                }

                var result = new
                {
                    objectName = args.objectName,
                    action = args.action,
                    success = true,
                };

                return NPCFunctionResult.Success(
                    call.callId,
                    call.name,
                    Newtonsoft.Json.JsonConvert.SerializeObject(result)
                );
            }
            catch (Exception ex)
            {
                return NPCFunctionResult.Failure(call.callId, call.name, ex.Message);
            }
        }

        private async Task<NPCFunctionResult> ExecuteUpdateTrustAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<UpdateTrustArgs>();
            if (args == null)
                return NPCFunctionResult.Failure(
                    call.callId,
                    call.name,
                    "Invalid arguments: change and reason required"
                );

            // Update trust via context service
            if (_contextService != null)
            {
                var npcSlug = profile.GetNpcSlug();
                var ctx = await _contextService.GetOrLoadContextAsync(npcSlug);
                ctx.TrustScore = Mathf.Clamp(ctx.TrustScore + args.change, 0, 100);
                _contextService.InvalidateContext(npcSlug);

                // Persist to Supabase
                // This would need a method in PlayerDialogueContextService to save trust changes
            }

            var result = new
            {
                trustChange = args.change,
                reason = args.reason,
                newTrustScore = 0, // Would need to fetch updated value
            };

            return NPCFunctionResult.Success(
                call.callId,
                call.name,
                Newtonsoft.Json.JsonConvert.SerializeObject(result)
            );
        }

        private async Task<NPCFunctionResult> ExecuteUnlockDialogueTopicAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<UnlockTopicArgs>();
            if (args == null || string.IsNullOrWhiteSpace(args.topicId))
                return NPCFunctionResult.Failure(call.callId, call.name, "Invalid arguments: topicId required");

            _unlockedTopics[args.topicId] = 1; // Could store unlock timestamp or stage

            var result = new
            {
                topicId = args.topicId,
                topicName = args.topicName,
                description = args.description,
                unlocked = true,
            };

            _logger?.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                $"NPC {profile.GetDisplayName()} unlocked dialogue topic: {args.topicName}",
                source: nameof(NPCFunctionExecutor)
            );

            return NPCFunctionResult.Success(
                call.callId,
                call.name,
                Newtonsoft.Json.JsonConvert.SerializeObject(result)
            );
        }

        private async Task<NPCFunctionResult> ExecuteSpawnObjectAsync(NPCFunctionCall call, NPCProfile profile)
        {
            var args = call.GetArguments<SpawnObjectArgs>();
            if (args == null || string.IsNullOrWhiteSpace(args.prefabName))
                return NPCFunctionResult.Failure(call.callId, call.name, "Invalid arguments: prefabName required");

            // This would need a prefab registry or Addressables system
            // For now, we'll log and return a mock result
            _logger?.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                $"NPC {profile.GetDisplayName()} requested spawn of prefab: {args.prefabName}",
                source: nameof(NPCFunctionExecutor)
            );

            var result = new
            {
                prefabName = args.prefabName,
                spawned = false, // Would be true if actually spawned
                message = "Spawn requested - requires prefab registry implementation",
            };

            return NPCFunctionResult.Success(
                call.callId,
                call.name,
                Newtonsoft.Json.JsonConvert.SerializeObject(result)
            );
        }

        // ==================== Helper Methods ====================

        private GameObject FindSceneObject(string objectName)
        {
            // Try direct find first
            var obj = GameObject.Find(objectName);
            if (obj != null)
                return obj;

            // Try finding under scene objects root
            if (_sceneObjectsRoot != null)
            {
                var transform = _sceneObjectsRoot.transform.Find(objectName);
                if (transform != null)
                    return transform.gameObject;
            }

            // Try finding by path (e.g., "Parent/Child")
            obj = GameObject.Find(objectName);
            return obj;
        }

        private void SetMoodInternal(string mood, string reason, bool notify, int durationSeconds = 0)
        {
            var previousMood = _currentMood;
            _currentMood = mood;
            _moodExpiresAt = durationSeconds > 0 ? Time.time + durationSeconds : 0f;

            _logger?.Log(
                NPCFlowStage.DialogueGeneration,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                $"NPC mood changed: {previousMood} -> {mood} ({reason})",
                source: nameof(NPCFunctionExecutor)
            );

            if (notify && previousMood != mood)
            {
                OnMoodChanged?.Invoke(mood);
            }
        }

        /// <summary>
        /// Get the current NPC mood.
        /// </summary>
        public string GetCurrentMood() => _currentMood;

        /// <summary>
        /// Check if a dialogue topic is unlocked.
        /// </summary>
        public bool IsTopicUnlocked(string topicId) => _unlockedTopics.ContainsKey(topicId);

        /// <summary>
        /// Get all unlocked topic IDs.
        /// </summary>
        public IReadOnlyCollection<string> GetUnlockedTopics() => _unlockedTopics.Keys;

        /// <summary>
        /// Reset function call tracking (e.g., on new session).
        /// </summary>
        public void ResetSessionTracking()
        {
            _functionCooldowns.Clear();
            _functionCallCounts.Clear();
        }
    }

    // ==================== Argument Classes ====================

    [Serializable]
    public class GiveItemArgs
    {
        public string itemId;
        public int quantity = 1;
        public string reason;
    }

    [Serializable]
    public class ChangeMoodArgs
    {
        public string mood;
        public string reason;
        public int duration = 0;
    }

    [Serializable]
    public class ModifySceneObjectArgs
    {
        public string objectName;
        public string action;
        public Vector3Serializable position;
        public Vector3Serializable rotation;
        public Vector3Serializable scale;
        public ColorSerializable color;
        public string animationName;
        public bool? active;
    }

    [Serializable]
    public class UpdateTrustArgs
    {
        public int change;
        public string reason;
    }

    [Serializable]
    public class UnlockTopicArgs
    {
        public string topicId;
        public string topicName;
        public string description;
    }

    [Serializable]
    public class SpawnObjectArgs
    {
        public string prefabName;
        public Vector3Serializable offset;
        public bool parentToNPC = false;
    }

    [Serializable]
    public struct Vector3Serializable
    {
        public float x,
            y,
            z;
    }

    [Serializable]
    public struct ColorSerializable
    {
        public float r,
            g,
            b,
            a = 1f;
    }
}
