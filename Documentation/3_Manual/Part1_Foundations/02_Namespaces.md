# Chapter 02: Namespaces — Your Code's Address Book

> **Duration:** 15-minute read
> **Audience:** Junior to mid-level Unity developers
> **Prerequisites:** Basic C# knowledge; Chapter 01 (Project Layout)

---

> **NPC Senior Dev says:**
> *"Namespaces? Oh, you mean those things the guy before you ignored because 'it compiles fine without them.' Yeah, he also thought WebGL builds were 'just a checkbox.' We don't talk about him anymore."*

---

## 1. What's a Namespace, Actually?

A **namespace** in C# is a logical container for types — classes, structs, enums, interfaces. Think of it as a last name for your code. When you write:

```csharp
namespace NPCSystem.Dialogue.Core
```

you're saying: *"This file belongs to the Core Dialogue module of the NPC System."*

It's not a physical folder. It's not a file path. It's a **declaration of belonging**.

```csharp
// These two files live in different folders on disk,
// but share the same logical home:
namespace NPCSystem.Dialogue.Core
{
    public class NPCDialogueManager { }
}

namespace NPCSystem.Dialogue.Core
{
    public class NPCDialogueValidator { }
}
```

### Why Unity Doesn't Force Them

Unity is famously lax about namespaces. You can write every script in `GlobalNamespace` (no namespace at all) and the project compiles. The engine resolves type names by class name alone — until two classes collide.

**But that's the trap.** Unity's permissiveness means you won't feel the pain until you have:

1. Two classes named `Config` in different modules.
2. A teammate who can't tell which `SessionService` a using directive refers to.
3. A WebGL build that's 50% bigger than it needs to be.

> **NPC Senior Dev says:**
> *"I inherited a project with 47 classes all in GlobalNamespace. Finding anything was like playing Where's Waldo? except Waldo is named 'Manager' and there are fourteen of him."*

---

## 2. Namespace ≠ Folder (Critical!)

Here's the mistake every junior (and more than a few seniors) makes:

```
Folder:   Assets/Scripts/Runtime/Dialogue/Core/
           ↓  (you assume)
Namespace: NPCSystem.Dialogue.Core
```

**Unity does NOT automatically set the namespace from the folder path.** When you create a new C# script via `Assets > Create > C# Script`, Unity generates:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() { }
}
```

No namespace. Zero. The folder path is irrelevant to the compiler.

### The Mirror Rule

> **Your folder structure should MIRROR your namespace structure — but they are ENFORCED separately.**

This means:

| Folder | Expected Namespace | Actual? |
|---|---|---|
| `Dialogue/Core/` | `NPCSystem.Dialogue.Core` | Only if you type it |
| `Character/NPC/` | `NPCSystem.Character.NPC` | Only if you type it |
| `Network/Core/` | `NPCSystem.Network.Core` | Only if you type it |

### How to Fix This

**Option A — Manual discipline:** Every time you create a script, replace the header:

```csharp
using UnityEngine;

namespace NPCSystem.Dialogue.Core
{
    public class MyNewScript : MonoBehaviour
    {
    }
}
```

**Option B — .editorconfig + IDE (best):** Some IDEs support a `file_header_template` in `.editorconfig`. For example:

```ini
# .editorconfig at project root
[*.cs]
file_header_template = using UnityEngine;\n\nnamespace NPCSystem.Dialogue.Core\n{\n    public class $FILENAME : MonoBehaviour\n    {\n    }\n}
```

> ⚠️ **Reality check:** Option B is fragile and IDE-dependent. What actually works at scale is **code review** — the `.codebaserules.yaml` config has a `NAM07` rule that checks namespace declarations against folder paths automatically. CI fails if they don't match. That's your safety net.

---

## 3. Why One Namespace Kills WebGL Performance

This is the part most tutorials skip — and it's the **real reason** we care about namespaces in this project.

### The Short Version

WebGL builds use **IL2CPP** (Intermediate Language To C++), which then compiles to WebAssembly. Unlike Mono (the JIT-compiled runtime), IL2CPP does **Ahead-of-Time (AOT) compilation**: it generates native C++ code for every type reachable in your assemblies.

**With one flat namespace + one assembly definition file (asmdef):**

- ALL types in that assembly are reachable by default.
- IL2CPP compiles ALL of them.
- The WebGL linker cannot strip unused types because they're all in the same compilation unit.
- Your `.wasm` file bloats. Your load time balloons. Your users on mobile Safari download megabytes of dead code.

### The Long Version

Let's walk through what happens at build time.

#### Scenario A: Everything in GlobalNamespace, one asmdef

```
Assembly: NPCSystem.Runtime.dll
Contains: 150 classes across Dialogue, Auth, Monitoring, Items, Network
WebGL build: IL2CPP generates C++ → WASM for all 150 classes
Final .wasm: ~25 MB (including 40 classes you never use in WebGL)
```

Every class that `MonoBehaviour.FindObjectOfType` might theoretically touch? Compiled. Every enum that exists in the assembly? Compiled. That `NPCSystem.Monitoring.Datadog` logger you only use in the editor? **Compiled into your WebGL build.**

#### Scenario B: Domain namespaces + domain asmdefs

```
Assembly: NPCSystem.Runtime.dll
Contains: Dialogue, Auth, Items, Character, Network, etc.
But: NPCSystem.Monitoring.dll is a SEPARATE asmdef
WebGL build: IL2CPP only compiles NPCSystem.Runtime
NPCSystem.Monitoring.dll: NOT included in WebGL → ZERO bytes wasted
```

### The Real Cost

> **"If you have `NPCSystem.Monitoring.Datadog` namespace but ZERO scripts reference it from gameplay code, the assembly still contains all types from `NPCSystem.Monitoring`. But with proper asmdef isolation, Monitoring compiles separately and can be excluded from WebGL builds entirely."**

This is the key insight: **empty or dead namespaces don't cost anything by themselves.** A namespace is a compile-time concept — it disappears in IL. What costs is **having everything in one namespace AND one asmdef**, because that forces the compiler to assume everything is reachable.

### Summary Table

| Situation | IL2CPP Behavior | WebGL Size Impact |
|---|---|---|
| No namespace, one asmdef | All types compiled | 🔴 Large |
| Domain namespaces, one asmdef | All types still compiled (same assembly) | 🔴 Large |
| Domain namespaces, domain asmdefs | Only referenced assemblies compiled | 🟢 Small |
| Domain asmdefs + Linker stripping | Unreferenced types removed from compiled set | 🟢🟢 Smallest |

> **NPC Senior Dev says:**
> *"You know what's fun? Debugging a 40 MB WebGL build on a 2019 iPad. Actually no, it's not fun. It's the digital equivalent of watching paint dry while being waterboarded. Use asmdefs. Your users will thank you."*

---

## 4. Our 16 Namespace Map

Here is the complete namespace map for the NPCSystem project. Every `.cs` file under `Assets/Scripts/Runtime/` belongs to exactly one of these namespaces.

| # | Namespace | Folder | Contains |
|---|---|---|---|
| 1 | `NPCSystem.Auth` | `Auth/` | Authentication, login, identity providers |
| 2 | `NPCSystem.Character.Animation` | `Character/Animation/` | Character animation controllers, blend trees, rigging |
| 3 | `NPCSystem.Character.NPC` | `Character/NPC/` | NPC behavior trees, personality, state machines |
| 4 | `NPCSystem.Character.Player` | `Character/Player/` | Player controller, input handling, camera |
| 5 | `NPCSystem.Dialogue.Core` | `Dialogue/Core/` | Dialogue engine, line resolution, branching logic |
| 6 | `NPCSystem.Dialogue.Persistence` | `Dialogue/Persistence/` | Save/load dialogue state, progress tracking |
| 7 | `NPCSystem.Dialogue.RAG` | `Dialogue/RAG/` | Retrieval-Augmented Generation, embedding queries |
| 8 | `NPCSystem.Dialogue.Session` | `Dialogue/Session/` | Active dialogue sessions, context management |
| 9 | `NPCSystem.Dialogue.UI` | `Dialogue/UI/` | Dialogue UI panels, subtitles, choice buttons |
| 10 | `NPCSystem.Initialization` | `Initialization/` | Bootstrap, scene init, dependency wiring |
| 11 | `NPCSystem.Items` | `Items/` | Inventory, item definitions, trading |
| 12 | `NPCSystem.LocalAI` | `LocalAI/` | LocalAI HTTP client, config, health checks |
| 13 | `NPCSystem.Monitoring` | `Monitoring/Core/` | Logging, metrics, telemetry (separate asmdef!) |
| 14 | `NPCSystem.Monitoring.Datadog` | `Monitoring/Datadog/` | Datadog-specific APM integration |
| 15 | `NPCSystem.Network.Bridges` | `Network/Bridges/` | Multiplayer relay, matchmaking bridges |
| 16 | `NPCSystem.Network.Core` | `Network/Core/` | Network transport, RPC layer, session sync |

**Key asmdef boundaries:**

| asmdef | Location | Includes |
|---|---|---|
| `NPCSystem.Runtime` | `Scripts/Runtime/` | All namespaces EXCEPT Monitoring |
| `NPCSystem.Monitoring` | `Scripts/Runtime/Monitoring/Core/` | `NPCSystem.Monitoring`, `NPCSystem.Monitoring.Datadog` |

> ⚠️ **Critical:** `NPCSystem.Monitoring` does **NOT** reference `NPCSystem.Runtime`. This means Monitoring code can use its own types, but gameplay types (Dialogue, Auth, etc.) are invisible to it. This is intentional — monitoring should never create a hard dependency on gameplay logic. If you need to log from gameplay code, you call an interface that lives in `NPCSystem.Monitoring`; the runtime depends on monitoring, not the other way around.

---

## 5. The File Header Pattern

Every `.cs` file in this project follows this exact header template:

```csharp
using UnityEngine;
using NPCSystem.Dialogue.Core;

namespace NPCSystem.Dialogue.Session
{
    public class NPCDialogueSessionService : MonoBehaviour
    {
        // ...
    }
}
```

### The Rules

1. **`using UnityEngine;`** always first (system using directives at the top).
2. **Project using directives** come next, grouped by domain.
3. **Blank line** before the `namespace` declaration.
4. **Opening brace** on the same line as `namespace`.
5. **Type declaration** inside the namespace block, indented by one level.
6. No additional `using` statements inside the namespace block.

### What About Editor Scripts?

Editor scripts (in `Assets/Scripts/Editor/`) use the `NPCSystem.Editor` namespace and reference `UnityEditor` instead of (or in addition to) `UnityEngine`.

---

## 6. Using Directives — The Import Cost

A common fear among developers new to namespaces:

> *"If I add `using NPCSystem.Dialogue.Core;` at the top of every file, won't that bloat my build?"*

**No.**

### In Mono (Standalone, Android, iOS)

The `using` directive is a **compile-time convenience**. It tells the compiler: *"When I type `NPCDialogueManager`, look in this namespace too."*

- `using` directives add **zero bytes** to the compiled IL.
- Only **types you actually reference in your code** produce IL instructions.
- A file with 20 `using` directives that references 2 classes produces the same IL as one with 2 `using` directives.

### In IL2CPP (WebGL, Consoles)

IL2CPP adds an extra wrinkle: the **managed linker** (IL Linker) analyzes your assemblies and tries to strip unused types. But the linker works at the **type level**, not the namespace level. A `using` directive doesn't force a type to be linked — only actual usage does.

**However**, if assembly A references assembly B, the linker looks at ALL types in assembly B that could potentially be used by A. This is where **asmdef boundaries** matter more than namespace boundaries.

### Our GlobalUsings.cs Attempt

Early in the project, we tried a `GlobalUsings.cs` file (C# 10 feature):

```csharp
// global using NPCSystem.Dialogue.Core;
// global using NPCSystem.Auth;
// etc.
```

**Result:** Removed within a week. Why?

- We were targeting **C# 9** compatible with Unity's IL2CPP toolchain at the time.
- Global usings are a C# 10 compiler feature. Unity 2022 LTS supports C# 9 by default (with C# 10 available via the `allowUnsafeCode` + `API Compatibility Level` dance, but it's not guaranteed on all build targets).
- Even if supported, global usings hide dependencies. A script in the `Items` domain that silently compiles because of a global `using NPCSystem.Network.Core` is a maintenance trap.

> **Current stance:** Explicit `using` directives in every file. Verbose but honest. Your IDE folds them anyway; your future self will thank you for not hiding cross-domain coupling.

---

## 7. Namespace Migration Diary

The project didn't start with this clean namespace hierarchy. Here's how it evolved.

### Phase 1: The Flat Mess (Week 1-2)

```
Original structure:
Assets/Scripts/Runtime/
├── NPCDialogueManager.cs          (no namespace)
├── NPCDialogueValidator.cs        (no namespace)
├── NPCDialogueUI.cs               (no namespace)
├── NPCCharacterController.cs      (no namespace)
├── NPCNetworkBridge.cs            (no namespace)
├── NPCItemTradeService.cs         (no namespace)
├── ... 30+ flat files
```

Everything in `GlobalNamespace`. Works fine until you have `NPCDialogueManager` depending on `NPCNetworkBridge` depending on `NPCDialogueManager` — circular references hidden by the fact that everything's in the same flat bucket.

### Phase 2: The Great Rename (Week 3)

Day 1 — team decided on the 16-namespace map from §4.

Day 2 — opened every file and changed:

```csharp
// Before:
public class NPCDialogueManager : MonoBehaviour
{
    public NPCNetworkBridge networkBridge;
}

// After:
namespace NPCSystem.Dialogue.Core
{
    public class NPCDialogueManager : MonoBehaviour
    {
        public NPCNetworkBridge networkBridge;
    }
}
```

**The compile error party lasted 2 hours.** Every file that referenced `NPCDialogueManager` needed a `using NPCSystem.Dialogue.Core;` at the top. But it was mechanical — no logic changes, just adding directives.

### The sed Command (For the Brave)

If you're migrating a flat project, these `sed` commands will add a namespace to every `.cs` file in a folder:

```bash
# Add namespace to all files in Dialogue/Core/
for f in Assets/Scripts/Runtime/Dialogue/Core/*.cs; do
  # Insert namespace line after last using directive (or after file header)
  sed -i '/^using /a\
namespace NPCSystem.Dialogue.Core\n{' "$f"
  # Append closing brace at end of file
  echo '}' >> "$f"
done
```

> ⚠️ **Warning:** This sed approach is crude. It doesn't handle:
> - Files with no `using` directives
> - Files that already have a namespace
> - Partial classes split across files
> - Interface-only files
>
> A proper migration uses **Roslyn-based refactoring** (dotnet-format or your IDE's built-in rename). The sed version is a conversation starter, not a production tool.

### Phase 3: asmdef Split (Week 4)

After the namespace rename, we split the assembly definitions:

1. Created `NPCSystem.Runtime.asmdef` at `Scripts/Runtime/` — includes everything.
2. Created `NPCSystem.Monitoring.asmdef` at `Scripts/Runtime/Monitoring/Core/` — isolates monitoring.
3. Removed the Monitoring folder from the main Runtime asmdef's `Assembly Definition References`.
4. **Result:** Monitoring types are now invisible to gameplay code by default. You cannot accidentally call Datadog logging from `Dialogue.Core` without explicitly adding a reference.

---

## 8. Quick Reference

### Complete Namespace → Folder → Type Map

| Namespace | Folder Path | Example Types |
|---|---|---|
| `NPCSystem.Auth` | `Auth/` | `NPCFirebaseAuth`, `NPCIdentityProvider`, `NPCAuthConfig` |
| `NPCSystem.Character.Animation` | `Character/Animation/` | `NPCAnimationController`, `NPCBlendTreeConfig` |
| `NPCSystem.Character.NPC` | `Character/NPC/` | `NPCBehaviorTree`, `NPCPersonalityProfile`, `NPCStateMachine` |
| `NPCSystem.Character.Player` | `Character/Player/` | `NPCPlayerController`, `NPCPlayerInput`, `NPCPlayerCamera` |
| `NPCSystem.Dialogue.Core` | `Dialogue/Core/` | `NPCDialogueManager`, `NPCDialogueValidator`, `NPCDialogueLine` |
| `NPCSystem.Dialogue.Persistence` | `Dialogue/Persistence/` | `NPCDialogueSaveData`, `NPCDialogueProgressTracker` |
| `NPCSystem.Dialogue.RAG` | `Dialogue/RAG/` | `NPCRAGEmbeddingService`, `NPCRAGQueryEngine` |
| `NPCSystem.Dialogue.Session` | `Dialogue/Session/` | `NPCDialogueSessionService`, `NPCSessionContext` |
| `NPCSystem.Dialogue.UI` | `Dialogue/UI/` | `NPCDialogueUIController`, `NPCChoiceButton`, `NPCSubtitlePanel` |
| `NPCSystem.Initialization` | `Initialization/` | `NPCSceneInitializationController`, `NPCServiceLocator` |
| `NPCSystem.Items` | `Items/` | `NPCItemDefinition`, `NPCInventoryService`, `NPCItemTradeService` |
| `NPCSystem.LocalAI` | `LocalAI/` | `NPCLocalAIClient`, `NPCLocalAIConfig`, `NPCLocalAIHealthCheck` |
| `NPCSystem.Monitoring` | `Monitoring/Core/` | `NPCFlowLogger`, `NPCMetricsCollector`, `NPCTelemetrySink` |
| `NPCSystem.Monitoring.Datadog` | `Monitoring/Datadog/` | `NPCDatadogAPM`, `NPCDatadogTraceExporter` |
| `NPCSystem.Network.Bridges` | `Network/Bridges/` | `NPCDialogueNetworkBridge`, `NPCMatchmakingBridge` |
| `NPCSystem.Network.Core` | `Network/Core/` | `NPCNetworkBootstrap`, `NPCNetworkTransport`, `NPCNetworkSession` |

### asmdef Quick Reference

| asmdef | Scope | Depends On | Excluded From WebGL? |
|---|---|---|---|
| `NPCSystem.Runtime` | All gameplay code | Unity modules, Newtonsoft.Json | ❌ (always included) |
| `NPCSystem.Monitoring` | Monitoring only | Unity modules, Datadog SDK | ✅ (optional build flag) |

### Code Review Checklist

When reviewing a PR, check these namespace rules:

| Rule | Check | Auto-fix? |
|---|---|---|
| NAM01 | File belongs to the correct namespace for its folder path | `.codebaserules.yaml` rule NAM07 |
| NAM02 | No types in `GlobalNamespace` (empty namespace) | IDE quick action |
| NAM03 | `using` directives are explicit (no `using global::`) | Manual |
| NAM04 | Monitoring code doesn't reference `NPCSystem.Runtime` types | CI check |
| NAM05 | New domain → new namespace + new asmdef? | Architecture review |

---

> **NPC Senior Dev says:**
> *"Before namespaces: 'Hey Bob, where's the dialogue manager?' — 'Uh, check in Dialogue/ or maybe Core/ or maybe the file named NPCSystemManager.cs?' — 2 minutes of scrolling. After namespaces: 'NPCSystem.Dialogue.Core — done, found it in 2 seconds.' Bob spent those saved 118 seconds getting coffee. Bob is now my favorite coworker."*
