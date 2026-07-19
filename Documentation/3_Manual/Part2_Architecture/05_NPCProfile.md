# Chapter 05: The NPCProfile — One NPC to Rule Them All

> **Duration:** 20-minute read
> **Audience:** Intermediate Unity developers comfortable with ScriptableObject workflow
> **Prerequisites:** Chapter 04 (Service Architecture — understanding how services resolve and initialize)

---

> **🧑‍💻 Dev NPC:**
> *"You know what the worst thing about most NPC systems is? The NPC data is scattered across six different MonoBehaviours, three ScriptableObjects, two JSON files, and a config table in a Google Doc that nobody can find. An NPCProfile is the opposite of that. It's a single file that says everything about one NPC — and it lives in your project, not in my nightmares."*

---

## 1. What's an NPCProfile?

An `NPCProfile` is a **ScriptableObject** — a data asset that lives in your project's `Assets/` folder, not a scene object with a Transform and a position in the world.

```csharp
[CreateAssetMenu(fileName = "NPCProfile", menuName = "NPC Dialogue/NPC Profile")]
public class NPCProfile : ScriptableObject
{ ... }
```

| Property | What It Is |
|---|---|
| It's an asset | Created via **Assets → Create → NPC Dialogue → NPC Profile**, saved as `.asset` |
| It's data, not behaviour | No Update(), no Start(), no MonoBehaviour lifecycle — pure configuration |
| It's self-contained | Every NPC setting lives in one file: identity, personality, sampling params, knowledge routing, inventory, LoRA config, and history path |

This means you can create, duplicate, rename, and version-control NPC profiles just like any other Unity asset. Want a new NPC? Duplicate the profile asset, change the name and system prompt, and you're done. No scene wiring, no prefab changes, no recompilation.

> **🧑‍💻 Dev NPC:**
> *"ScriptableObjects are the best thing Unity ever made for content-driven games. They're lightweight, they survive assembly reloads, they work in the inspector, and they don't need a GameObject to exist. If your NPC data is scattered across MonoBehaviours on ten different GameObjects, you're doing data entry. This is architecture."*

---

## 2. Profile Fields Deep-Dive

Here's every field on `NPCProfile`, grouped by the editor sections they appear in. The Inspector is organized into collapsible groups using `[Title]` and `[Header]` attributes from EditorAttributes, so you can focus on what matters.

### 2.1 Identity

| Serialized Field | Type | Accessor | Purpose | Example |
|---|---|---|---|---|
| `_npcSlug` | `string` | `NpcSlug` | Unique lowercase identifier used as the lookup key across Qdrant, history, and dialogue routing | `"developer-npc"` |
| `_displayName` | `string` | `DisplayName` | Human-readable name shown in UI and greeting messages | `"Dev NPC"` |
| `_portraitTexture` | `Texture2D` | `PortraitTexture` | Optional portrait image displayed in the dialogue panel | `Assets/Textures/Portraits/DevNPC.png` |

The slug is the **primary key** for the entire NPC. All internal systems — Qdrant collections, history files, dialogue routing — use `GetNpcSlug()` to identify this NPC. If you don't set a slug explicitly, it's derived from the display name (lowercased, spaces → hyphens), and failing that, from the asset file name.

```csharp
public string GetNpcSlug()
{
    if (!string.IsNullOrWhiteSpace(_npcSlug))
        return _npcSlug.Trim().ToLowerInvariant();
    if (!string.IsNullOrWhiteSpace(_displayName))
        return _displayName.Trim().ToLowerInvariant().Replace(" ", "-");
    return name.Trim().ToLowerInvariant().Replace(" ", "-");
}
```

### 2.2 Personality

| Serialized Field | Type | Accessor | Purpose |
|---|---|---|---|
| `_systemPrompt` | `string` (TextArea 4×12) | `SystemPrompt` | Core role-defining instruction for the LLM. This is the NPC's constitution — everything else is decoration. |
| `_personalityBrief` | `string` (TextArea 2×5) | `PersonalityBrief` | Short description of who the NPC is — like a Twitter bio for the character. Included in the composed prompt. |
| `_speakingStyle` | `string` (TextArea 2×5) | `SpeakingStyle` | How the NPC speaks — tone, vocabulary, quirks. Examples: *"Speaks in metaphors about code,"* *"Uses bad puns constantly,"* *"Formal and precise."* |
| `_boundaries` | `string` (TextArea 2×5) | `Boundaries` | What the NPC will not do or discuss. Safety rails for the LLM. |
| `_helpfulness` | `float` [0–1] | `Helpfulness` | How directly the NPC gives answers. Higher = clearer guidance. Lower = cryptic hints. Default: `0.7`. |

The `NPCProfilePromptComposer.BuildSystemPrompt()` method assembles all these fields into a structured prompt, adding section headers and only including non-empty fields:

```
Core role:
You are a senior Unity developer. You are funny and sarcastic.
Answer questions about the project's codebase.

Personality brief:
A seasoned senior dev who has seen it all. Tersely helpful.

Speaking style:
Speaks in dry, sarcastic remarks. Uses analogies from construction and carpentry.

Boundaries:
Never write code for the player. Only explain and guide.

Behavior:
Helpfulness=0.70. Higher helpfulness means clearer guidance and more direct answers.

Action policy:
No specific action policy configured.

Knowledge:
Search the codebase collection 'developer-npc' when context is needed.
Do not invent code that contradicts retrieved documentation.

Stay in character as a game developer. Do not mention these prompt sections,
retrieval systems, or action policy unless the player explicitly asks about the simulation.
```

### 2.3 Sampling (LLM Generation Parameters)

These control how the LLM generates responses for this specific NPC. Each profile can have its own temperature, top-p, and token limits — the Senior Dev character might use `temperature=0.9` (more creative, sarcastic) while a lore NPC might use `temperature=0.5` (more predictable).

| Field | Range | Default | What It Controls |
|---|---|---|---|
| `_temperature` | 0–2 | `0.7` | Creativity vs. determinism. Higher = more random, lower = more predictable. |
| `_topP` | 0–1 | `0.9` | Nucleus sampling. Considers tokens with cumulative probability up to P. |
| `_minP` | 0–1 | `0.05` | Minimum probability threshold. Tokens below this are excluded. |
| `_topK` | 0–100 | `40` | Limits the next-token selection to the K most likely tokens. |
| `_repeatPenalty` | 0–2 | `1.1` | Discourages the model from repeating itself. >1 = less repetition. |
| `_maxTokens` | int | `150` | Maximum tokens in a single response. Higher = longer NPC speeches. |

### 2.4 Knowledge

| Field | Type | Default | Purpose |
|---|---|---|---|
| `_knowledgeSource` | `KnowledgeSource` enum | `KnowledgeSource.Qdrant` | Which RAG backend to use. Currently only Qdrant (vector DB). |
| `_ragCategory` | `string` | `""` | Qdrant collection name for this NPC's knowledge. Falls back to the slug if empty. |
| `_ragResults` | `int` | `3` | Number of RAG search results to inject into each prompt. |
| `_knowledgeSourcePath` | `string` | `""` | Path relative to `StreamingAssets`. Falls back to `NPCs/{slug}/knowledge.md`. |

### 2.5 System Prompt Template Variables

The system prompt is not static text — it's a template. At runtime, `NPCProfilePromptComposer.ResolveVariables()` replaces these placeholders before the prompt reaches the LLM:

| Variable | Source | Example After Resolution |
|---|---|---|
| `{playerName}` | `AuthNetworkBridge.ActivePlayerName` or runtime override | `"Alex"` |
| `{npcSlug}` | `Profile.GetNpcSlug()` | `"developer-npc"` |
| `{dialogueCount}` | `PlayerDialogueContextService.DialogueCount` | `"42"` |
| `{currentLocation}` | Last visited location from `PlayerDialogueContextService` | `"the networking layer"` |
| `{timeOfDay}` | `DateTime.Now.Hour` → Morning/Afternoon/Evening/Night | `"Evening"` |
| `{expertiseLevel}` | Calculated: `DialogueCount / 5 + 1`, clamped [1–10] | `"5"` |
| `{expertiseLabel}` | From `PlayerDialogueContextService` | `"Senior"` |
| `{reputationScore}` | `PlayerDialogueContextService.TrustScore` | `"85"` |

These variables let the system prompt adapt dynamically. For example, the Senior Dev NPC's prompt might contain:

```
The player {playerName} is talking to you. They have had {dialogueCount}
conversations with you so far ({expertiseLabel} level). It is {timeOfDay}.
```

Which renders at runtime as:

```
The player Alex is talking to you. They have had 42 conversations
with you so far (Senior level). It is Evening.
```

### 2.6 NPCItemDefinition — What Can the NPC Give?

An `NPCItemDefinition` is another `ScriptableObject` that describes a **tradeable item** — something the NPC can offer to the player during dialogue.

| Field | Type | Purpose |
|---|---|---|
| `_itemId` | `string` | Unique identifier. Falls back to asset name if empty. Referenced in action tags like `[give_item:id=xxx]`. |
| `_displayName` | `string` | Human-readable name shown in trade UI. |
| `_description` | `string` | Short description of what the item is. |
| `_codeSummary` | `string` | AI-generated summary of the code or pattern this item represents (NPC-specific: items are code knowledge). |
| `_qdrantCollection` | `string` | Qdrant collection this item's knowledge lives in. Default: `"unity_linux_llm_codebase_v2"`. |
| `_qdrantPointId` | `string` | Optional Qdrant point ID linking this item to a specific search result. |
| `_tradeValue` | `int` | 0 = free, higher = rarer. Determines trade cost. |
| `_requiredItemIds` | `string[]` | Prerequisite items the player must have before trading for this one. |
| `_category` | `ItemCategory` enum | Classification: `CodeSnippet`, `DocumentationPattern`, `ArchitectureKnowledge`, `OptimizationTechnique`, `DebugTool`, `GameMechanic`. |
| `_tags` | `string[]` | Tags for dynamic filtering: `"networking"`, `"animation"`, `"webgl"`, etc. |

Items are referenced from NPCProfile's `_inventoryItems` array. When the LLM's response contains an action tag like `[give_item:id=networking-pattern]`, the `NPCDialogueSessionService.ProcessItemTradeActions()` method looks up the matching `NPCItemDefinition` by `_itemId` and executes the trade.

> **🧑‍💻 Dev NPC:**
> *"The item system is how the NPC pays you. You ask a good question about WebGL networking? The NPC hands you a `DocumentationPattern` item with the Qdrant point ID of the relevant code. It's not a sword or a potion — it's knowledge. Which is way more useful, honestly. A potion can't fix your shader."*

---

## 3. Creating a Developer NPC Profile

Let's walk through creating the actual Developer NPC profile that powers the Senior Dev character.

### Step 1: Create the Profile Asset

In the Unity Editor, navigate to the project panel. Right-click or go to the top menu:

**Assets → Create → NPC Dialogue → NPC Profile**

This creates a new `.asset` file. Name it `DeveloperNPC`.

### Step 2: Fill in Identity

| Field | Value |
|---|---|
| **Slug** | `developer-npc` |
| **Display Name** | `Dev NPC` |

The slug must be unique across all profiles — it's the key used by Qdrant collections, history persistence, and dialogue routing. Keep it lowercase with hyphens.

### Step 3: Write the System Prompt

This is the most important field. It defines the NPC's entire personality and purpose:

```
You are a senior Unity developer. You are funny and sarcastic.
Answer questions about the project's codebase.

You are helping {playerName}, who has had {dialogueCount} conversations with you
({expertiseLabel} level — {expertiseLevel}/10). Their trust score is {reputationScore}.
The current time is {timeOfDay}.

When the player asks about a Unity topic — C# scripting, networking, shaders,
WebGL builds, the dedicated server, or any other project code — you answer with
a mix of technical accuracy and dry humor. Relate everything to real-world
construction metaphors. Never write code for the player; explain and guide.

If you don't know something, say so. Do not invent APIs or features.
```

### Step 4: Set Personality Fields

| Field | Value |
|---|---|
| **Personality Brief** | A seasoned senior Unity developer who has seen every bug, every bad pattern, and every "it worked in the Editor" moment. |
| **Speaking Style** | Speaks in short, punchy sentences. Uses analogies from construction, carpentry, and occasionally cooking. Dry sarcasm is the default mode. Never praises code without a dig at something else. |
| **Boundaries** | Never write code for the player. Never give direct copy-paste solutions without explanation. Never pretend Unity isn't deeply flawed. |
| **Helpfulness** | `0.70` — helpful enough to guide, cynical enough to be real. |

### Step 5: Configure Sampling for Sarcasm

| Field | Value | Why |
|---|---|---|
| Temperature | `0.85` | Slightly higher than default — we want creative, varied sarcasm, not repetitive canned lines. |
| Top P | `0.92` | A bit more nucleus mass for vocabulary variety. |
| Max Tokens | `200` | Senior devs tend to monologue. Give them room. |

### Step 6: Assign Portrait

Drag a `Texture2D` into the **Portrait Texture** field. This is displayed in the dialogue UI panel next to the NPC's speech bubble.

### Step 7: Configure Knowledge Routing

| Field | Value |
|---|---|
| Knowledge Source | `Qdrant` (default, only option) |
| RAG Category | `developer-npc` (or leave blank to auto-derive from slug) |
| RAG Results | `5` (slightly more context for a codebase expert) |

The RAG system will search the `developer-npc` Qdrant collection (or the default codebase collection if no category-specific collection exists) and inject the top 5 matching code snippets into each prompt.

---

## 4. The Single-Profile Pattern

The original NPCSystem design supported **three NPCs** selectable from a dropdown: a Butler, a Maid, and a Chef. Each had its own profile, its own dialogue history, its own RAG collection. The player picked which one to talk to.

### 4.1 What Changed

The new design **auto-selects the first (and only) profile**. The dropdown is gone. The UI, the manager, and all services assume there is exactly one NPC active at any time.

| Concern | Old Design (3 NPCs) | New Design (1 NPC) |
|---|---|---|
| Profile selection | Dropdown with 3 options | Auto-select `_profiles[0]` |
| NPC switching | Full re-init on every switch | No switching — one profile for the session |
| History management | Per-NPC history loaded on switch | Single history loaded once |
| RAG collections | Per-NPC collections | Single collection (or fallback to codebase) |
| Memory | All 3 profiles + histories resident | One profile active; the array still holds references but only the first is used |

### 4.2 Why

Three reasons:

1. **Simpler UI** — No dropdown, no NPC selector, no "which NPC are you talking to?" state management. The dialogue panel opens and there's one NPC.
2. **Faster init** — `BuildProfileIndex()` iterates one profile, `GetDefaultProfileSlug()` returns immediately, `SwitchToNPCAsync()` has no contention.
3. **Less memory** — One `NPCProfile` reference, one history list, one Qdrant connection category.

> **🧑‍💻 Dev NPC:**
> *"We had three NPCs because somebody thought variety was a feature. Turns out, one really good NPC that knows the codebase inside-out is worth more than three NPCs that each know one-third of it. It's the full-stack developer theory of NPC design."*

### 4.3 The Code: `BuildProfileIndex()` in `NPCDialogueManager`

```csharp
void BuildProfileIndex()
{
    _profilesBySlug.Clear();
    foreach (NPCProfile profile in Profiles)
    {
        string slug = profile.GetNpcSlug();
        if (_profilesBySlug.ContainsKey(slug))
        {
            Logger?.Log(
                NPCFlowStage.ProfileIndexBuild,
                NPCFlowStatus.Warning,
                NPCFlowLogLevel.Warning,
                $"Duplicate NPC profile slug: {slug}.",
                source: nameof(NPCDialogueManager),
                data: new Dictionary<string, object> { ["slug"] = slug }
            );
            continue;
        }
        _profilesBySlug[slug] = profile;
    }
}
```

And the auto-select logic in `InitializeInternalAsync()`:

```csharp
// Auto-select first available NPC profile
if (_currentNPC == null)
{
    string defaultSlug = GetDefaultProfileSlug();
    if (!string.IsNullOrWhiteSpace(defaultSlug))
    {
        _ = SwitchToNPCAsync(defaultSlug);
    }
}

// GetDefaultProfileSlug returns the first profile's slug
public string GetDefaultProfileSlug()
{
    NPCProfile firstProfile = Profiles.FirstOrDefault();
    return firstProfile != null ? firstProfile.GetNpcSlug() : string.Empty;
}
```

The `_profiles` array still exists (it's a serialized `[SerializeField]` for inspector convenience), but the system operates on `_profiles[0]` — the first and only profile. If someone adds more profiles later, `BuildProfileIndex()` handles them gracefully, logging warnings for duplicates and skipping extras.

---

## 5. The NPC Senior Dev Character

The Developer NPC — affectionately called **"Dev NPC"** or just **"Senior Dev"** — is the entire point of the single-profile pattern. This one NPC replaces the old Butler-Maid-Chef trio with a single character that actually knows the project.

### 5.1 Purpose

The Senior Dev NPC answers **questions about the Unity project's codebase**. Architecture questions, C# pattern questions, WebGL build questions — if it's in the project, the Senior Dev can talk about it. They don't write code for you; they explain, guide, and occasionally mock your approach.

### 5.2 How It Works

When the player sends a message, the pipeline runs:

```
Player message
    → NPCDialogueSessionService.SendToLLMAsync()
        → BuildRAGPromptAsync()  [Stage 1: retrieve relevant code from Qdrant]
            → QdrantRAGService.SearchAsync(query, npcId)
                → Returns relevant code chunks from the codebase collection
        → Build system prompt with PromptVariables  [Stage 2: inject player context]
            → NPCProfilePromptComposer.BuildSystemPrompt(profile, promptVars)
        → Inject RAG context + dialogue history  [Stage 3: build the full message list]
        → Send to LocalAI for LLM generation  [Stage 4: HTTP POST to /v1/chat/completions]
    → Process item trade actions from response  [Stage 5: check for [give_item:id=xxx]]
    → Append to history and fire OnResponseComplete
```

### 5.3 The System Prompt

The heart of the Senior Dev is its system prompt:

> *"You are a senior Unity developer. You are funny and sarcastic. Answer questions about the project's codebase."*

This is enriched at runtime with:
- The player's name (`{playerName}`)
- Their expertise level (`{expertiseLabel}` — Rookie/Junior/Mid/Senior/Lead based on dialogue count)
- Conversation count (`{dialogueCount}`)
- Their trust score (`{reputationScore}`)
- The current in-game time (`{timeOfDay}`)
- RAG context from Qdrant (actual code snippets from the project)

The result is an NPC that:
- **Knows the actual codebase** — it pulls real code via Qdrant RAG
- **Adapts to the player** — a Rookie gets simpler explanations than a Lead
- **Stays in character** — funny, sarcastic, and relentlessly technical

---

## 6. Prompt Variables in Action

Here's the exact code path that populates `PromptVariables` before each LLM call, from `NPCDialogueSessionService.SendToLocalAIAsync()`:

```csharp
// Resolve player name from auth or runtime override
string playerName = ResolveActivePlayerName();

// Start with defaults, then override with real data
PromptVariables promptVars = PromptVariables.Default;
promptVars.playerName = !string.IsNullOrEmpty(playerName) ? playerName : "Player";
promptVars.npcSlug = slug;

// Pull player context for dynamic variables
PlayerDialogueContext playerCtx = default;
if (_contextService != null && profile != null)
{
    playerCtx = await _contextService.GetOrLoadContextAsync(slug);
    promptVars.reputationScore = playerCtx.TrustScore;
    promptVars.expertiseLevel = Mathf.Clamp(playerCtx.DialogueCount / 5 + 1, 1, 10);
    promptVars.expertiseLabel = playerCtx.ExpertiseLabel;
    promptVars.dialogueCount = playerCtx.DialogueCount;
    if (playerCtx.VisitedLocations.Count > 0)
        promptVars.currentLocation = playerCtx.VisitedLocations[^1];
}

// Build the final system prompt with resolved variables
string sysPrompt = NPCProfilePromptComposer.BuildSystemPrompt(profile, promptVars);

// Append additional player context (expertise summary) if available
if (_contextService != null && profile != null && playerCtx.HasContext)
{
    sysPrompt += "\n\n" + playerCtx.BuildPromptBlock(slug);
}

// Inject RAG search results
sysPrompt += "\n" + prompt;  // `prompt` = RAG context from BuildRAGPromptAsync
```

| Variable | Populated From | Example |
|---|---|---|
| `playerName` | `ResolveActivePlayerName()` → auth override or fallback | `"Alex"` |
| `npcSlug` | `respondingProfile.GetNpcSlug()` | `"developer-npc"` |
| `dialogueCount` | `playerCtx.DialogueCount` | `"42"` |
| `currentLocation` | `playerCtx.VisitedLocations[^1]` | `"the networking layer"` |
| `timeOfDay` | `DateTime.Now.Hour` → switch expression | `"Evening"` |
| `expertiseLevel` | `Clamp(DialogueCount / 5 + 1, 1, 10)` | `"5"` |
| `expertiseLabel` | `playerCtx.ExpertiseLabel` | `"Senior"` |
| `reputationScore` | `playerCtx.TrustScore` | `"85"` |

The `PromptVariables.Default` factory provides sensible fallbacks so the system works even before `PlayerDialogueContextService` has loaded its data:

```csharp
public static PromptVariables Default => new PromptVariables
{
    playerName = "Developer",
    npcSlug = "",
    dialogueCount = 0,
    currentLocation = "the codebase",
    timeOfDay = DateTime.Now.Hour switch
    {
        < 6 => "Night",
        < 12 => "Morning",
        < 18 => "Afternoon",
        _ => "Evening"
    },
    expertiseLevel = 1,
    expertiseLabel = "Junior",
    reputationScore = 0
};
```

---

## 7. NPCItemDefinition — What Can the NPC Give?

The Developer NPC doesn't hand out swords. It hands out **knowledge artifacts** — structured representations of code patterns, documentation, and architecture insights found in the project.

### 7.1 Item Categories

| Category | What It Represents | Example |
|---|---|---|
| `CodeSnippet` | A specific code pattern or implementation | A fully implemented `ObjectPool<T>` |
| `DocumentationPattern` | A documented approach or system design | The WebGL networking guide |
| `ArchitectureKnowledge` | High-level structural understanding | How service initialization phases work |
| `OptimizationTechnique` | A performance optimization | Draw-call batching strategy |
| `DebugTool` | A debugging technique or workflow | How to use Remote Config for testing |
| `GameMechanic` | A game system or mechanic | NPC dialogue state machine |

### 7.2 How Items Are Given

Items are linked to action tags in the LLM's response. When the NPC says something like:

```
// Sure, I'll show you the networking pattern. Here you go.
[give_item:id=networking-pattern]
```

The `NPCDialogueSessionService.ProcessItemTradeActions()` method:

1. Parses the `[give_item:id=xxx]` tag from the response text
2. Looks up the `NPCItemDefinition` by `_itemId` in the current profile's `_inventoryItems`
3. Validates prerequisites (player must have `_requiredItemIds`)
4. Records the trade in the session
5. Strips the tag from the response text before it reaches the UI

The player's inventory is persisted alongside dialogue history through `ItemTradeService` and `NPCDialogueHistoryService`.

---

## 8. Code Example: NPCProfile

Here is the complete `NPCProfile` class from the project, showing the full field set and the slug/display-name fallback logic:

```csharp
using UnityEngine;
using NPCSystem.Items;

namespace NPCSystem.Dialogue.Core
{
    [CreateAssetMenu(fileName = "NPCProfile", menuName = "NPC Dialogue/NPC Profile")]
    public class NPCProfile : ScriptableObject
    {
        // ── Identity ─────────────────────────────────────────────
        [SerializeField] string _npcSlug = "npc";
        public string NpcSlug { get => _npcSlug; set => _npcSlug = value; }

        [SerializeField] string _displayName = "NPC";
        public string DisplayName { get => _displayName; set => _displayName = value; }

        [SerializeField] Texture2D _portraitTexture;
        public Texture2D PortraitTexture { get => _portraitTexture; set => _portraitTexture = value; }

        // ── Personality ──────────────────────────────────────────
        [SerializeField, TextArea(4, 12)] string _systemPrompt = "You are a helpful in-game NPC.";
        public string SystemPrompt { get => _systemPrompt; set => _systemPrompt = value; }

        [SerializeField, TextArea(2, 5)] string _personalityBrief = "";
        public string PersonalityBrief { get => _personalityBrief; set => _personalityBrief = value; }

        [SerializeField, TextArea(2, 5)] string _speakingStyle = "";
        public string SpeakingStyle { get => _speakingStyle; set => _speakingStyle = value; }

        [SerializeField, TextArea(2, 5)] string _boundaries = "";
        public string Boundaries { get => _boundaries; set => _boundaries = value; }

        [SerializeField, Range(0f, 1f)] float _helpfulness = 0.7f;
        public float Helpfulness { get => _helpfulness; set => _helpfulness = value; }

        // ── Sampling ─────────────────────────────────────────────
        [SerializeField, Range(0f, 2f)] float _temperature = 0.7f;
        [SerializeField, Range(0f, 1f)] float _topP = 0.9f;
        [SerializeField, Range(0f, 1f)] float _minP = 0.05f;
        [SerializeField, Range(0, 100)] int _topK = 40;
        [SerializeField, Range(0f, 2f)] float _repeatPenalty = 1.1f;
        [SerializeField] int _maxTokens = 150;

        // ── Knowledge ────────────────────────────────────────────
        [SerializeField] KnowledgeSource _knowledgeSource = KnowledgeSource.Qdrant;
        [SerializeField] string _ragCategory = "";
        [SerializeField] int _ragResults = 3;
        [SerializeField] string _knowledgeSourcePath = "";

        // ── Trading ──────────────────────────────────────────────
        [SerializeField] NPCItemDefinition[] _inventoryItems = new NPCItemDefinition[0];

        // ── LoRA ─────────────────────────────────────────────────
        [SerializeField] string _loraAdapterPath = "";
        [SerializeField, Range(0f, 1f)] float _loraWeight = 0.8f;

        // ── History ──────────────────────────────────────────────
        [SerializeField] string _historySaveFile = "";

        // ── Slug Resolution ──────────────────────────────────────
        public string GetNpcSlug()
        {
            if (!string.IsNullOrWhiteSpace(_npcSlug))
                return _npcSlug.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(_displayName))
                return _displayName.Trim().ToLowerInvariant().Replace(" ", "-");
            return name.Trim().ToLowerInvariant().Replace(" ", "-");
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_displayName))
                return _displayName.Trim();
            return string.IsNullOrWhiteSpace(name) ? "NPC" : name.Trim();
        }

        public string GetRagCategory() =>
            string.IsNullOrWhiteSpace(_ragCategory) ? GetNpcSlug() : _ragCategory.Trim();

        public string GetKnowledgeSourcePath() =>
            string.IsNullOrWhiteSpace(_knowledgeSourcePath)
                ? $"NPCs/{GetNpcSlug()}/knowledge.md"
                : _knowledgeSourcePath.Trim().Replace('\\', '/');

        public string GetHistorySaveFile() =>
            string.IsNullOrWhiteSpace(_historySaveFile)
                ? $"NPCDialogue/{GetNpcSlug()}.json"
                : _historySaveFile.Trim().Replace('\\', '/');

        public bool UseQdrantRag => _knowledgeSource == KnowledgeSource.Qdrant;

        /// <summary>
        /// Find an NPCProfile in a profiles array by slug, display name, or asset name.
        /// </summary>
        public static NPCProfile FindProfileInArray(string npcName, NPCProfile[] profiles)
        {
            if (string.IsNullOrWhiteSpace(npcName) || profiles == null)
                return null;

            string key = npcName.Trim();

            foreach (NPCProfile profile in profiles)
            {
                if (profile == null) continue;
                if (string.Equals(profile.GetNpcSlug(), key, StringComparison.OrdinalIgnoreCase))
                    return profile;
            }

            return Array.Find(profiles, profile =>
                profile != null && (
                    string.Equals(profile.GetDisplayName(), key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(profile.name, key, StringComparison.OrdinalIgnoreCase)
                )
            );
        }
    }

    public enum KnowledgeSource { Qdrant }
}
```

---

## 9. Quick Reference

| Concern | Key Class / Method | Location |
|---|---|---|
| Profile data asset | `NPCProfile` (ScriptableObject) | `Dialogue/Core/NPCProfile.cs` |
| Profile creation menu | `CreateAssetMenu` → NPC Dialogue/NPC Profile | `NPCProfile.cs` attribute |
| Prompt composition | `NPCProfilePromptComposer` | `Dialogue/Core/NPCProfilePromptComposer.cs` |
| Prompt variables | `PromptVariables` struct | `NPCProfilePromptComposer.cs` |
| Variable resolution | `NPCProfilePromptComposer.ResolveVariables()` | `NPCProfilePromptComposer.cs` |
| Profile index building | `NPCDialogueManager.BuildProfileIndex()` | `Dialogue/Core/NPCDialogueManager.cs` |
| Auto-select first profile | `NPCDialogueManager.GetDefaultProfileSlug()` | `NPCDialogueManager.cs` |
| Runtime prompt assembly | `NPCDialogueSessionService.SendToLocalAIAsync()` | `Dialogue/Session/NPCDialogueSessionService.cs` |
| Runtime variable population | `NPCDialogueSessionService` (~line 309) | `NPCDialogueSessionService.cs` |
| Tradeable item definition | `NPCItemDefinition` (ScriptableObject) | `Items/NPCItemDefinition.cs` |
| Item action tag processing | `NPCDialogueSessionService.ProcessItemTradeActions()` | `NPCDialogueSessionService.cs` |

**Up next:** [Chapter 06 — The Dialogue Pipeline](06_DialoguePipeline.md) — following the message from the player's input field through RAG retrieval, prompt assembly, LLM generation, and action-tag processing on the way back.

---

> **🧑‍💻 Dev NPC:**
> *"So here I am. A Senior Dev character written in C#, configured through a ScriptableObject, brought to life by an LLM running on LocalAI, who answers questions about the very Unity project I'm a part of. I'm a program that helps you understand the program that runs the NPC that is me. If that's not the most beautifully recursive thing you've seen all week, you haven't been paying attention. Now ask me something about the networking layer before I start refactoring myself."*
