# Chapter 06: The Dialogue Pipeline — From Player Input to Game State Change

> **Duration:** 25-minute read
> **Audience:** Intermediate Unity developers comfortable with async/await, UnityEvents, and HTTP-based LLM integration
> **Prerequisites:** Chapters 04–05 (Service Architecture, NPCProfile system)

---

> **🧑‍💻 Dev NPC:**
> *"You type some words, the NPC says some words back, and sometimes an item appears in your inventory. Magic? No. A carefully orchestrated pipeline of async method calls, JSON serialization, and regex parsing — which is basically magic if you think about it."*

---

## 1. The Full Pipeline (Overview)

Every dialogue turn — from the moment the player presses Enter to the moment the game state changes — flows through a six-stage pipeline:

```
Player Input
    ↓
UIController (NPCDialogueUIController)
    ↓
DialogueManager (NPCDialogueManager)
    ↓
LLM + RAG (NPCLocalAIClient → LocalAI + Qdrant)
    ↓
Response Parsing (NPCDialogueSessionService.ProcessItemTradeActions)
    ↓
Game State Changes (ItemTradeService, Trust/Mood, Supabase persistence)
    ↓
UI Update (NPCDialogueUIController.SetAIText)
```

Each stage has a clear owner, a defined responsibility, and a well-tested failure mode. Let's walk through every one.

---

## 2. Step 1: Player Input

### 2.1 The TMP_InputField

The player types their message into a standard TextMeshPro `TMP_InputField` component wired to `NPCDialogueUIController.PlayerInput`. The binding happens in `Start()`:

```csharp
// NPCDialogueUIController.cs
void BindUiListeners()
{
    if (PlayerInput != null)
        PlayerInput.onSubmit.AddListener(OnInputFieldSubmit);
    if (StopButton != null)
        StopButton.onClick.AddListener(OnStopPressed);
}
```

The `onSubmit` event fires when the player presses Enter (or the equivalent on their platform).

### 2.2 Validation

`OnInputFieldSubmit` runs two quick checks before anything happens:

```csharp
void OnInputFieldSubmit(string text)
{
    if (!_readyForInput)        // ← Guard 1: Is dialogue initialized and ready?
        return;

    string message = (text ?? string.Empty).Trim();
    if (message.Length == 0)    // ← Guard 2: Did they actually type something?
        return;

    // ... routing ...
}
```

| Guard | Checks | Why |
|---|---|---|
| `_readyForInput` | Has initialization completed? Is an NPC selected? | Prevents sending messages before dialogue services are ready |
| `Length == 0` | Is the input empty or whitespace? | Avoids wasteful empty LLM requests |

If either guard fails, the input is silently dropped — no error, no LLM call, nothing. The UI stays responsive and the player can try again.

### 2.3 Routing: Single-Player vs. Multiplayer

After validation, the message is routed based on whether a `NetworkBridge` is present and connected:

```csharp
if (ShouldUseNetworkBridge())
    NetworkBridge.SubmitPlayerMessage(message);
else
    DialogueManager.SendDialogueMessage(message);
```

| Route | When | What Happens |
|---|---|---|
| **Direct** | No bridge, or bridge not spawned | `DialogueManager.SendDialogueMessage()` — single-player / server-authoritative |
| **NetworkBridge** | Bridge exists, NetworkManager is active | `NetworkBridge.SubmitPlayerMessage()` — client sends RPC to server, server processes, server broadcasts response |

`ShouldUseNetworkBridge()` checks:
- `NetworkBridge != null`
- `NetworkBridge.NetworkObject.IsSpawned`
- `NetworkManager.Singleton` is listening
- Local peer is a client or server (not host-only)

> **🧑‍💻 Dev NPC:**
> *"The bridge check is like checking whether you're in a multiplayer lobby or a solo game before shouting your order. Shouting at a waiter who doesn't exist just makes you look silly. Shouting through a bridge that isn't spawned makes your RPC disappear into the void — no errors, no response, just a very confused NPC."*

---

## 3. Step 2: DialogueManager Orchestration

`NPCDialogueManager.SendDialogueMessage()` is the entry point on the processing side. It does three things before handing off to the session service:

### 3.1 Guard: NPC Selected

```csharp
public void SendDialogueMessage(string playerMessage)
{
    if (_currentNPC == null)
    {
        Logger?.Log(NPCFlowStage.RequestStart, NPCFlowStatus.Skipped,
            NPCFlowLogLevel.Warning, "No NPC selected! Call SwitchToNPC() first.");
        OnError?.Invoke("No NPC selected");
        return;
    }
    // ...
}
```

If no NPC profile is active (e.g., initialization hasn't completed, or `SwitchToNPC()` was never called), the message is rejected with a logged warning and a UnityEvent error.

### 3.2 Resolve Player Context

```csharp
string activePlayerName = AuthNetworkBridge.ActivePlayerName;
if (!string.IsNullOrWhiteSpace(activePlayerName) && activePlayerName != "Player")
{
    _sessionService?.SetRuntimePlayerContext(activePlayerName);
}
```

The player's authenticated name is resolved from the auth system and pushed into the session service as a runtime override. This ensures the LLM prompt includes the correct player name for personalization.

### 3.3 Delegate to SessionService

```csharp
_sessionService?.SendDialogueMessage(playerMessage, _currentNPC);
```

The manager passes the baton. All heavy lifting — history gathering, RAG, LLM request, response parsing — happens in `NPCDialogueSessionService`.

### 3.4 Behind the Scenes: BuildProfileIndex()

Before the first message can be sent, `InitializeInternalAsync()` calls `BuildProfileIndex()`:

```csharp
void BuildProfileIndex()
{
    _profilesBySlug.Clear();
    foreach (NPCProfile profile in Profiles)
    {
        string slug = profile.GetNpcSlug();
        if (_profilesBySlug.ContainsKey(slug))
        {
            // Log duplicate slug warning and skip
            continue;
        }
        _profilesBySlug[slug] = profile;
    }
}
```

This builds a fast `Dictionary<string, NPCProfile>` lookup keyed by slug so that `FindProfile()` and `SwitchToNPCAsync()` run in O(1) instead of scanning the array.

---

## 4. Step 3: LLM Request (LocalAI)

This is the core of the pipeline. `NPCDialogueSessionService.SendToLLMAsync()` orchestrates the full LLM round-trip.

### 4.1 Prompt Building

The session service builds an enriched prompt in three layers:

**Layer 1 — System Prompt:**
```
NPCProfilePromptComposer.BuildSystemPrompt(profile, promptVars)
```

This generates the NPC's character definition — name, personality, backstory, speaking style — from the `NPCProfile` ScriptableObject. The `promptVars` struct injects runtime context:
- `playerName` — authenticated player name
- `reputationScore` — trust score from `PlayerDialogueContextService`
- `expertiseLabel` — player expertise tier (Rookie → Lead)
- `dialogueCount` — how many conversations this player has had
- `currentLocation` — last visited location

**Layer 2 — RAG Context:**
```csharp
string ragKnowledge = await _retrievalService.SearchAsync(profile, playerMessage, reqId);
```

If RAG is enabled (`_useQdrantRag = true`), `NPCDialogueRetrievalService.SearchAsync()` queries Qdrant for relevant knowledge chunks for this NPC, scoped by the NPC's `ragCategory`. The retrieved text is prepended to the prompt as "Relevant knowledge for [NPC name]:...".

If no RAG context is found and the question is flagged as technical (via `IsTechnicalCodebaseQuestion()`), a fallback prompt asks the LLM to be honest about uncertainty rather than hallucinating.

**Layer 3 — Conversation History:**
```csharp
foreach (var entry in _historyService?.GetHistoryForSlug(slug)
    ?? new List<DialogueEntry>())
{
    string role = string.Equals(entry.Role, "assistant", ...) ? "assistant" : "user";
    messages.Add(new NPCOpenAIMessage { Role = role, Content = entry.Content });
}
messages.Add(new NPCOpenAIMessage { Role = "user", Content = playerMessage });
```

All previous turns (capped at `MaxHistoryPerNPC`, default 20) are added as OpenAI-format messages. The current player message is appended as the final `user` message.

### 4.2 The HTTP Request

The assembled message array is sent via `NPCLocalAIClient.ChatAsync()`:

```
POST http://127.0.0.1:8080/v1/chat/completions
Content-Type: application/json

{
  "model": "llama-3.2-3b-instruct:q8_0",    // or Gemma 3 4B, configured in RemoteModel
  "messages": [
    { "role": "system", "content": "..." },
    { "role": "user", "content": "..." },
    { "role": "assistant", "content": "..." },
    ...
  ],
  "temperature": 0.2,
  "top_p": 0.9,
  "max_tokens": 256
}
```

| Field | Source | Notes |
|---|---|---|
| `model` | `NPCDialogueManager.RemoteModel` | Configurable per deployment. Default: `llama-3.2-3b-instruct:q8_0`. **In our setup: Gemma 3 4B** |
| `messages` | System prompt + history + RAG + current input | Built by `SendToLocalAIAsync()` |
| `temperature` | `NPCProfile.Temperature` or `NPCLocalAIClient._temperature` | Per-NPC or global fallback |
| `max_tokens` | `NPCLocalAIClient._maxTokens` | Default 256 |

### 4.3 Retry Logic

`NPCLocalAIClient.ChatAsync()` includes exponential backoff retry:

| Attempt | Wait | Trigger |
|---|---|---|
| 1 | 500ms | Empty response |
| 2 | 1000ms | Empty response |
| 3 (final) | 1500ms | Empty response |
| — | Abort | Exception at any attempt |

After exhausting retries, the method logs the failure, fires `OnError`, and returns an empty string — the dialogue continues without a crash.

### 4.4 Response Cleanup

Two post-processing steps strip unwanted content:

1. **`<think>` block removal:** The regex `/<think>.*?<\/think>/s` strips chain-of-thought reasoning that some models (like Gemma 3) output before their actual answer.
2. **`NPCFlowTextSanitizer.CleanDialogueText()`:** Removes stray special characters, excessive whitespace, and other formatting artifacts.

> **🧑‍💻 Dev NPC:**
> *"Think of the LLM response as a raw diamond — it comes out of LocalAI covered in rough edges, think-tags, and the occasional existential crisis paragraph. CleanDialogueText is the polishing wheel. Without it, your NPC is quoting Descartes before selling you a sword."*

---

## 5. Step 4: Response Parsing

Once the response text is cleaned, `NPCDialogueSessionService.ProcessItemTradeActions()` scans for action tags embedded in the NPC's reply.

### 5.1 Action Tag Detection

The method delegates to `ItemTradeService.ProcessDialogueActions()`:

```csharp
string ProcessItemTradeActions(string responseText)
{
    if (string.IsNullOrWhiteSpace(responseText))
        return responseText;

    // Only process on server
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        return responseText;

    ItemTradeService tradeService = FindAnyObjectByType<ItemTradeService>(...);
    if (tradeService == null)
        return responseText;

    ulong playerClientId = _activePlayerClientIdOverride
        ?? NetworkManager.Singleton.LocalClientId;
    return tradeService.ProcessDialogueActions(responseText, playerClientId);
}
```

Key guard: action tags are **only processed on the server** in multiplayer. Clients see the raw tag text in the response, but nothing happens — the server runs the actual trade logic and syncs state via NetworkVariables.

### 5.2 How Action Tags Work

The NPC's response contains tags in this format:

```
[give_item:id=iron-sword]
[trade_item:id=health-potion,require=gold-coin]
```

`ItemTradeService.ProcessDialogueActions()` uses two regex patterns:

| Pattern | Regex | Example |
|---|---|---|
| Give item | `\[give_item:id=([a-z0-9_-]+)\]` | `[give_item:id=iron-sword]` |
| Trade item | `\[trade_item:id=([a-z0-9_-]+)(?:,require=([a-z0-9_-]+))?\]` | `[trade_item:id=health-potion,require=gold-coin]` |

Each match is validated against the `ItemCatalog`, executed (on server), and the tag text is **stripped** from the final response so the player never sees the action tag — only the natural-language text around it.

> **Example:**
>
> NPC response text (raw):
> ```
> Well fought! You've earned this blade. [give_item:id=iron-sword] Use it wisely.
> ```
>
> After parsing:
> ```
> Well fought! You've earned this blade. Use it wisely.
> ```
>
> Meanwhile, `iron-sword` was added to the player's inventory server-side.

---

## 6. Step 5: Game State Changes

Two categories of state changes happen as a result of the LLM response.

### 6.1 Item Trading

`ItemTradeService` provides a full server-authoritative item trading system:

| Method | Purpose |
|---|---|
| `ServerTryGiveItem(clientId, itemId)` | Validate + add item to player inventory |
| `ServerTryRemoveItem(clientId, itemId)` | Remove item (for trades where something is consumed) |
| `ServerCanGiveItem(clientId, itemId)` | Pre-check prerequisites without mutating state |
| `PlayerHasItem(clientId, itemId)` | Check if player already owns an item |

The trade flow for `[trade_item:id=X,require=Y]`:

```
1. Parse tag → itemId = "X", requireId = "Y"
2. Check: Does player have item "Y"? If no → log warning, skip
3. ServerTryGiveItem(clientId, "X") → adds to inventory
4. ServerTryRemoveItem(clientId, "Y") → consumes the required item
5. Strip tag from response text
```

### 6.2 Trust and Context Updates

Trust and expertise tracking are handled automatically by `PlayerDialogueContextService`:

| Metric | Updated When | Effect |
|---|---|---|
| `DialogueCount` | Every completed turn | Advances expertise tier (Rookie → Novice → Adept → Expert → Lead) |
| `TrustScore` | Per turn (configurable delta) | Affects NPC dialogue tone via prompt context |
| `VisitedLocations` | On scene change | Used for location-aware dialogue |

These don't happen synchronously in the pipeline — they're updated during initialization and cached for the next turn's prompt.

### 6.3 Persistence: Saving to Supabase

History persistence runs in `SendToLLMAsync()` after the response is received:

```csharp
await _historyService.AppendConversationAsync(
    respondingProfile,
    playerMessage,     // what the player said
    dialogueMessage    // what the NPC said back
);
```

This calls `SupabaseDialogueRepository.SaveTurnAsync()` which:

1. Resolves (or creates) a dialogue session for this player+NPC pair via the `find_or_create_dialogue_session` RPC
2. Inserts a `DialogueTurnRecord` row with session ID, player ID, role (`user` or `assistant`), and content
3. Notifies `SessionAnalyticsService` for session metadata tracking

```csharp
// Conceptual flow:
SaveTurnAsync(npcSlug, "user", playerMessage)      → INSERT INTO dialogue_turns
SaveTurnAsync(npcSlug, "assistant", dialogueMessage) → INSERT INTO dialogue_turns
```

Both turns (player and NPC) are saved so the full conversation history can be reconstructed on reconnect.

---

## 7. Step 6: UI Update

### 7.1 The Event Chain

The response makes its way back to the UI through UnityEvents:

```
NPCDialogueSessionService
    ↓ OnResponseComplete.Invoke(npcName, responseText)
NPCDialogueManager
    ↓ OnResponseComplete.Invoke(npcName, responseText)
NPCDialogueUIController
    ↓ HandleResponseComplete(npcName, response)
```

### 7.2 HandleResponseComplete

```csharp
void HandleResponseComplete(string npcName, string response)
{
    // 1. Update the AI text display
    SetAIText(response);

    // 2. Re-enable the input field
    SetInputEnabled(true);

    // 3. Clear the input text for the next turn
    if (PlayerInput != null)
        PlayerInput.text = "";
}
```

`SetAIText()` is a thin wrapper around `DialogueDisplayHelper`:

```csharp
public void SetAIText(string text)
{
    DialogueDisplayHelper.SetAIText(AiText, text);
}
```

### 7.3 The "..." Interim

While the LLM is processing, the UI shows an animated "..." placeholder via `HandleResponseStart`:

```csharp
void HandleResponseStart(string playerMessage)
{
    if (PlayerInput != null)
        PlayerInput.interactable = false;
    SetAIText("...");     // ← "thinking" indicator
}
```

The input is disabled during the request to prevent the player from sending another message before the current turn completes. It's re-enabled in `HandleResponseComplete` (or `HandleError` if something went wrong).

| UI State | When | Input Enabled | AI Text |
|---|---|---|---|
| Idle | Waiting for player input | ✅ Yes | Last response or empty |
| Thinking | LLM request in flight | ❌ No | `...` |
| Response received | After `OnResponseComplete` | ✅ Yes | Clean response text |
| Error | LLM or network failure | ✅ Yes | Error message |

> **🧑‍💻 Dev NPC:**
> *"The `...` is the most honest part of the whole pipeline. It's the game saying 'I have no idea what this NPC is about to say. Give me a second. Maybe two. Depends on how many tokens LocalAI decides to think about.'"*

---

## 8. Action Tag Reference

### 8.1 Supported Tags

| Tag | Syntax | Effect | Server Only? |
|---|---|---|---|
| **Give Item** | `[give_item:id=<item-slug>]` | Adds the specified item to the player's inventory. Fails silently if unknown item. | ✅ Yes |
| **Trade Item** | `[trade_item:id=<item-slug>,require=<req-slug>]` | Consumes `require` item from player, then gives `id` item. Skips entirely if player lacks the required item. | ✅ Yes |

### 8.2 Tag Processing Rules

| Rule | Behavior |
|---|---|
| **Order of execution** | `give_item` tags processed first (left to right), then `trade_item` tags |
| **Multiple tags** | All matching tags in a single response are processed |
| **Tag stripping** | Tags are removed from the response text before the player sees it |
| **Invalid items** | Unknown item slugs log a warning and are skipped — no crash |
| **Duplicate items** | `ServerTryAddItem` returns false for duplicates (already owned), logged but non-fatal |
| **Client-side** | Tags are silently ignored on non-server peers (guarded by `IsServer` check) |

### 8.3 Item Catalog Validation

All item IDs are validated against the `ItemCatalog` ScriptableObject at runtime:

```csharp
NPCItemDefinition def = _catalog.FindItem(itemId);
if (def == null)
{
    LogWarning($"Unknown item '{itemId}' requested — not in catalog.");
    return false;  // Item not found → skip gracefully
}

// Check prerequisites
foreach (string requiredId in def.RequiredItemIds)
{
    if (!PlayerHasItem(playerClientId, requiredId))
    {
        LogWarning($"Player {playerClientId} lacks prerequisite '{requiredId}' for '{itemId}'.");
        return false;
    }
}
```

This means the LLM can **request** any item by slug, but the game only honors items that exist in the catalog. A hallucinated item ID is safely rejected with a server-side warning — the player sees the NPC's text but no item appears.

---

## 9. Complete Code Flow

Here is the complete `SendDialogueMessage` flow across all three files involved, from UI to game state change.

### 9.1 Entry Point: NPCDialogueUIController

```csharp
// NPCDialogueUIController.cs — Step 1 & 6
void OnInputFieldSubmit(string text)
{
    if (!_readyForInput) return;

    string message = (text ?? string.Empty).Trim();
    if (message.Length == 0) return;

    if (PlayerInput != null)
        PlayerInput.interactable = false;

    if (ShouldUseNetworkBridge())
        NetworkBridge.SubmitPlayerMessage(message);
    else
        DialogueManager.SendDialogueMessage(message);
}

void HandleResponseComplete(string npcName, string response)
{
    SetAIText(response);
    SetInputEnabled(true);
    if (PlayerInput != null)
        PlayerInput.text = "";
}
```

### 9.2 Orchestrator: NPCDialogueManager

```csharp
// NPCDialogueManager.cs — Step 2
public void SendDialogueMessage(string playerMessage)
{
    if (_currentNPC == null)
    {
        OnError?.Invoke("No NPC selected");
        return;
    }

    // Resolve player name before each turn
    string activePlayerName = AuthNetworkBridge.ActivePlayerName;
    if (!string.IsNullOrWhiteSpace(activePlayerName) && activePlayerName != "Player")
    {
        _sessionService?.SetRuntimePlayerContext(activePlayerName);
    }

    _sessionService?.SendDialogueMessage(playerMessage, _currentNPC);
}
```

### 9.3 Core Pipeline: NPCDialogueSessionService

```csharp
// NPCDialogueSessionService.cs — Steps 3, 4, 5

public void SendDialogueMessage(string playerMessage, NPCProfile currentNpc)
{
    if (_isResponding || string.IsNullOrWhiteSpace(playerMessage))
        return;

    string trimmedMessage = playerMessage.Trim();
    _isResponding = true;
    _responseNPC = currentNpc;
    OnResponseStart?.Invoke(trimmedMessage);

    _ = SendToLLMAsync(_responseNPC, trimmedMessage);
}

async Task SendToLLMAsync(NPCProfile respondingProfile, string playerMessage)
{
    try
    {
        // ── Step 3a: Build RAG prompt ──
        string prompt = await BuildRAGPromptAsync(
            respondingProfile, playerMessage, reqId);

        // ── Step 3b: Send to LocalAI ──
        string dialogueMessage = await SendToLocalAIAsync(
            respondingProfile, playerMessage, prompt, reqId, slug);

        dialogueMessage = NPCFlowTextSanitizer.CleanDialogueText(dialogueMessage);

        // ── Step 4: Parse action tags ──
        string tradeProcessedMessage = ProcessItemTradeActions(dialogueMessage);
        dialogueMessage = tradeProcessedMessage;

        // ── Step 5: Persist to Supabase ──
        if (!string.IsNullOrWhiteSpace(dialogueMessage))
        {
            await _historyService.AppendConversationAsync(
                respondingProfile, playerMessage, dialogueMessage);
        }

        // ── Step 6: Fire completion event → UI update ──
        OnResponseComplete?.Invoke(
            respondingProfile.GetDisplayName(), dialogueMessage);
    }
    catch (Exception ex)
    {
        OnError?.Invoke(ex.Message);
    }
    finally
    {
        _isResponding = false;
        _responseNPC = null;
        ClearRuntimePlayerContext();
    }
}

async Task<string> SendToLocalAIAsync(
    NPCProfile profile, string playerMessage, string prompt,
    string reqId, string slug)
{
    // Build OpenAI-compatible messages array
    List<NPCOpenAIMessage> messages = new List<NPCOpenAIMessage>();

    // System prompt: NPC character definition + context
    string sysPrompt = NPCProfilePromptComposer.BuildSystemPrompt(
        profile, promptVars);
    messages.Add(new NPCOpenAIMessage {
        Role = "system",
        Content = sysPrompt + "\n" + prompt
    });

    // Conversation history
    foreach (var entry in _historyService.GetHistoryForSlug(slug))
    {
        messages.Add(new NPCOpenAIMessage {
            Role = entry.Role, Content = entry.Content
        });
    }

    // Current player message
    messages.Add(new NPCOpenAIMessage {
        Role = "user", Content = playerMessage
    });

    // Send via NPCLocalAIClient → HTTP POST to LocalAI
    return await _chatClient.ChatAsync(
        messages.ToArray(),
        profile.Temperature,
        modelOverride: ResolvedModelName
    );
}
```

### 9.4 Visual Summary

```
┌────────────────────────────────────────────────────────────┐
│                    NPC Dialogue Pipeline                     │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  [Player types message]                                     │
│       ↓                                                     │
│  OnInputFieldSubmit()                                       │
│  ├─ Validates non-empty, _readyForInput                     │
│  └─ Routes via NetworkBridge or direct                      │
│       ↓                                                     │
│  NPCDialogueManager.SendDialogueMessage()                   │
│  ├─ Guards: _currentNPC != null                             │
│  ├─ Resolves player name (AuthNetworkBridge)                │
│  └─ Delegates to SessionService                             │
│       ↓                                                     │
│  NPCDialogueSessionService.SendToLLMAsync()                 │
│  ├─ BuildRAGPromptAsync()                                   │
│  │   ├─ Queries Qdrant via NPCDialogueRetrievalService      │
│  │   └─ Falls back gracefully if no RAG context             │
│  ├─ SendToLocalAIAsync()                                    │
│  │   ├─ Composes system prompt (NPCProfilePromptComposer)   │
│  │   ├─ Appends history (NPCDialogueHistoryService)          │
│  │   ├─ Appends current player message                      │
│  │   └─ NPCLocalAIClient.ChatAsync() → HTTP POST            │
│  │       POST http://localhost:8080/v1/chat/completions     │
│  ├─ CleanDialogueText()                                     │
│  ├─ ProcessItemTradeActions()                               │
│  │   └─ ItemTradeService.ProcessDialogueActions()           │
│  │       ├─ [give_item:id=...] → ServerTryGiveItem()       │
│  │       └─ [trade_item:id=...,require=...] → Trade flow    │
│  ├─ _historyService.AppendConversationAsync()               │
│  │   └─ SupabaseDialogueRepository.SaveTurnAsync()          │
│  └─ OnResponseComplete.Invoke()                             │
│       ↓                                                     │
│  NPCDialogueUIController.HandleResponseComplete()            │
│  ├─ SetAIText(response)                                     │
│  ├─ SetInputEnabled(true)                                   │
│  └─ Clear input field                                       │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

### 9.5 Error Handling Summary

| Failure Point | What Happens | User Sees |
|---|---|---|
| No NPC selected | Logged warning, `OnError` fired | Error message in AI text area |
| LocalAI unreachable | Exception in `SendToLocalAIAsync`, `OnError` fired | Error message, input re-enabled |
| Empty LLM response | Retried up to 3 times, then `OnError` | Error message |
| RAG unavailable | Graceful fallback to prompt without context | Normal dialogue (no knowledge) |
| Invalid item tag | Warning logged, tag stripped, item not given | NPC text without item |
| Supabase save failure | Warning logged, dialogue still displayed | Normal dialogue (no persistence) |

---

> **🧑‍💻 Dev NPC:**
> *"Six steps, four services, one HTTP request, two regex parsings, and a database insert — all to turn 'hello' into 'greetings, adventurer' plus a health potion. It's just text in, text out — with a whole lot of awesome in between."*
