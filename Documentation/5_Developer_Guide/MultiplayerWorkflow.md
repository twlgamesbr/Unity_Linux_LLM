# Multiplayer Workflow and Scene Reliability Rules

**Last verified:** 2026-07-01 12:44 -03  
**Scene:** `Assets/Scenes/NPCDialoguePrototype1.unity`  
**Networking stack:** Unity Netcode for GameObjects `2.13.0`, Unity Transport via `UnityTransport`, Multiplayer Play Mode package present.

This project must use a documentation-first, GladeKit-verified workflow for multiplayer changes. The scene is still small enough that every NetworkManager, NetworkObject, transport field, bridge reference, and UI binding should be explicitly checked before and after edits.

## Required workflow for every multiplayer change

1. **Verify official Unity guidance first.**
   - Start from `https://docs.unity.com/en-us/multiplayer/llms.txt`.
   - Use local package docs under `Library/PackageCache/com.unity.netcode.gameobjects@*/Documentation~/` for exact package-version behavior.
   - Do not assume an NGO API shape; inspect the installed package source when needed.
2. **Use GladeKit MCP as the scene source of truth.**
   - Read the scene hierarchy.
   - Inspect target GameObject components.
   - Inspect Inspector properties for serialized references and flags.
   - Prefer GladeKit component/property tools over reading `.unity` YAML directly. Use YAML only as a fallback verification/repair path.
3. **Trace source before editing.**
   - Locate symbols with `search_files` / codebase audit.
   - Read relevant scripts before patching.
   - Check `.asmdef` references before adding Netcode-facing types.
4. **Use server authority for dialogue.**
   - Clients send compact requests to the server.
   - Server owns `NPCDialogueManager`, per-client session snapshots, evidence, and notebook state.
   - Server returns targeted responses/notebook updates only to the requesting client unless a feature explicitly requires broadcast.
5. **Choose the right NGO synchronization primitive.**
   - Use RPCs for request/response events and targeted per-client UI messages.
   - Use `NetworkVariable` only for state that must always synchronize to current and late-joining clients.
   - Use `INetworkSerializable` DTOs for bounded, explicit payloads.
6. **Keep state-only NetworkObjects cheap.**
   - A NetworkObject used only for game state/RPCs should normally disable transform synchronization.
   - Keep NetworkObject ordered before NetworkBehaviour components in the Inspector.
7. **Track every pass with a report.**
   - Write audit outputs under `.hermes/reports/` or `Documentation/5_Developer_Guide/`.
   - Reports must include docs consulted, GladeKit scene facts, code/test evidence, risks, and next validation steps.
8. **Verify before declaring success.**
   - Run GladeKit `compile_scripts` after code/scene changes.
   - Run editor tests where available.
   - Re-inspect critical Inspector references after scene edits.
   - Save the scene only after the verification pass is clean.

## Verified Unity guidance mapped to this project

| Unity guidance | Project rule |
|---|---|
| `NetworkManager` is the central netcode hub and must be started as server, host, or client before Netcode subsystems are available. | `NPCNetworkBootstrap` owns `NetworkManager`, `UnityTransport`, player prefab resolution, and explicit start mode. Do not start networking from an unrelated UI/controller script. |
| Unity recommends `UnityTransport` for client-server NGO unless unique transport needs exist. | Keep using `UnityTransport` on `NPCDialoguePrototypeNetwork`. WebSockets are currently enabled for this prototype. |
| For IP/port runtime configuration, Unity recommends `UnityTransport.SetConnectionData`; Relay setup must use Relay-specific APIs instead. | Current direct LAN/local mode uses explicit address/port/listen values. If Relay is added later, do not route it through the same `SetConnectionData` path. |
| RPCs are appropriate for direct server/client events; `SendTo.SpecifiedInParams` supports targeted client messages. | `NPCDialogueNetworkBridge` correctly uses server RPCs for client requests and targeted client RPCs for responses/notebook state. |
| `NetworkVariable` is best for state that must synchronize for current and late-joining clients. | Do not convert dialogue response streaming or private notebook pages to broadcast NetworkVariables unless late-join replay becomes a requirement. |
| State-only in-scene NetworkObjects can disable transform synchronization to avoid unnecessary initial sync cost. | `NPCDialogueRuntimeBridge` is a state/RPC bridge, so its `NetworkObject.SynchronizeTransform` should remain disabled. |
| Local multiplayer testing should use Multiplayer Play Mode for quick iteration and artificial network conditions for latency/jitter/loss. | Use MPP for host/client simulation, then add Network Simulator profiles such as 100–150 ms desktop latency plus 5–10% packet loss for reliability testing. |

## Current scene multiplayer baseline

### `NPCDialoguePrototypeNetwork`

Components verified via GladeKit:

- `NetworkManager`
- `UnityTransport`
- `NPCNetworkBootstrap`

Important Inspector state:

- `NPCNetworkBootstrap.networkManager` -> `NPCDialoguePrototypeNetwork`
- `NPCNetworkBootstrap.unityTransport` -> `NPCDialoguePrototypeNetwork`
- `NPCNetworkBootstrap.playerPrefab` -> `NPCPlayerAvatar`
- `NPCNetworkBootstrap.playerPrefabResourcesPath` = `Networking/NPCPlayerAvatar`
- `NPCNetworkBootstrap.configureOnAwake` = `true`
- `NPCNetworkBootstrap.autoStartInPlayMode` = `false`
- `UnityTransport.UseWebSockets` = `true`
- `UnityTransport.UseEncryption` = `false`
- `UnityTransport.MaxPayloadSize` = `6144`

### `NPCDialogueRuntimeBridge`

Components verified via GladeKit:

- `NetworkObject`
- `NPCNetworkSessionManager`
- `NPCDialogueNetworkBridge`

Important Inspector state:

- `NPCDialogueNetworkBridge.dialogueManager` -> `NPCDialogueSystem`
- `NPCDialogueNetworkBridge.sessionManager` -> `NPCDialogueRuntimeBridge`
- `NetworkObject.SynchronizeTransform` = `false`
- `NetworkObject.SpawnWithObservers` = `true`

### `NPCDialogueUI`

Components verified via GladeKit:

- `NPCDialogueUIController`
- `NotebookUIController`

Important Inspector state:

- `NPCDialogueUIController.dialogueManager` -> `NPCDialogueSystem`
- `NPCDialogueUIController.networkBridge` -> `NPCDialogueRuntimeBridge`
- `NotebookUIController.dialogueManager` -> `NPCDialogueSystem`
- `NotebookUIController.networkBridge` -> `NPCDialogueRuntimeBridge`
- Main UI object references are assigned for dropdown, input, AI text, stop button, portraits, notebook panels, answer dropdowns, and notebook text fields.

## Code baseline

Core networking files:

- `Assets/Scripts/Runtime/Networking/NPCTransportConfig.cs`
- `Assets/Scripts/Runtime/Networking/NPCNetworkBootstrap.cs`
- `Assets/Scripts/Runtime/Networking/NPCPlayerNetworkAvatar.cs`
- `Assets/Scripts/Runtime/Networking/NPCNetworkSessionManager.cs`
- `Assets/Scripts/Runtime/Networking/NPCDialogueMessageModels.cs`
- `Assets/Scripts/Runtime/Networking/NPCDialogueNetworkBridge.cs`
- `Assets/Scripts/Runtime/NPCDialogue/NPCNotebookStateFormatter.cs`
- `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueUIController.cs`
- `Assets/Scripts/Runtime/NPCDialogue/NotebookUIController.cs`

Tests currently covering multiplayer baseline:

- `Assets/Scripts/Tests/Editor/NPCNetworkingTests.cs`
- `Assets/Scripts/Tests/Editor/NPCDialogueNetworkingTests.cs`
- `Assets/Scripts/Tests/Editor/NPCNotebookStateTests.cs`

Assembly status:

- `Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef` references `Unity.Netcode.Runtime`.
- `Assets/Scripts/Tests/Editor/NPCSystem.Tests.asmdef` references `NPCSystem.Runtime` and `Unity.Netcode.Runtime`.

## Reliability risks to track next

1. **No automated multi-peer play-mode scenario yet.** Editor unit tests cover config/session/message DTOs, but not host/client interaction in Play Mode or Multiplayer Play Mode.
2. **Connection approval is not yet documented or enforced.** For public or semi-public network tests, add approval and version/protocol checks before accepting clients.
3. **Payload limits are implicit.** Current request/response DTOs sanitize strings, but a future pass should add explicit maximum lengths aligned with `UnityTransport.MaxPayloadSize`.
4. **Transport encryption is disabled.** Acceptable for local prototype testing; not acceptable for internet-facing deployment without Relay/DTLS/TLS planning.
5. **Single server-authoritative dialogue manager serializes dialogue turns.** Current bridge rejects overlapping dialogue requests with `Dialogue system is busy.` This is safe but limits concurrency; improve only after baseline host/client tests are reliable.
6. **GladeKit bridge version warning.** Current bridge reported `0.7.11`, while `0.7.12` is recommended by the tool. Avoid depending on advanced tools that are blocked or unstable until the bridge is updated.

## Repeatable verification checklist

- [ ] Fetch/consult Unity docs for the specific networking area being changed.
- [ ] Run GladeKit `get_scene_hierarchy`.
- [ ] Inspect `NPCDialoguePrototypeNetwork`, `NPCDialogueRuntimeBridge`, and `NPCDialogueUI` components/properties.
- [ ] Read/trace changed networking scripts and dependent UI/dialogue scripts.
- [ ] Run `compile_scripts` and confirm `hasErrors=false`.
- [ ] Run editor networking tests when a Unity test runner path is available.
- [ ] Re-inspect scene properties after changes.
- [ ] Save scene.
- [ ] Update or create a report with evidence and next risks.
