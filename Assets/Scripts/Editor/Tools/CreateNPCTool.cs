using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GladeAgenticAI.Core.Tools;
using NPCSystem;

namespace GladeAgenticAI.Core.Tools.Implementations.NPC
{
    [InitializeOnLoad]
    public class CreateNPCTool : ITool
    {
        static CreateNPCTool()
        {
            GladeAgenticAI.Services.ToolExecutor.RegisterExternal(new CreateNPCTool());
        }

        public string Name => "create_npc";

        public string Execute(Dictionary<string, object> args)
        {
            string npcSlug = args.ContainsKey("npcSlug") ? args["npcSlug"].ToString() : "";
            string displayName = args.ContainsKey("displayName") ? args["displayName"].ToString() : "";
            string systemPrompt = args.ContainsKey("systemPrompt") ? args["systemPrompt"].ToString() : "You are a helpful in-game NPC.";
            string knowledgeText = args.ContainsKey("knowledgeText") ? args["knowledgeText"].ToString() : "";
            
            float temperature = 0.7f;
            if (args.ContainsKey("temperature") && float.TryParse(args["temperature"].ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t))
            {
                temperature = t;
            }

            if (string.IsNullOrEmpty(npcSlug))
            {
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "Create NPC tool rejected request with missing npcSlug.", nameof(CreateNPCTool));
                return ToolUtils.CreateErrorResponse("npcSlug is required.");
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = npcSlug;
            }

            try
            {
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Start, NPCFlowLogLevel.Info,
                    "Create NPC tool started.", nameof(CreateNPCTool), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["displayName"] = displayName,
                        ["knowledgeLength"] = knowledgeText?.Length ?? 0,
                        ["systemPromptLength"] = systemPrompt?.Length ?? 0
                    });

                // Create Data Directory
                string profilesDir = "Assets/Data/NPCProfiles";
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                if (!AssetDatabase.IsValidFolder(profilesDir)) AssetDatabase.CreateFolder("Assets/Data", "NPCProfiles");

                // Create StreamingAssets Directory for knowledge
                string streamingAssetsDir = Application.streamingAssetsPath;
                string npcsDir = Path.Combine(streamingAssetsDir, "NPCs");
                if (!Directory.Exists(npcsDir)) Directory.CreateDirectory(npcsDir);
                
                string specificNpcDir = Path.Combine(npcsDir, npcSlug);
                if (!Directory.Exists(specificNpcDir)) Directory.CreateDirectory(specificNpcDir);

                // Write knowledge file
                string knowledgePath = $"NPCs/{npcSlug}/knowledge.md";
                string fullKnowledgePath = Path.Combine(streamingAssetsDir, knowledgePath);
                File.WriteAllText(fullKnowledgePath, knowledgeText);

                // Create or Load Profile
                string profileAssetPath = $"{profilesDir}/{npcSlug}_Profile.asset";
                NPCProfile profile = AssetDatabase.LoadAssetAtPath<NPCProfile>(profileAssetPath);
                bool isNew = false;
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<NPCProfile>();
                    isNew = true;
                }

                profile.npcSlug = npcSlug;
                profile.displayName = displayName;
                profile.systemPrompt = systemPrompt;
                profile.temperature = temperature;
                profile.ragCategory = npcSlug;
                profile.knowledgeSourcePath = knowledgePath;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(profile, profileAssetPath);
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Add to Manager if available
                NPCDialogueManager manager = UnityEngine.Object.FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
                if (manager != null)
                {
                    List<NPCProfile> profileList = new List<NPCProfile>(manager.profiles != null ? manager.profiles : Array.Empty<NPCProfile>());
                    if (!profileList.Contains(profile))
                    {
                        profileList.Add(profile);
                        manager.profiles = profileList.ToArray();
                        EditorUtility.SetDirty(manager);
                        
                        // Force a rebuild of RAG index for this NPC
                        _ = manager.AddNPCKnowledge(displayName, knowledgeText);
                    }
                }

                // Ingest to Cognee asynchronously
                try
                {
                    string endpoint = "http://localhost:8000/api/v1";
                    var service = UnityEngine.Object.FindAnyObjectByType<GladeAgenticAI.Core.Memory.CogneeMemoryService>(FindObjectsInactive.Include);
                    if (service != null) endpoint = service.CogneeEndpoint;

                    string escapedData = knowledgeText?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                    string escapedUser = npcSlug?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                    string jsonPayload = $"{{\"data\": \"{escapedData}\", \"user_id\": \"{escapedUser}\"}}";
                    
                    Task.Run(async () => 
                    {
                        try
                        {
                            using (HttpClient client = new HttpClient())
                            {
                                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                                await client.PostAsync($"{endpoint}/add", content);
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.LogWarning($"[CreateNPCTool] Background Cognee Ingestion failed: {innerEx.Message}");
                            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                                "Create NPC background Cognee ingestion failed.", nameof(CreateNPCTool), new Dictionary<string, object>
                                {
                                    ["npcSlug"] = npcSlug,
                                    ["exceptionType"] = innerEx.GetType().Name,
                                    ["exceptionMessage"] = innerEx.Message
                                });
                        }
                    });
                    Debug.Log($"[CreateNPCTool] Sent knowledge for {npcSlug} to Cognee background ingestion.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CreateNPCTool] Cognee Ingestion setup failed: {ex.Message}");
                    NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                        "Create NPC Cognee ingestion setup failed.", nameof(CreateNPCTool), new Dictionary<string, object>
                        {
                            ["npcSlug"] = npcSlug,
                            ["exceptionType"] = ex.GetType().Name,
                            ["exceptionMessage"] = ex.Message
                        });
                }

                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "Create NPC tool completed.", nameof(CreateNPCTool), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["profileAssetPath"] = profileAssetPath,
                        ["knowledgePath"] = knowledgePath,
                        ["addedToManager"] = manager != null
                    });
                return ToolUtils.CreateSuccessResponse($"Created NPC '{displayName}' with slug '{npcSlug}' at '{profileAssetPath}'");
            }
            catch (Exception ex)
            {
                NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                    "Create NPC tool failed.", nameof(CreateNPCTool), new Dictionary<string, object>
                    {
                        ["npcSlug"] = npcSlug,
                        ["exceptionType"] = ex.GetType().Name,
                        ["exceptionMessage"] = ex.Message
                    });
                return ToolUtils.CreateErrorResponse($"Failed to create NPC: {ex.Message}");
            }
        }
    }
}
