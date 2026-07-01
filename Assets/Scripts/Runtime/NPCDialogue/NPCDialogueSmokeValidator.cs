using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(500)]
    public class NPCDialogueSmokeValidator : MonoBehaviour
    {
        public NPCDialogueManager dialogueManager;
        public LLM chatLLM;
        public LLM ragLLM;
        public LLMAgent llmAgent;
        public RAG rag;
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
            ok &= Require(logger, chatLLM != null, "Chat LLM is assigned.");
            ok &= Require(logger, ragLLM != null, "RAG LLM is assigned.");
            ok &= Require(logger, llmAgent != null, "LLMAgent is assigned.");
            ok &= Require(logger, rag != null, "RAG is assigned.");

            if (dialogueManager != null)
            {
                ok &= Require(logger, dialogueManager.llm == chatLLM, "NPCDialogueManager.llm points to the chat LLM.");
                ok &= Require(logger, dialogueManager.llmAgent == llmAgent, "NPCDialogueManager.llmAgent points to LLMAgent.");
                ok &= Require(logger, dialogueManager.rag == rag, "NPCDialogueManager.rag points to RAG.");
                ok &= Require(logger, !string.IsNullOrWhiteSpace(dialogueManager.ragEmbeddingPath), "NPCDialogueManager.ragEmbeddingPath is set.");
            }

            if (chatLLM != null)
            {
                ok &= Require(logger, !chatLLM.embeddingsOnly, $"Chat LLM is completion-capable ({chatLLM.model}).");
            }

            if (ragLLM != null)
            {
                ok &= Require(logger, ragLLM.embeddingsOnly, $"RAG LLM is embedding-only ({ragLLM.model}).");
                ok &= Require(logger, ragLLM.embeddingLength > 0, $"RAG LLM embedding length is set ({ragLLM.embeddingLength}).");
            }

            if (llmAgent != null)
            {
                ok &= Require(logger, llmAgent.llm == chatLLM, "LLMAgent.llm points to the chat LLM.");
            }

            if (rag != null && rag.search != null && rag.search.llmEmbedder != null)
            {
                ok &= Require(logger, rag.search.llmEmbedder.llm == ragLLM, "RAG embedder points to the RAG LLM.");
            }
            else if (rag != null)
            {
                ok = false;
                logger.Log(NPCFlowStage.SmokeValidation, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "RAG search/embedder is not initialized.", source: nameof(NPCDialogueSmokeValidator));
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
            if (llmAgent == null) llmAgent = FindAnyObjectByType<LLMAgent>(FindObjectsInactive.Include);
            if (rag == null) rag = FindAnyObjectByType<RAG>(FindObjectsInactive.Include);
            if (chatLLM == null && dialogueManager != null) chatLLM = dialogueManager.llm;
            if (chatLLM == null && llmAgent != null) chatLLM = llmAgent.llm;
            if (ragLLM == null && rag != null && rag.search != null && rag.search.llmEmbedder != null) ragLLM = rag.search.llmEmbedder.llm;
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
