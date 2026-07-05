# NPC Dialogue Multiplayer — Complete System Dataflow

**Scene:** `Assets/Scenes/NPCDialoguePrototype1.unity`
**Date:** 2026-07-05
**Purpose:** Step-by-step dataflow reference for coverage test planning

---

## 1. Scene Initialization Sequence

The initialization is orchestrated by Unity's script execution order across multiple components:

```
Unity Scene Awake/Start
  │
  ├── NPCFlowLogger (order -3000)
  │     FindOrCreate() → singleton, platform logging overrides
  │
  ├── NPCNetworkBootstrap (order -2500)
  │     ApplyCommandLineOverrides() → -npc-server, -npc-client, -port, -address
  │     ApplyRuntimeSettings() → runInBackground
  │     ResolveReferences() → NetworkManager, UnityTransport, prefabs
  │     RegisterRuntimeCallbacks() → OnServerStarted, OnClientConnected, OnClientDisconnected
  │     ApplyTransportConfiguration() → UnityTransport config + prefab registration
  │     Start() → autoStart if batch mode or autoStartInPlayMode
  │
  ├── NPCDialogueManager (order -1500)
  │     (if initializeOnStart) → InitializeAsync()
  │
  ├── NPCDialogueBootstrapper (order -1000)
  │     (if initializeOnStart) → InitializeOnDemandAsync()
  │
  ├── NPCSceneInitializationController (order -2000)
  │   ┌────────────────────────────────────────────────────────────┐
  │   │ ResolveReferences():                                        │
  │   │   flowLogger, networkBootstrap, dialogueManager,            │
  │   │   backendReadiness, networkBridge, smokeValidator           │
  │   │                                                            │
  │   │ InitializeSceneInternalAsync() — 8-phase pipeline:         │
  │   │   Phase 1: Logger → ensure NPCFlowLogger singleton         │
  │   │   Phase 2: SceneReferences → ResolveReferences()           │
  │   │   Phase 3: NetworkTransport → bootstrap.ApplyTransport()   │
  │   │   Phase 4: DialogueServices → dialogueManager.Initialize() │
  │   │   Phase 5: BackendReadiness → backendReadiness.ProbeAsync()│
  │   │   Phase 6: NetworkBridge → networkBridge.InitializeAsync() │
  │   │   Phase 7: Validation → smokeValidator.ValidateConfig()    │
  │   │   Phase 8: Spawning → bootstrap.StartConfiguredMode()      │
  │   └────────────────────────────────────────────────────────────┘
  │
  ├── NPCBackendReadinessService
  │     ProbeAsync(probeLocalAi) → probes auth + LocalAI endpoints
  │
  ├── NPCDialogueSmokeValidator (order 500)
  │     ValidateConfiguration() → checks all refs assigned
  │     RunFirstQuestionSmokeAsync() → test LLM call, quit on failure
  │
  └── NPCDialogueBootstrapper (deferred)
        InitializeOnDemandAsync():
          1. ProbeAsync(probeLocalAi: true)
          2. dialogueManager.InitializeAsync()
          3. networkBridge.InitializeAsync()
          4. smokeValidator.ValidateConfiguration()
          5. auto-select default NPC (butler)
```

**Scene config snapshot (key flags):**

| Component | Flag | Value |
|---|---|---|
| `NPCSceneInitializationController` | `initializeOnStart` | true |
| `NPCSceneInitializationController` | `verifyBackendsDuringInitialization` | true |
| `NPCSceneInitializationController` | `initializeNetworkBridge` | true |
| `NPCSceneInitializationController` | `startNetworkingAfterInitialization` | false |
| `NPCDialogueManager` | `initializeOnStart` | false (deferred) |
| `NPCDialogueBootstrapper` | `initializeOnStart` | false |
| `NPCDialogueBootstrapper` | `autoSelectDefaultNPC` | true |
| `NPCDialogueManager` | `useQdrantRag` | true |
| `NPCDialogueBootstrapper` | `defaultNpcSlug` | butler |

---

## 2. Player Authentication Flow

```
Scene loads → AuthUIController.Start()
  │
  └── InitializeAsync()
        └── PlayerAuthService.InitializeAsync()
              └── PlayerSessionStore.Load()
                    ├── Stored session found & valid
                    │     → AuthEvents.onLoginSuccess.Invoke(username)
                    │       → AuthNetworkBridge.HandleAuthSuccess(username)
                    │
                    └── No stored session → Auth UI shown
                          User fills form → HandleSubmitPressed()
                            │
                            ├── Login mode
                            │     PlayerAuthService.LoginAsync(u, pwd, rememberMe)
                            │       POST http://localhost:5100/api/auth/login
                            │       Body: {username, password, rememberMe, deviceId}
                            │       Response: PlayerAuthSessionResponse
                            │         (sessionId, playerId, username, sessionToken,
                            │          expiresAtUtc, deviceId)
                            │
                            └── Register mode
                                  PlayerAuthService.RegisterAsync(u, pwd)
                                    POST http://localhost:5100/api/auth/register
                                    Body: {username, password}
                                    Response: PlayerAuthRegisterResponse
                                      (playerId, username, createdAtUtc)

AuthNetworkBridge.HandleAuthSuccess(username)
  1. Static AuthNetworkBridge.ActivePlayerName = username
  2. ResolveStartupMode() → Host or Client
  3. Close auth UI, activate gameplay Canvas
  4. NPCDialogueUIController.InitializeOnDemandAsync()
     → NPCDialogueBootstrapper.InitializeOnDemandAsync()
       → ProbeAsync, dialogueManager.Init, bridge.Init, validator.Validate
  5. If Host:
     → NPCNetworkBootstrap.StartConfiguredMode(Host)
     → Wait for PlayerObject (100-frame timeout)
     → NPCPlayerNetworkAvatar.SetDisplayName(username)
     → onHostStarted.Invoke()
  6. If Client:
     → Configure bootstrap connect address/port
     → NPCNetworkBootstrap.StartConfiguredMode(Client)
     → NPCPlayerNetworkAvatar.OnNetworkSpawn()
       → RegisterPlayerNameServerRpc(AuthNetworkBridge.ActivePlayerName)
```

---

## 3. Player Profile Management (Server Session Cache)

```
NPCNetworkSessionManager (on NPCDialogueRuntimeBridge)
  Per-client state: Dictionary<ulong ClientId, ClientSessionData>
    - playerDisplayName: string
    - selectedNpcSlug: string
    - historyByNpc: Dictionary<string, List<DialogueEntry>>
    - evidenceSnapshot: NPCEvidenceStateSnapshot

Seeding (first request from client):
  NPCDialogueNetworkBridge.EnsureClientSessionSeeded(clientId)
    → Capture baseline from DialogueManager (history snapshots)
    → sessionManager.SetAllHistorySnapshots(clientId, baseline)
    → sessionManager.SetEvidenceSnapshot(clientId, baselineEvidence)
    → sessionManager.SetPlayerDisplayName(clientId, resolvedName)

Applying (client request being processed):
  NPCDialogueNetworkBridge.ApplySessionStateToManager(clientId)
    → dialogueManager.ApplyHistorySnapshot(session.historyByNpc)
    → dialogueManager.ApplyEvidenceSnapshot(session.evidenceSnapshot)
    → dialogueManager.SetRuntimePlayerContext(playerName, clientId)

Sync (after response completes):
  NPCDialogueNetworkBridge.SyncSessionFromManagerState(clientId)
    → sessionManager.SetAllHistorySnapshots(clientId, dm.CaptureHistorySnapshot())
    → sessionManager.SetEvidenceSnapshot(clientId, dm.CaptureEvidenceSnapshot())

Clear (client disconnect):
  NPCDialogueNetworkBridge.HandleClientDisconnected(clientId)
    → sessionManager.ClearClientSession(clientId)
```

---

## 4. Dialogue Initialization Flow

```
NPCDialogueManager.InitializeAsync()
  │
  ├── AutoAssignReferencesIfNeeded()
  │     chatClient ← NPCLocalAIClient (on same GameObject)
  │     localRag ← NPCLocalRAG
  │     qdrantRag ← QdrantRAGService
  │     actionPlanner ← NPCDialogueActionPlanner
  │     evidenceState ← NPCEvidenceState
  │     Copy remoteHost:remotePort → chatClient
  │
  ├── ValidateReferences()
  │
  ├── BuildProfileIndex()
  │     Loads NPCProfile[] → _profilesBySlug[slug] = profile
  │     Profiles: Butler, Maid, Chef (3 ScriptableObjects)
  │     Each has: identity, system prompt, behavior,
  │               action permissions, sampling config,
  │               knowledge source path, LoRA adapter,
  │               history save path
  │
  ├── LoadAllHistories()
  │     For each profile:
  │       path = profile.GetHistorySaveFile()
  │       → NPCHistoryStore.Load(path)
  │         → Read JSON from Application.persistentDataPath/NPCDialogue/{slug}.json
  │         → NormalizeForChatTemplate() → enforce user/assistant alternation
  │         → Return List<DialogueEntry>
  │       → _historyByNpc[slug] = entries
  │
  └── LoadOrBuildRagEmbeddingsAsync()
        └── EnsureRagReadyAsync()
              → Wait for embedder to respond (poll every 2s, up to 12s)
              → Check NPCRAGMetadata freshness
              ├── Fresh: Load .rag file (Assets/StreamingAssets/RAG/NPCDialogues-minilm-chunked.rag)
              └── Stale: NPCRAGImporter.RebuildAsync()
                    → Read knowledge files (StreamingAssets/NPCs/{slug}/knowledge.md)
                    → Chunk, embed via NPCLocalAIEmbedder
                    → Save to .rag file
```

---

## 5. Dialogue Send/Receive Flow (Full Round-Trip)

```
User types message → NPCDialogueUIController.OnInputFieldSubmit(question)
  │
  ├── Networked mode → networkBridge.SubmitPlayerMessage(question)
  └── Direct mode → dialogueManager.SendMessage(question)

NETWORKED PATH (via NPCDialogueNetworkBridge):
  Client calls SubmitPlayerMessage(question)
    ├── if IsServer → dialogueManager.SendMessage(question) directly
    └── if IsClient → SubmitDialogueServerRpc(NPCDialogueRequestMessage)
          → [RPC: Client → Server]
          → HandleSubmitDialogueServerAsync(clientId, request)
            │
            ├── Active request in progress?
            │     → EnqueueDialogueRequest(clientId, request)
            │     → Wait for queue
            │
            └── BeginDialogueRequestAsync(clientId, request)
                  │
                  ├── WaitForResolvedPlayerNameAsync(clientId)
                  │     → Find NPCPlayerNetworkAvatar.DisplayName
                  │     → Fallback "Player {OwnerClientId}"
                  │
                  ├── ApplySessionStateToManager(clientId)
                  │     → Restore NPC-specific history + evidence
                  │
                  └── dialogueManager.SwitchToNPCAsync(slug)
                        └── dialogueManager.SendMessage(playerMessage)

NPCDialogueManager.SendMessage(playerMessage)
  │
  ├── onResponseStart.Invoke(playerMessage)
  ├── _isResponding = true
  │
  └── SendToLLMAsync(profile, playerMessage) — 5-step pipeline:
        │
        ├── [1] BuildRAGPromptAsync(profile, playerMessage)
        │     ├── Try QdrantRAGService.SearchMemoryAsync() (useQdrantRag = true)
        │     ├── Fallback NPCLocalRAG.Search() (enableRAG = true)
        │     ├── evidenceState.BuildNpcStateLine(slug)
        │     ├── evidenceState.BuildStateContextString()
        │     ├── IsTechnicalCodebaseQuestion() → specialized instruction
        │     └── Return combined prompt with knowledge context
        │
        ├── [2] actionPlanner.Plan(playerMessage, profile)
        │     ├── Keyword match: notes/map/solve/help/hint/evidence/suspect
        │     └── Return NPCDialogueActionPlan → BuildPromptHint(plan)
        │
        ├── [3] SendToLocalAIAsync(profile, message, prompt, reqId, slug)
        │     ├── NPCProfilePromptComposer.BuildSystemPrompt(profile)
        │     ├── Inject authenticated player name
        │     ├── Build messages array: [system, history..., user message]
        │     └── NPCLocalAIClient.ChatAsync(messages, temperature)
        │           → POST http://localhost:8080/v1/chat/completions
        │           → Model: llama-3.2-3b-instruct:q8_0
        │           → Retry 3x with 500ms exponential backoff
        │           → Strip <think>...</think> blocks
        │
        ├── [4] AppendConversationAsync(profile, message, response)
        │     ├── Add user + assistant to _historyByNpc[slug]
        │     ├── TrimHistory(maxHistoryPerNPC = 20)
        │     └── NPCHistoryStore.Save(path, history)
        │
        ├── [5] NPCDialogueActionHandler.AnalyzeResponse()
        │     ├── Parse LLM response for game actions
        │     ├── RecordClue(), AddItem(), AddLocation()
        │     ├── SetNpcMood(), AdjustNpcTrust()
        │
        ├── onResponseComplete.Invoke(displayName, response)
        │
        └── [Server: RPC back to client]
              ├── SyncSessionFromManagerState(clientId)
              ├── ReceiveResponseCompleteClientRpc(...)
              ├── SendNotebookStateToClient()
              └── TryProcessNextQueuedRequest()

CLIENT RECEIVES RESPONSE:
  NPCDialogueUIController.HandleResponseComplete()
    → SetAIText(fullResponse)
    → Enable input, clear, select
    → NotebookUIController.RefreshNotebookState()
```

---

## 6. Qdrant RAG Query Flow

```
NPCDialogueManager.BuildRAGPromptAsync()
  │
  └── if useQdrantRag && qdrantRag != null:
        │
        └── QdrantRAGService.SearchMemoryAsync(query, limit, reqId, npcSlug)
              │
              ├── Resolve NPCLocalAIEmbedder (scene find if null)
              │
              ├── embedder.Embeddings(query)
              │     → POST http://localhost:8080/v1/embeddings
              │       { model: "nomic-embed-text-v1.5", input: query }
              │     → Retry 3x
              │     → Return List<float> embedding vector
              │
              ├── Build QdrantSearchRequest
              │     { vector: [...], limit, with_payload: true }
              │
              ├── POST http://localhost:6333/collections/
              │         unity_linux_llm_codebase_v1/points/search
              │
              ├── Parse QdrantSearchResponse
              │     → Extract payload.text from each QdrantPoint
              │
              └── Return concatenated text (or empty on failure)
                    → Falls through to local RAG on error/empty
```

---

## 7. NPC Knowledge Retrieval Flow (Local .rag + Qdrant)

```
NPCDialogueManager.BuildRAGPromptAsync(profile, playerMessage)
  │
  ├── PRIMARY: Qdrant (if enabled)
  │     qdrantRag.SearchMemoryAsync(query, k, reqId, slug)
  │     Falls back to local RAG on HTTP error or empty result
  │
  ├── SECONDARY: Local RAG (NPCLocalRAG)
  │     EnsureRagReadyAsync() → poll embedder up to 12s
  │     localRag.Search(playerMessage, k, profile.GetRagCategory())
  │       → NPCSimpleSearch.IncrementalSearch(embedding, group)
  │         → InverseDotProduct against stored embeddings in group
  │         → Sort by distance
  │       → Return (string[] results, float[] scores)
  │     → Concatenate into ragKnowledge string
  │
  ├── EVIDENCE STATE INJECTION
  │     evidenceState.BuildNpcStateLine(slug)
  │     evidenceState.BuildStateContextString()
  │
  ├── TECHNICAL QUESTION DETECTION
  │     IsTechnicalCodebaseQuestion() → specialized instruction
  │
  └── Return combined prompt with all context strings
```

**Knowledge sources (StreamingAssets):**
- `StreamingAssets/NPCs/butler/knowledge.md`
- `StreamingAssets/NPCs/maid/knowledge.md`
- `StreamingAssets/NPCs/chef/knowledge.md`
- `StreamingAssets/RAG/NPCDialogues-minilm-chunked.rag`

**Qdrant collection:** `unity_linux_llm_codebase_v1`

---

## 8. RPC/Network Call Queue Management

```
NPCDialogueNetworkBridge (NetworkBehaviour on NPCDialogueRuntimeBridge)

Queue State:
  Queue<PendingDialogueRequest> _pendingRequests
  ulong? _activeClientId
  string _activeRequestId

Request Arrival → HandleSubmitDialogueServerAsync(clientId, request)
  ├── _activeClientId == null && !isResponding
  │     → BeginDialogueRequestAsync(clientId, request) IMMEDIATELY
  └── Else
        → EnqueueDialogueRequest(clientId, request) → queue.WaitAsync()

Request Completion:
  → onResponseComplete fires
  → SyncSessionFromManagerState()
  → RPCs back to client
  → _activeClientId = null
  → _activeRequestId = ""
  → TryProcessNextQueuedRequest()
        ├── Dequeue next pending request
        ├── Check session still valid (selected NPC exists)
        └── BeginDialogueRequestAsync() for next

Client Disconnect → HandleClientDisconnected(clientId):
  → RemoveQueuedRequestsForClient(clientId)
  → sessionManager.ClearClientSession(clientId)
  → If was active: CancelRequests()
  → TryProcessNextQueuedRequest()

RPC Contract — Client → Server:
  RequestNpcSelectionServerRpc(NPCDialogueSelectionMessage)
  SubmitDialogueServerRpc(NPCDialogueRequestMessage)
  CancelActiveRequestServerRpc()

RPC Contract — Server → Client (single target):
  ReceiveNpcChangedClientRpc(NPCDialogueResponseMessage)
  ReceiveResponseStartClientRpc(NPCDialogueResponseMessage)
  ReceiveResponseUpdatedClientRpc(NPCDialogueResponseMessage)
  ReceiveResponseCompleteClientRpc(NPCDialogueResponseMessage)
  ReceiveErrorClientRpc(string)
  ReceiveNotebookStateClientRpc(NPCNotebookStateMessage)
```

---

## 9. Evidence & Notebook State Tracking

```
NPCEvidenceState (on NPCDialogueSystem)
  State:
    discoveredClues: List<ClueEntry> (dedup by hash)
    obtainedItems: List<string>
    visitedLocations: List<string>
    _npcMoods: Dictionary<string, string>
    _npcTrust: Dictionary<string, int>

State Mutations (from NPCDialogueActionHandler.AnalyzeResponse):
  → RecordClue(npcSlug, clueText, category)
  → AddItem(itemId)
  → AddLocation(locationName)
  → SetNpcMood(slug, mood)
  → AdjustNpcTrust(slug, delta)

Snapshot Lifecycle (multiplayer):
  1. CaptureBaselineState() at bridge init
  2. EnsureClientSessionSeeded(clientId) → copy baseline
  3. BeginDialogueRequestAsync() → ApplySessionStateToManager()
  4. Dialogue processes → ActionHandler may modify state
  5. SyncSessionFromManagerState() → persist back to session
  6. SendNotebookStateToClient() → RPC to client

Notebook UI Rendering:
  NPCNotebookStateFormatter.Build(snapshot, slug)
    → NPCNotebookStateMessage (notesPageLeft, notesPageRight)
  NotebookUIController.ApplyNotebookState(msg)
    → notesText1.text, notesText2.text

Puzzle Solving:
  Correct answers: "Professor Pluot", "Living Room", "A Hollow Bible"
  NotebookUIController.SubmitAnswer()
    → match TMP_Dropdown selections → show success/fail
```

---

## 10. Server Spawning & Item Flow

```
NPCServerCharacterSpawner (on Backend):
  NetworkManager.OnServerStarted
    → SpawnServerNpcCharacters()
      → Iterate NPCDialogueManager.Profiles
      → Instantiate NPCServerCharacter at grid position
        (Grid: start -4,1,6, spacing 2.75,0,2.5, max 3 columns)
      → NetworkObject.Spawn()
      → NPCServerCharacter.InitializeIdentity(profile)

NPCTransferableItemSpawner (on Backend):
  NetworkManager.OnServerStarted
    → SpawnTestItem()
      → Despawn existing items
      → Instantiate NPCTransferableItem at (0,1,4)
      → NetworkObject.Spawn()
      → AssignToNpc("butler") or PlaceInWorld()

NPCPlayerAvatar (spawned per client):
  Prefab: Resources/Networking/NPCPlayerAvatar
  OnNetworkSpawn():
    → Server: set fallback "Player {OwnerClientId}"
    → Client owner: RegisterPlayerNameServerRpc(ActivePlayerName)
      → Server validates → sets playerDisplayName NetworkVariable
      → Syncs to all clients via NGO
```

---

## Coverage Priority Matrix

| Class | File | Priority | Testable Paths |
|---|---|---|---|
| `NPCHistoryStore` | `NPCDialogue/NPCHistoryStore.cs` | High | JSON load/save, normalization, malformed input, deletion |
| `NPCDialogueManager` | `NPCDialogue/NPCDialogueManager.cs` | High | Profile selection, fallback, RAG prompt building, init |
| `NPCDialogueNetworkBridge` | `Networking/NPCDialogueNetworkBridge.cs` | High | RPC routing, queue management, session lifecycle |
| `NPCNetworkSessionManager` | `Networking/NPCNetworkSessionManager.cs` | High | Session CRUD, client isolation, cleanup |
| `NPCLocalAIClient` | `LocalAI/NPCLocalAIClient.cs` | Medium | HTTP request, retry, response parsing |
| `NPCLocalAIEmbedder` | `LocalAI/NPCLocalAIEmbedder.cs` | Medium | Embedding request/response, error handling |
| `QdrantRAGService` | `NPCDialogue/QdrantRAGService.cs` | Medium | Search request, embedder resolution, error fallback |
| `NPCSimpleSearch` | `LocalRAG/NPCSimpleSearch.cs` | Medium | Vector search, incremental results |
| `NPCEvidenceState` | `NPCDialogue/NPCEvidenceState.cs` | Medium | Clue dedup, snapshot round-trip |
| `NPCNetworkBootstrap` | `Networking/NPCNetworkBootstrap.cs` | Medium | Transport config, prefab registration, CLI parsing |
| `PlayerAuthService` | `NPCDialogue/PlayerAuthService.cs` | Medium | HTTP login/register, session restore, persistence |
