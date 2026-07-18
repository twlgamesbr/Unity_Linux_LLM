using System.Collections.Generic;
using System.Threading.Tasks;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DefaultExecutionOrder(500)]
    public class NPCDialogueSmokeValidator : MonoBehaviour
    {
        [FoldoutGroup("References", true, nameof(_dialogueManager), nameof(_chatClient), nameof(_localRag))]
        [SerializeField]
        EditorAttributes.Void referencesGroup;

        [FormerlySerializedAs("dialogueManager")]
        [SerializeField, HideProperty, Required]
        public NPCDialogueManager _dialogueManager;

        [FormerlySerializedAs("chatClient")]
        [SerializeField, HideProperty, Required]
        public NPCLocalAIClient _chatClient;

        [FormerlySerializedAs("localRag")]
        [SerializeField, HideProperty]
        public NPCLocalRAG _localRag;

        [FoldoutGroup("Smoke Test Settings", true, nameof(validateOnStart), nameof(runFirstQuestionSmokeOnStart), nameof(smokeQuestion), nameof(smokeTimeoutSeconds))]
        [SerializeField]
        EditorAttributes.Void smokeSettingsGroup;

        [SerializeField, HideProperty]
        public bool validateOnStart = true;

        [SerializeField, HideProperty]
        public bool runFirstQuestionSmokeOnStart = false;

        [TextArea(2, 4)]
        [SerializeField, HideProperty]
        public string smokeQuestion = "What should I investigate first?";

        [SerializeField, HideProperty, Suffix("s")]
        public float smokeTimeoutSeconds = 60f;

        [Title("Runtime Status")]
        [ShowInInspector, ReadOnly]
        bool HasAllReferences =>
            _dialogueManager != null && _chatClient != null;

        bool _responseCompleted;
        string _lastResponse = string.Empty;

        async void Start()
        {
            ResolveReferences();

            if (validateOnStart)
            {
                if (_dialogueManager != null && !_dialogueManager.IsInitialized)
                {
                    NPCFlowLogger
                        .FindOrCreate()
                        .Log(
                            NPCFlowStage.SmokeValidation,
                            NPCFlowStatus.Skipped,
                            NPCFlowLogLevel.Info,
                            "Skipping Start validation because NPCDialogueManager is not initialized yet (deferred loading active).",
                            source: nameof(NPCDialogueSmokeValidator)
                        );
                }
                else
                {
                    ValidateConfiguration();
                }
            }

            if (runFirstQuestionSmokeOnStart)
            {
                await RunFirstQuestionSmokeAsync();
            }
        }

        [Button("Validate Configuration")]
        public void ValidateConfiguration()
        {
            ResolveReferences();

            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            bool ok = true;
            ok &= Require(logger, _dialogueManager != null, "NPCDialogueManager is assigned.");
            ok &= Require(logger, _chatClient != null, "NPCLocalAIClient is assigned.");
            ok &= Require(logger, _localRag != null, "NPCLocalRAG is assigned.");

            if (_dialogueManager != null)
            {
                ok &= Require(
                    logger,
                    _dialogueManager._chatClient == _chatClient,
                    "NPCDialogueManager.ChatClient points to the chat client."
                );
                ok &= Require(
                    logger,
                    _dialogueManager._localRag == _localRag,
                    "NPCDialogueManager.LocalRag points to the local RAG."
                );
                ok &= Require(
                    logger,
                    !string.IsNullOrWhiteSpace(_dialogueManager._ragEmbeddingPath),
                    "NPCDialogueManager.RagEmbeddingPath is set."
                );
            }

            logger.Log(
                NPCFlowStage.SmokeValidation,
                ok ? NPCFlowStatus.Success : NPCFlowStatus.Error,
                ok ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Error,
                ok
                    ? "NPC dialogue configuration validation passed."
                    : "NPC dialogue configuration validation failed.",
                source: nameof(NPCDialogueSmokeValidator)
            );
        }

        [Button("Run First Question Smoke")]
        public async void RunFirstQuestionSmoke()
        {
            await RunFirstQuestionSmokeAsync();
        }

        public async Task RunFirstQuestionSmokeAsync()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            using var scope = NPCFlowScope.Start(
                logger,
                NPCFlowStage.SmokeValidation,
                nameof(NPCDialogueSmokeValidator),
                data: new Dictionary<string, object> { ["timeoutSeconds"] = smokeTimeoutSeconds }
            );
            ResolveReferences();
            ValidateConfiguration();

            if (_dialogueManager == null)
            {
                logger.Log(
                    NPCFlowStage.SmokeValidation,
                    NPCFlowStatus.Error,
                    NPCFlowLogLevel.Error,
                    "Cannot run smoke test without NPCDialogueManager.",
                    source: nameof(NPCDialogueSmokeValidator)
                );
                scope.Error(null, "Smoke failed: missing NPCDialogueManager.");
                Application.Quit(1);
                return;
            }

            await _dialogueManager.InitializeAsync();
            if (_dialogueManager.currentProfile == null)
            {
                string defaultSlug = _dialogueManager.GetDefaultProfileSlug();
                if (!string.IsNullOrWhiteSpace(defaultSlug))
                {
                    await _dialogueManager.SwitchToNPCAsync(defaultSlug);
                }
            }

            if (_dialogueManager.currentProfile == null)
            {
                logger.Log(
                    NPCFlowStage.SmokeValidation,
                    NPCFlowStatus.Error,
                    NPCFlowLogLevel.Error,
                    "Smoke failed: no default NPC could be selected.",
                    source: nameof(NPCDialogueSmokeValidator)
                );
                scope.Error(null, "Smoke failed: no default NPC selected.");
                Application.Quit(1);
                return;
            }

            _responseCompleted = false;
            _lastResponse = string.Empty;
            DialogueManager.OnResponseComplete.AddListener(HandleSmokeResponseComplete);

            try
            {
                DialogueManager.SendDialogueMessage(smokeQuestion);
                float startTime = Time.realtimeSinceStartup;
                while (
                    !_responseCompleted
                    && Time.realtimeSinceStartup - startTime < smokeTimeoutSeconds
                )
                {
                    await Task.Yield();
                }

                if (!_responseCompleted)
                {
                    logger.Log(
                        NPCFlowStage.SmokeValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        "Smoke failed: timed out waiting for NPC response.",
                        source: nameof(NPCDialogueSmokeValidator),
                        data: new Dictionary<string, object>
                        {
                            ["timeoutSeconds"] = smokeTimeoutSeconds,
                            ["npcSlug"] = _dialogueManager.currentProfile.GetNpcSlug(),
                        }
                    );
                    scope.Error(
                        null,
                        "Smoke failed: timed out waiting for NPC response.",
                        new Dictionary<string, object>
                        {
                            ["timeoutSeconds"] = smokeTimeoutSeconds,
                            ["npcSlug"] = _dialogueManager.currentProfile.GetNpcSlug(),
                        }
                    );
                    Application.Quit(1);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_lastResponse))
                {
                    logger.Log(
                        NPCFlowStage.SmokeValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        "Smoke failed: NPC response was empty.",
                        source: nameof(NPCDialogueSmokeValidator),
                        data: new Dictionary<string, object>
                        {
                            ["npcSlug"] = _dialogueManager.currentProfile.GetNpcSlug(),
                        }
                    );
                    scope.Error(
                        null,
                        "Smoke failed: NPC response was empty.",
                        new Dictionary<string, object>
                        {
                            ["npcSlug"] = _dialogueManager.currentProfile.GetNpcSlug(),
                        }
                    );
                    Application.Quit(1);
                    return;
                }

                string logMsg =
                    $"Smoke test passed with {_dialogueManager.currentProfile.GetDisplayName()}. Response: {_lastResponse}";
                logger.Log(
                    NPCFlowStage.SmokeValidation,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    logMsg,
                    source: nameof(NPCDialogueSmokeValidator)
                );
                System.IO.File.WriteAllText("dialogue_log.txt", logMsg);
                scope.Success(
                    "First-question smoke passed.",
                    NPCFlowTextSanitizer.MergeSummary(
                        new Dictionary<string, object>
                        {
                            ["npcSlug"] = _dialogueManager._currentProfile.GetNpcSlug(),
                        },
                        "response",
                        _lastResponse,
                        includeSnippet: false,
                        maxSnippetChars: 0
                    )
                );

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                Application.Quit(0);
            }
            finally
            {
                _dialogueManager.OnResponseComplete.RemoveListener(HandleSmokeResponseComplete);
            }
        }

        void ResolveReferences()
        {
            if (_dialogueManager == null)
                _dialogueManager = FindAnyObjectByType<NPCDialogueManager>(
                    FindObjectsInactive.Include
                );
            if (_chatClient == null && _dialogueManager != null)
                _chatClient = _dialogueManager._chatClient;
            if (_chatClient == null)
                _chatClient = FindAnyObjectByType<NPCLocalAIClient>(FindObjectsInactive.Include);
            if (_localRag == null)
                _localRag = FindAnyObjectByType<NPCLocalRAG>(FindObjectsInactive.Include);
        }

        void HandleSmokeResponseComplete(string npcName, string response)
        {
            _lastResponse = response;
            _responseCompleted = true;
        }

        static bool Require(NPCFlowLogger logger, bool condition, string message)
        {
            if (condition)
            {
                logger.Log(
                    NPCFlowStage.SmokeValidation,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Debug,
                    message,
                    source: nameof(NPCDialogueSmokeValidator)
                );
                return true;
            }

            logger.Log(
                NPCFlowStage.SmokeValidation,
                NPCFlowStatus.Error,
                NPCFlowLogLevel.Error,
                message,
                source: nameof(NPCDialogueSmokeValidator)
            );
            return false;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/NPC System/Run CLI Smoke Test")]
        public static void RunSmokeTestCLI()
        {
            var validator = FindAnyObjectByType<NPCDialogueSmokeValidator>(
                FindObjectsInactive.Include
            );
            if (validator == null)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.SmokeValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        "No validator found in scene.",
                        source: nameof(NPCDialogueSmokeValidator)
                    );
                Application.Quit(1);
                return;
            }
            validator.RunFirstQuestionSmoke();
        }
#endif
    }
}
