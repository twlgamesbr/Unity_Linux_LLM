---
name: unity-mcp-llm-workflow
description: Mandatory rules and context for building LLM features, GladeKit MCP tools, and NPC factories in this Unity project. Enforces VRAM limits, LocalAI routing, and Cognee memory infrastructure.
---

# Unity MCP & LLM Workflow Standards

This skill activates when working with Unity, LLMUnity, LocalAI, GladeKit MCP, Cognee, or the NPC Factory in this project. It ensures high-quality architecture, performance, and consistency.

## 1. VRAM & LocalAI Policy (CRITICAL)

The host machine has constrained VRAM (e.g., RTX 3060 Laptop GPU with 6GB). Running `llama.cpp` directly inside the Unity Editor process is highly discouraged as it leads to Out-Of-Memory crashes and poor editor performance.

*   **Rule:** `LLMUnity` must **always** be configured to use the `Remote` option.
*   **Implementation:** The `LLM` component in the scene must have `Remote` checked, and the Port must point to the workstation's running `LocalAI` instance (usually port `8080`).
*   **Benefit:** This offloads inference and memory management to LocalAI, which has optimized memory reclaimers and watchdogs, keeping Unity stable.

## 2. GladeKit MCP Tool Development

When creating new tools for the AI to use within Unity via GladeKit MCP, strict adherence to the following pattern is required:

*   **Interface:** All tools must implement `GladeAgenticAI.Core.Tools.ITool`.
*   **Namespace:** Tools should reside in `GladeAgenticAI.Core.Tools.Implementations.<Category>`.
*   **Registration:** Tools MUST auto-register themselves upon Unity load using the `[InitializeOnLoad]` attribute and a static constructor.

**Boilerplate Example:**
```csharp
using System.Collections.Generic;
using UnityEditor;
using GladeAgenticAI.Core.Tools;

namespace GladeAgenticAI.Core.Tools.Implementations.Custom
{
    [InitializeOnLoad]
    public class MyCustomTool : ITool
    {
        // Auto-register on load
        static MyCustomTool()
        {
            GladeAgenticAI.Services.ToolExecutor.RegisterExternal(new MyCustomTool());
        }

        public string Name => "my_custom_tool";

        public string Execute(Dictionary<string, object> args)
        {
            try
            {
                // Parse args safely
                string myArg = args.ContainsKey("myArg") ? args["myArg"].ToString() : "";
                if (string.IsNullOrEmpty(myArg)) return ToolUtils.CreateErrorResponse("myArg required");

                // ... implementation logic ...

                return ToolUtils.CreateSuccessResponse("Success");
            }
            catch (System.Exception ex)
            {
                return ToolUtils.CreateErrorResponse($"Failed: {ex.Message}");
            }
        }
    }
}
```

## 3. Cognee Memory Integration Layer

The project relies on Cognee for episodic and semantic memory to prevent context exhaustion and hallucination. 
*   **Backend:** Configured per `/mnt/data/Projects_SSD/pc-resource-agent-team/decisions/cognee-memory-policy.md`. It must use `SQLite` for metadata, `LanceDB` for vectors, and `Kuzu` for graphs.
*   **System Root:** The storage is local-first at `/mnt/data/Projects_SSD/pc-resource-agent-team/cognee/system`.
*   **Implementation:** Unity interacts with Cognee through REST API calls or local MCP bridges. The `CogneeMemoryService.cs` acts as the standard client layer in Unity to push conversation history and fetch context.

## 4. NPC Factory & Dialogue Manager

The project uses a custom `NPCSystem` utilizing `NPCDialogueManager` and `NPCProfile` ScriptableObjects.

*   **Profiles:** `NPCProfile` assets must be saved in `Assets/Data/NPCProfiles/`.
*   **Knowledge (RAG/Cognee):** While initial knowledge can be Markdown (`Assets/StreamingAssets/NPCs/{npcSlug}/knowledge.md`), memory integration requires pushing this to Cognee so that NPC conversations are context-aware.
*   **Integration:** After generating an NPC profile and saving its knowledge, the profile MUST be added to the active `NPCDialogueManager.profiles` array.

## 5. Subagent Utilization

For complex features, do not attempt to write all code in a single context window to avoid hallucination and forgetting.

*   **unity_llm_architect:** Use this subagent to design the system, review VRAM implications, outline the exact classes and logic flow, and structure the Cognee integration.
*   **unity_mcp_developer:** Delegate the actual C# implementation and file generation to this subagent, passing it the architect's blueprint.
