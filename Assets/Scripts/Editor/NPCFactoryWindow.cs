using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using NPCSystem;
using LLMUnity;
using System.IO;

namespace GladeAgenticAI.Core.Tools.Implementations.NPC
{
    public class NPCFactoryWindow : EditorWindow
    {
        private string subject = "A grumpy blacksmith";
        private string personality = "Speaks in short sentences, values hard work, complains about the youth.";
        private string npcSlug = "blacksmith";
        
        private string generatedSystemPrompt = "";
        private string generatedKnowledgeText = "";
        
        // Chat Simulation
        private string testChatInput = "";
        private string testChatHistory = "";
        private UnityEngine.GameObject tempAgentObj;
        private LLMAgent tempAgent;

        private bool isGenerating = false;
        private string statusMessage = "";
        private Vector2 scrollPos;
        private Vector2 chatScrollPos;

        [MenuItem("Tools/NPC Factory 10x")]
        public static void ShowWindow()
        {
            GetWindow<NPCFactoryWindow>("NPC Factory 10x");
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            GUILayout.Label("10x Automated NPC Factory", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("1. Define the NPC", EditorStyles.boldLabel);
            npcSlug = EditorGUILayout.TextField("NPC Slug (ID)", npcSlug);
            
            GUILayout.Label("Subject");
            subject = EditorGUILayout.TextArea(subject, GUILayout.Height(40));
            
            GUILayout.Label("Personality & Background");
            personality = EditorGUILayout.TextArea(personality, GUILayout.Height(60));

            EditorGUILayout.Space();

            if (GUILayout.Button("2. Deep Generate Profile (LocalAI)", GUILayout.Height(30)))
            {
                if (!isGenerating)
                {
                    _ = DeepGenerateWithAI();
                }
            }

            if (isGenerating)
            {
                EditorGUILayout.HelpBox("Generating... Please wait.", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(generatedSystemPrompt) || !string.IsNullOrEmpty(generatedKnowledgeText))
            {
                GUILayout.Label("3. Generated Assets (Review & Edit)", EditorStyles.boldLabel);
                
                GUILayout.Label("System Prompt");
                generatedSystemPrompt = EditorGUILayout.TextArea(generatedSystemPrompt, GUILayout.Height(60));
                
                GUILayout.Label("Knowledge Base (RAG & Cognee)");
                generatedKnowledgeText = EditorGUILayout.TextArea(generatedKnowledgeText, GUILayout.Height(100));

                EditorGUILayout.Space();
                GUILayout.Label("4. Test Chat (Live LLM)", EditorStyles.boldLabel);
                chatScrollPos = EditorGUILayout.BeginScrollView(chatScrollPos, GUILayout.Height(150), GUILayout.ExpandHeight(true));
                EditorGUILayout.TextArea(testChatHistory, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                GUILayout.BeginHorizontal();
                testChatInput = EditorGUILayout.TextField(testChatInput);
                if (GUILayout.Button("Send", GUILayout.Width(60)))
                {
                    _ = SendTestChat();
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                if (GUILayout.Button("5. Ingest to Cognee & Create Asset", GUILayout.Height(40)))
                {
                    _ = FinalizeNPCAsync();
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        private async Task DeepGenerateWithAI()
        {
            isGenerating = true;
            statusMessage = "Connecting to LocalAI via LLMUnity...";
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Start, NPCFlowLogLevel.Info,
                "NPC factory deep generation started.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                {
                    ["npcSlug"] = npcSlug,
                    ["subjectLength"] = subject?.Length ?? 0,
                    ["personalityLength"] = personality?.Length ?? 0
                });
            
            try
            {
                LLM llm = FindAnyObjectByType<LLM>();
                if (llm == null)
                {
                    statusMessage = "Error: No LLM component found in scene. Please add one.";
                    NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                        "NPC factory generation failed: no LLM component in scene.", nameof(NPCFactoryWindow));
                    isGenerating = false;
                    return;
                }

                if (string.IsNullOrEmpty(npcSlug))
                {
                    npcSlug = subject.ToLower().Replace(" ", "-").Replace("'", "");
                    if (npcSlug.Length > 20) npcSlug = npcSlug.Substring(0, 20);
                }

                if (tempAgentObj == null)
                {
                    tempAgentObj = new UnityEngine.GameObject("TempFactoryAgent");
                    tempAgent = tempAgentObj.AddComponent<LLMAgent>();
                    tempAgent.llm = llm;
                }
                
                tempAgent.systemPrompt = "You are an expert game designer creating rich NPC profiles. Output exactly what is requested, no conversational filler.";
                
                await llm.WaitUntilReady();

                statusMessage = "Generating Deep System Prompt...";
                Repaint();
                
                string promptQuery = $"Write a comprehensive system prompt for a Unity NPC. Subject: '{subject}'. Personality: '{personality}'. Include behavior rules, speech quirks, and restrictions. Only output the system prompt.";
                generatedSystemPrompt = await tempAgent.Chat(promptQuery, null, null, false);

                statusMessage = "Generating Deep Knowledge Text for Cognee/RAG...";
                Repaint();

                string knowledgeQuery = $"Write a detailed knowledge base document (markdown format) for the NPC. Subject: '{subject}'. Traits: '{personality}'. Include deep background, secrets, relationships, and lore. This will be ingested into a vector graph database. Only output the knowledge text.";
                generatedKnowledgeText = await tempAgent.Chat(knowledgeQuery, null, null, false);
                
                // Initialize Test Chat
                tempAgent.systemPrompt = generatedSystemPrompt;
                await tempAgent.ClearHistory();
                testChatHistory = $"--- Test Chat with {subject} Started ---\n";
                
                statusMessage = "Generation Complete! You can now test chat and finalize.";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "NPC factory deep generation completed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["systemPromptLength"] = generatedSystemPrompt?.Length ?? 0,
                        ["knowledgeLength"] = generatedKnowledgeText?.Length ?? 0
                    });
            }
            catch (Exception e)
            {
                statusMessage = $"Error: {e.Message}";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "NPC factory deep generation failed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["exceptionType"] = e.GetType().Name,
                        ["exceptionMessage"] = e.Message
                    });
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        private async Task SendTestChat()
        {
            if (tempAgent == null || string.IsNullOrWhiteSpace(testChatInput) || isGenerating) return;

            string playerMsg = testChatInput;
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Start, NPCFlowLogLevel.Info,
                "NPC factory test chat started.", nameof(NPCFactoryWindow),
                NPCFlowTextSanitizer.MergeSummary(new Dictionary<string, object> { ["npcSlug"] = npcSlug }, "player", playerMsg, false, 0));
            testChatInput = "";
            testChatHistory += $"\nYou: {playerMsg}\n";
            isGenerating = true;
            statusMessage = "Waiting for NPC reply...";
            Repaint();

            try
            {
                string reply = await tempAgent.Chat(playerMsg, null, null, false);
                testChatHistory += $"{subject}: {reply}\n";
                statusMessage = "Idle.";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "NPC factory test chat completed.", nameof(NPCFactoryWindow),
                    NPCFlowTextSanitizer.MergeSummary(new Dictionary<string, object> { ["npcSlug"] = npcSlug }, "reply", reply, false, 0));
            }
            catch(Exception e)
            {
                statusMessage = $"Chat Error: {e.Message}";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "NPC factory test chat failed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["exceptionType"] = e.GetType().Name,
                        ["exceptionMessage"] = e.Message
                    });
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        private async Task FinalizeNPCAsync()
        {
            isGenerating = true;
            statusMessage = "Ingesting knowledge into Cognee Graph Database...";
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Start, NPCFlowLogLevel.Info,
                "NPC factory finalize started.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                {
                    ["npcSlug"] = npcSlug,
                    ["systemPromptLength"] = generatedSystemPrompt?.Length ?? 0,
                    ["knowledgeLength"] = generatedKnowledgeText?.Length ?? 0
                });
            Repaint();

            try
            {
                // Ingest to Cognee
                string endpoint = "http://localhost:8000/api/v1"; 
                var service = UnityEngine.Object.FindAnyObjectByType<GladeAgenticAI.Core.Memory.CogneeMemoryService>(FindObjectsInactive.Include);
                if (service != null) endpoint = service.CogneeEndpoint;

                string jsonPayload = $"{{\"data\": \"{EscapeJson(generatedKnowledgeText)}\", \"user_id\": \"{EscapeJson(npcSlug)}\"}}";
                using (UnityWebRequest request = new UnityWebRequest($"{endpoint}/add", "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    var op = request.SendWebRequest();
                    while (!op.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"Cognee Ingestion failed: {request.error}. Ensure Cognee backend is running.");
                        statusMessage = $"Cognee ingestion failed. Asset will still be created.";
                        NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                            "NPC factory Cognee ingestion failed; asset creation will continue.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                            {
                                ["npcSlug"] = npcSlug,
                                ["error"] = request.error
                            });
                    }
                    else
                    {
                        statusMessage = "Cognee Ingestion successful!";
                        Debug.Log($"Cognee Memory Ingestion Successful for {npcSlug}");
                        NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                            "NPC factory Cognee ingestion completed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                            {
                                ["npcSlug"] = npcSlug
                            });
                    }
                }

                // Create Asset via Tool
                var args = new Dictionary<string, object>
                {
                    { "npcSlug", npcSlug },
                    { "displayName", subject },
                    { "systemPrompt", generatedSystemPrompt },
                    { "knowledgeText", generatedKnowledgeText },
                    { "temperature", 0.7f }
                };

                CreateNPCTool tool = new CreateNPCTool();
                string result = tool.Execute(args);
                
                statusMessage = "NPC Built and Ingested Successfully!";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "NPC factory finalize completed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["toolResultLength"] = result?.Length ?? 0
                    });
                EditorUtility.DisplayDialog("Result", result, "OK");
            }
            catch (Exception ex)
            {
                statusMessage = $"Finalize Error: {ex.Message}";
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "NPC factory finalize failed.", nameof(NPCFactoryWindow), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    });
                EditorUtility.DisplayDialog("Error", ex.Message, "OK");
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private void OnDestroy()
        {
            if (tempAgentObj != null)
            {
                DestroyImmediate(tempAgentObj);
            }
        }
    }
}
