# Developer Guide

Professional guidelines for extending and modifying the NPC Dialogue system.

## Table of Contents

- [Code Organization & Standards](#code-organization--standards)
- [Scene Initialization Workflow](SceneInitializationWorkflow.md)
- [Adding New NPC Profiles](#adding-new-npc-profiles)
- [Extending Dialogue Logic](#extending-dialogue-logic)
- [Custom Action Handlers](#custom-action-handlers)
- [Debugging Guide](#debugging-guide)
- [Multiplayer Workflow](MultiplayerWorkflow.md)
- [Known Issues & Workarounds](#known-issues--workarounds)
- [Testing Patterns](#testing-patterns)

---

## Code Organization & Standards

> **Canonical rules live in [`AGENTS.md` §1](../../AGENTS.md#1-code-conventions).** That document is this project's
> single source of truth for naming, formatting, and anti-pattern rules, and is verified automatically by
> `Tools/NPCDialogueCodeReview`. Everything below is *supplementary* illustration (async patterns, nullable
> reference types, event-vs-delegate preference, perf habits) — if anything here ever looks like it contradicts
> `AGENTS.md` §1, `AGENTS.md` wins; treat the mismatch as a bug in this file, not in `AGENTS.md`.

### Namespace Discipline

All runtime code must use the `NPCSystem` namespace:

```csharp
// ✓ Correct
namespace NPCSystem
{
    public class MyDialogueExtension
    {
        // ...
    }
}

// ✗ Incorrect (don't do this)
namespace MyCustomSystem
{
    public class DialogueLogic
    {
        // ...
    }
}
```

### Assembly References

Respect assembly boundaries:

```
NPCSystem.Runtime (auto-referenced)
├─ Can reference: LLMUnity, GladeKit, Unity.Netcode, U-Search
├─ Cannot reference: NPCSystem.Editor (except via #if UNITY_EDITOR)
└─ Runtime code goes here

NPCSystem.Editor (manual reference, Editor-only)
├─ Can reference: NPCSystem.Runtime, GladeKit.Bridge
└─ Editor tools go here

NPCSystem.Tests (test-only)
├─ Can reference: NPCSystem.Runtime, NUnit
└─ Tests go here
```

### C# Coding Standards

**Style**
```csharp
// 1. Use async/await, not callbacks
public async Task<string> GetResponseAsync()
{
    return await llm.GenerateAsync(prompt);
}

// 2. Use expression-bodied members for simple properties
public string Name => displayName ?? "Unknown";

// 3. Use nullable reference types
public string? OptionalValue { get; set; }

// 4. Use UnityEvent instead of custom delegates for public events
public UnityEvent<string> OnResponseComplete = new();

// 5. Use readonly for immutable fields, and the project's _camelCase private-field
//    convention (AGENTS.md §1.1)
private readonly Dictionary<string, NPCProfile> _profiles = new();
```

**Logging**
```csharp
// ✓ Use NPCFlowLogger, not Debug.Log
private static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

Logger.Log(new NPCFlowEvent
{
    Stage = NPCFlowStage.DialogueInput,
    Message = "Processing user input"
});

// ✗ Don't use Debug.Log directly
Debug.Log("This is wrong");  // Will be ignored in distributed logging
```

**Performance**
```csharp
// ✓ Use async for LLM calls
var response = await llm.GenerateAsync(prompt);

// ✓ Cache repeated lookups (again, _camelCase per AGENTS.md §1.1)
private readonly Dictionary<string, NPCProfile> _profileCache = new();

// ✗ Don't block the main thread
var response = llm.Generate(prompt);  // Blocks!

// ✗ Don't create objects every frame
void OnGUI()
{
    // Bad: creates new list every frame
    var items = new List<string>();
    
    // Good: reuse or pool objects
    items.Clear();
}
```

---

## Adding New NPC Profiles

### Step 1: Create Profile Asset

```csharp
// 1. Create new ScriptableObject instance
//    In Unity Editor: Right-click in Assets/Resources/NPC/
//    Create → NPCProfile

// 2. Configure the profile (in Inspector)
Profile Fields:
├─ Slug: "brave-knight"                    [Unique ID]
├─ Display Name: "Sir Galahad"             [Human readable]
├─ System Prompt: [Paste persona text]     [See template below]
├─ Knowledge Documents: [Drag & drop]      [TextAsset array]
├─ Portrait Image: [Select image]          [Optional]
├─ Enable Memory Encoding: [Toggle]        [For long-term memory]
└─ Action Types: ["speak", "draw_sword"]   [Custom actions]
```

### Step 2: Write System Prompt

Template for system prompts:

```
# System Prompt Template

You are {Name}, a {Class} in {World}.

## Identity
- Name: {Name}
- Role: {Role}
- Personality: {Description}
- Location: {Where are they from}

## Knowledge Areas
- Topic 1: {Details}
- Topic 2: {Details}

## Speech Patterns
- Never say: {Forbidden phrases}
- Always say: {Signature expressions}
- Speaking style: {Formal/Casual/Poetic}

## Constraints
- You have these facts: {List them}
- You don't know: {Boundaries}
- You cannot: {Actions you can't do}

## Response Format
- Keep responses under 300 words
- Use markdown for emphasis
- End responses with a question

Example:
---

You are Sir Galahad, a noble knight sworn to protect the kingdom.

## Identity
- Name: Sir Galahad
- Role: Knight of the Round Table
- Personality: Honorable, brave, sometimes overly trusting
- Location: Originally from the western lands

## Knowledge Areas
- Medieval combat: Expert in sword fighting and tactics
- Kingdom politics: Knows court gossip and alliances
- Magic: Skeptical but aware it exists

## Speech Patterns
- Never say: Anything disloyal to the kingdom
- Always say: "By my honor" when making commitments
- Speaking style: Formal, medieval English influenced

## Constraints
- You believe the king is always right
- You don't know about secret plots (unless told)
- You cannot betray your oath

---
```

### Step 3: Add Knowledge Documents

Create TextAsset files with knowledge:

```
Assets/Resources/NPC/KnowledgeBases/
├─ BraveKnight_History.txt
├─ BraveKnight_Skills.txt
├─ BraveKnight_Secrets.txt
└─ BraveKnight_Quotes.txt
```

Example knowledge file:

```
# History of Sir Galahad

Sir Galahad was born in the western lands to Lord Merrick.
At age 7, he was squired to Sir Lancelot, the greatest knight.
After 10 years of training, he was knighted at the grand ceremony.
He has served the kingdom for 15 years with honor.
He won the Tournament of Champions in 1189.
He was nearly killed by dark magic but was healed by a holy relic.

# Key Events

- 1189: Won Tournament of Champions
- 1190: Fell in love with Lady Catherine (unrequited)
- 1191: Discovered the traitor in the council
- 1192: Saved the king from assassins
- 1193: Granted his own lands
- 1194: Began secret quest for the Grail

# Current Status

Sir Galahad is troubled by recent events.
He suspects foul play in the kingdom but has no proof.
He trusts very few people, mostly other knights.
He is considering leaving the kingdom.
```

### Step 4: Register in Dialogue Manager

```csharp
// In NPCDialogueManager scene configuration:
// 1. Drag your profile asset to the Profiles array
// 2. Or add programmatically:

var profile = Resources.Load<NPCProfile>("NPC/BraveKnight");
npCDialogueManager.profiles = new[] { profile };

// Or add to existing profiles:
var newProfiles = new NPCProfile[npCDialogueManager.profiles.Length + 1];
System.Array.Copy(npCDialogueManager.profiles, newProfiles, npCDialogueManager.profiles.Length);
newProfiles[newProfiles.Length - 1] = profile;
npCDialogueManager.profiles = newProfiles;
```

### Step 5: Test

```csharp
// Test dialogue with your new NPC
var npc = Resources.Load<NPCProfile>("NPC/BraveKnight");
var response = await npCDialogueManager.GetResponseAsync(
    "Hello! Tell me about yourself.",
    npc
);
Debug.Log($"Response: {response}");
```

---

## Extending Dialogue Logic

### Scenario 1: Custom Response Validation

Override `NPCDialogueActionPlanner` to validate responses:

```csharp
public class CustomActionPlanner : NPCDialogueActionPlanner
{
    // Override validation logic
    public override bool ValidateResponse(
        string response,
        NPCProfile profile,
        string userInput)
    {
        // Add custom rules
        if (response.Length < 10)
        {
            Debug.LogWarning("Response too short!");
            return false;
        }
        
        if (response.Contains("undefined"))
        {
            Debug.LogWarning("Model generated garbage!");
            return false;
        }
        
        return base.ValidateResponse(response, profile, userInput);
    }
}
```

Register in scene:

```csharp
// In NPCDialogueManager:
public CustomActionPlanner actionPlanner;  // Assign in Inspector

// Or programmatically:
npCDialogueManager.actionPlanner = GetComponent<CustomActionPlanner>();
```

### Scenario 2: Custom Prompt Composition

Extend prompt building:

```csharp
public class CustomPromptComposer
{
    public static string ComposePromptWithCustomData(
        NPCProfile profile,
        List<string> ragContext,
        List<DialogueEntry> dialogueHistory,
        NPCEvidenceState gameState,
        string userInput,
        // Custom data:
        Dictionary<string, object> customContext)
    {
        // Build using existing method
        var basePrompt = NPCProfilePromptComposer.ComposePrompt(
            profile, ragContext, dialogueHistory, gameState, userInput
        );
        
        // Add custom context
        var customSection = string.Empty;
        foreach (var (key, value) in customContext)
        {
            customSection += $"\n{key}: {value}";
        }
        
        return basePrompt + "\n\nAdditional Context:" + customSection;
    }
}
```

### Scenario 3: Custom Action Execution

Define custom NPC actions:

```csharp
public interface IDialogueAction
{
    string ActionType { get; }
    Task ExecuteAsync(NPCProfile npc);
}

public class EmoteAction : IDialogueAction
{
    public string ActionType => "emote";
    public string emoteName;
    
    public async Task ExecuteAsync(NPCProfile npc)
    {
        // Find animator for this NPC
        var animator = GetNPCAnimator(npc);
        animator.SetTrigger(emoteName);
        await Task.Delay(2000);  // Wait for animation
    }
}

// Register action parser
public class ActionParser
{
    public static List<IDialogueAction> ParseActionsFromResponse(
        string response,
        NPCProfile profile)
    {
        var actions = new List<IDialogueAction>();
        
        // Parse [action:name] syntax
        var regex = new System.Text.RegularExpressions.Regex(@"\[action:(\w+)\]");
        foreach (var match in regex.Matches(response))
        {
            var actionType = match.Groups[1].Value;
            actions.Add(CreateAction(actionType, profile));
        }
        
        return actions;
    }
}
```

---

## Custom Action Handlers

Define what NPCs can do in response:

```csharp
public class NPCDialogueActionHandler : MonoBehaviour
{
    private readonly Dictionary<string, Func<string, Task>> handlers = new();
    
    public void RegisterActionHandler(
        string actionType,
        Func<string, Task> handler)
    {
        handlers[actionType] = handler;
    }
    
    public async Task HandleActionAsync(string actionType, string parameter)
    {
        if (handlers.TryGetValue(actionType, out var handler))
        {
            await handler(parameter);
        }
    }
}

// Usage in your dialogue manager:
var handler = GetComponent<NPCDialogueActionHandler>();

handler.RegisterActionHandler("speak", async (text) =>
{
    // Play dialogue audio
    await PlayDialogueAsync(text);
});

handler.RegisterActionHandler("walk", async (location) =>
{
    // Move NPC to location
    await MoveNPCAsync(location);
});

handler.RegisterActionHandler("cast_spell", async (spellName) =>
{
    // Execute spell effect
    await ExecuteSpellAsync(spellName);
});
```

---

## Debugging Guide

### Enable Detailed Logging

```csharp
// Get logger and enable all events
var logger = NPCFlowLogger.FindOrCreate();

// Log every dialogue event
logger.minLogLevel = NPCFlowLogLevel.Debug;  // Verbose
logger.enableConsoleOutput = true;           // Print to console too
logger.enableFileOutput = true;               // Save to JSONL file
logger.logFilePath = "Logs/dialogue_flow.jsonl";
```

### Inspect Runtime State

Using GladeKit MCP (editor inspection):

```
1. Open Unity Editor
2. With project running in Play Mode
3. Use GladeKit MCP commands:
   - mcp_gladekit_mcp_get_gameobject_info
     (Inspect NPCDialogueManager state)
   - mcp_gladekit_mcp_console_logs
     (Get all logged dialogue events)
   - mcp_gladekit_mcp_get_scene_hierarchy
     (See all NPCs in scene)
```

### Common Debugging Scenarios

**Scenario 1: LLM Not Generating Response**

```csharp
// Check 1: Is LLM initialized?
if (!llm.ready)
{
    Debug.LogError("LLM not loaded!");
    return;
}

// Check 2: Is model file present?
if (!System.IO.File.Exists(llm.modelPath))
{
    Debug.LogError($"Model not found: {llm.modelPath}");
    return;
}

// Check 3: Monitor inference
var sw = System.Diagnostics.Stopwatch.StartNew();
var response = await llm.GenerateAsync(prompt);
sw.Stop();

Debug.Log($"Generation took {sw.ElapsedMilliseconds}ms");
Debug.Log($"Tokens per second: {response.Length / (sw.Elapsed.TotalSeconds * 4)}");  // Approx 4 chars per token
```

**Scenario 2: RAG Returns No Results**

```csharp
// Check 1: Is Qdrant running?
var qdrant = GetComponent<QdrantRAGService>();
if (!await qdrant.IsReadyAsync())
{
    Debug.LogError("Qdrant not available!");
    return;
}

// Check 2: Are documents indexed?
var results = await qdrant.RetrieveAsync("test query", topK: 10);
if (results.Count == 0)
{
    Debug.LogWarning("No documents indexed!");
    // Re-index NPC knowledge
    await ImportNPCKnowledge(npc);
}

// Check 3: Test embedding
var queryVector = await embedding.EmbedAsync(query);
Debug.Log($"Query vector dimensions: {queryVector.Length}");  // Should be 384
```

**Scenario 3: Networking Issues**

```csharp
// Check network config
var config = GetComponent<NPCTransportConfig>();
config.NormalizeInPlace();

if (!config.TryValidate(out var error))
{
    Debug.LogError($"Invalid config: {error}");
    return;
}

// Test connection
try
{
    var client = new WebSocketClient(
        $"ws://{config.connectAddress}:{config.port}{config.webSocketPath}"
    );
    await client.ConnectAsync(System.TimeSpan.FromSeconds(5));
    Debug.Log("Network connection OK");
}
catch (System.Exception ex)
{
    Debug.LogError($"Network error: {ex.Message}");
}
```

### Log Analysis

```bash
# Monitor dialogue logs in real-time
tail -f Logs/dialogue_flow.jsonl | jq '.stage, .message'

# Find all errors
jq 'select(.status == "Error")' Logs/dialogue_flow.jsonl

# Get timing stats
jq '.elapsedMs' Logs/dialogue_flow.jsonl | \
    awk '{sum += $1; count++} END {print "Avg:", sum/count, "ms"}'

# Specific NPC logs
jq "select(.metadata.npc_slug == \"wise-mage\")" Logs/dialogue_flow.jsonl
```

---

## Known Issues & Workarounds

### Issue 1: CS0618 - Obsolete FindFirstObjectByType

**Problem**
```
warning CS0618: 'FindFirstObjectByType' is obsolete
```

**Why**: Old Unity API is deprecated

**Workaround** (current): Suppress or update
```csharp
#pragma warning disable CS0618
var obj = FindFirstObjectByType<NPCDialogueManager>();
#pragma warning restore CS0618
```

**Better**: Use modern API
```csharp
// Instead of:
FindFirstObjectByType<NPCDialogueManager>()

// Use:
FindAnyObjectByType<NPCDialogueManager>()  // Non-deterministic
// Or:
FindObjectsOfType<NPCDialogueManager>()[0]  // Guaranteed first
// Or:
GetComponent<NPCDialogueManager>()  // If on same GameObject
```

### Issue 2: CS0108 - SendMessage Name Hiding

**Problem**
```
warning CS0108: 'NPCDialogueManager.SendMessage(string)' 
  hides inherited member 'Component.SendMessage(string)'
```

**Why**: Method name collides with Unity's SendMessage

**Workaround** (temporary)
```csharp
// Mark to suppress warning
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CS0108")]
public class NPCDialogueManager { }

// Or rename your method:
public void OnMessageReceived(string message)  // Instead of SendMessage
```

### Issue 3: Slow Response Generation

**Problem**: Taking 30+ seconds for a short response

**Causes**:
1. GPU not being used (CPU inference only)
2. Model too large for available VRAM
3. Context size too large
4. Network latency (if using remote server)

**Workarounds**:
```csharp
// Check GPU usage
if (llm.GetGPULayersUsed() == 0)
{
    Debug.LogWarning("GPU layers not offloaded!");
    // Increase gpu_layers in LLM settings
}

// Reduce context for faster inference
llm.contextSize = 512;  // Instead of 2048

// Use faster quantization
// Q4_K_M (recommended)
// vs Q6_K (slower, more accurate)

// For remote: use batch requests instead of streaming
```

### Issue 4: Model Fails to Load

**Problem**: "Failed to load model at path"

**Causes**:
1. File doesn't exist
2. File is corrupted
3. Insufficient disk space for loading
4. File locked by another process
5. Invalid GGUF format

**Workarounds**:
```bash
# Verify file exists and is readable
file /path/to/model.gguf
# Should output: "data" or GGUF-specific info

# Check file size
du -h /path/to/model.gguf

# Verify GGUF format (hex dump first bytes)
xxd -l 16 /path/to/model.gguf
# Should show "GGUF" magic number

# Check disk space
df -h $(dirname /path/to/model.gguf)
```

---

## Testing Patterns

### Unit Testing Dialogue Logic

```csharp
[Test]
public async Task GetResponse_WithValidProfile_ReturnsNonEmptyString()
{
    // Arrange
    var profile = CreateTestProfile("test-npc");
    var input = "Hello";
    
    // Act
    var response = await npCDialogueManager.GetResponseAsync(input, profile);
    
    // Assert
    Assert.IsNotEmpty(response);
    Assert.Greater(response.Length, 5);
}

[Test]
public async Task ValidateResponse_WithShortResponse_ReturnsFalse()
{
    // Arrange
    var planner = new NPCDialogueActionPlanner();
    var shortResponse = "Hi";
    
    // Act
    var valid = planner.ValidateResponse(shortResponse, null, "");
    
    // Assert
    Assert.IsFalse(valid);
}
```

### Integration Testing

```csharp
[Test]
public async Task FullDialogueFlow_WithValidInputs_SucceedsEndToEnd()
{
    // Arrange
    await npCDialogueManager.InitializeAsync();
    var npc = npCDialogueManager.Profiles[0];
    
    // Act
    var response = await npCDialogueManager.GetResponseAsync(
        "Tell me a joke",
        npc
    );
    
    // Assert
    Assert.IsNotEmpty(response);
    Assert.That(npCDialogueManager.isResponding, Is.False);  // Should finish
}
```

---

**Next**: See [Known Issues](KnownIssues.md) for more documented problems and solutions.

**Questions?** Check [Integration Guides](../4_Integration_Guides/README.md) if your question is about external systems.
