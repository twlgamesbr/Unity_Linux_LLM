using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(500)]
    public class NPCDialogueSmokeValidator : MonoBehaviour
    {
        public NPCDialogueManager dialogueManager;
        public NPCLocalAIClient chatClient;
        public NPCLocalRAG localRag;
        public bool validateOnStart = true;
        public bool runFirstQuestionSmokeOnStart = false;
        public string smokeQuestion = "What should I investigate first?";
        public float smokeTimeoutSeconds = 60f;

        bool _responseCompleted;
        string _lastResponse = string.Empty;

        async void Start()
        {
            ResolveReferences();

            if (validateOnStart)
            {
                ValidateConfiguration();
            }

            if (runFirstQuestionSmokeOnStart)
            {
                await RunFirstQuestionSmokeAsync();
            }
        }

        [ContextMenu("Validate NPC Dialogue Configuration")]
        public void ValidateConfiguration()
        {
            ResolveReferences();

            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            bool ok = true;
            ok &= Require(logger, dialogueManager != null, "NPCDialogueManager is assigned.");
            ok &= Require(logger, chatClient != null, "NPCLocalAIClient is assigned.");
            ok &= Require(logger, localRag != null, "NPCLocalRAG is assigned.");

            if (dialogueManager != null)
            {
                ok &= Require(logger, dialogueManager.chatClient == chatClient, "NPCDialogueManager.chatClient points to the chat client.");
                ok &= Require(logger, dialogueManager.localRag == localRag, "NPCDialogueManager.localRag points to the local RAG.");
                ok &= Require(logger, !string.IsNullOrWhiteSpace(dialogueManager.ragEmbeddingPath), "NPCDialogueManager.ragEmbeddingPath is set.");
            }

            logger.Log(NPCFlowStage.SmokeValidation,
                ok ? NPCFlowStatus.Success : NPCFlowStatus.Error,
                ok ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Error,
                ok ? "NPC dialogue configuration validation passed." : "NPC dialogue configuration validation failed.",
                source: nameof(NPCDialogueSmokeValidator));
        }

        [ContextMenu("Run First Question Smoke")]
        public async void RunFirstQuestionSmoke()
        {
            await RunFirstQuestionSmokeAsync();
        }

        public async Task RunFirstQuestionSmokeAsync()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            using var scope = NPCFlowScope.Start(logger, NPCFlowStage.SmokeValidation, nameof(NPCDialogueSmokeValidator), data: new Dictionary<string, object>
            {
                ["timeoutSeconds"] = smokeTimeoutSeconds
            });
            ResolveReferences();
            ValidateConfiguration();

            if (dialogueManager == null)
            {
                logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "Cannot run smoke test without NPCDialogueManager.", source: nameof(NPCDialogueSmokeValidator));
                scope.Error(null, "Smoke failed: missing NPCDialogueManager.");
                Application.Quit(1);
                return;
            }

            await dialogueManager.InitializeAsync();
            if (dialogueManager.currentProfile == null)
            {
                string defaultSlug = dialogueManager.GetDefaultProfileSlug();
                if (!string.IsNullOrWhiteSpace(defaultSlug))
                {
                    await dialogueManager.SwitchToNPCAsync(defaultSlug);
                }
            }

            if (dialogueManager.currentProfile == null)
            {
                logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "Smoke failed: no default NPC could be selected.", source: nameof(NPCDialogueSmokeValidator));
                scope.Error(null, "Smoke failed: no default NPC selected.");
                Application.Quit(1);
                return;
            }

            _responseCompleted = false;
            _lastResponse = string.Empty;
            dialogueManager.onResponseComplete.AddListener(HandleSmokeResponseComplete);

            try
            {
                dialogueManager.SendMessage(smokeQuestion);
                float startTime = Time.realtimeSinceStartup;
                while (!_responseCompleted && Time.realtimeSinceStartup - startTime < smokeTimeoutSeconds)
                {
                    await Task.Yield();
                }

                if (!_responseCompleted)
                {
                    logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                        "Smoke failed: timed out waiting for NPC response.", source: nameof(NPCDialogueSmokeValidator),
                        data: new Dictionary<string, object>
                        {
                            ["timeoutSeconds"] = smokeTimeoutSeconds,
                            ["npcSlug"] = dialogueManager.currentProfile.GetNpcSlug()
                        });
                    scope.Error(null, "Smoke failed: timed out waiting for NPC response.", new Dictionary<string, object>
                    {
                        ["timeoutSeconds"] = smokeTimeoutSeconds,
                        ["npcSlug"] = dialogueManager.currentProfile.GetNpcSlug()
                    });
                    Application.Quit(1);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_lastResponse))
                {
                    logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                        "Smoke failed: NPC response was empty.", source: nameof(NPCDialogueSmokeValidator),
                        data: new Dictionary<string, object>
                        {
                            ["npcSlug"] = dialogueManager.currentProfile.GetNpcSlug()
                        });
                    scope.Error(null, "Smoke failed: NPC response was empty.", new Dictionary<string, object>
                    {
                        ["npcSlug"] = dialogueManager.currentProfile.GetNpcSlug()
                    });
                    Application.Quit(1);
                    return;
                }

                string logMsg = $"Smoke test passed with {dialogueManager.currentProfile.GetDisplayName()}. Response: {_lastResponse}";
                logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    logMsg, source: nameof(NPCDialogueSmokeValidator));
                System.IO.File.WriteAllText("dialogue_log.txt", logMsg);
                scope.Success("First-question smoke passed.", NPCFlowTextSanitizer.MergeSummary(
                    new Dictionary<string, object>
                    {
                        ["npcSlug"] = dialogueManager.currentProfile.GetNpcSlug()
                    },
                    "response",
                    _lastResponse,
                    includeSnippet: false,
                    maxSnippetChars: 0));

                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
                Application.Quit(0);
            }
            finally
            {
                dialogueManager.onResponseComplete.RemoveListener(HandleSmokeResponseComplete);
            }
        }

        void ResolveReferences()
        {
            if (dialogueManager == null) dialogueManager = FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
            if (chatClient == null && dialogueManager != null) chatClient = dialogueManager.chatClient;
            if (chatClient == null) chatClient = FindAnyObjectByType<NPCLocalAIClient>(FindObjectsInactive.Include);
            if (localRag == null) localRag = FindAnyObjectByType<NPCLocalRAG>(FindObjectsInactive.Include);
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
                logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Success, NPCFlowLogLevel.Debug,
                    message, source: nameof(NPCDialogueSmokeValidator));
                return true;
            }

            logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                message, source: nameof(NPCDialogueSmokeValidator));
            return false;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/NPC System/Run CLI Smoke Test")]
        public static void RunSmokeTestCLI()
        {
            var validator = FindAnyObjectByType<NPCDialogueSmokeValidator>(FindObjectsInactive.Include);
            if (validator == null)
            {
                NPCFlowLogger.FindOrCreate().Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "No validator found in scene.", source: nameof(NPCDialogueSmokeValidator));
                Application.Quit(1);
                return;
            }
            validator.RunFirstQuestionSmoke();
        }
#endif
    }
}
