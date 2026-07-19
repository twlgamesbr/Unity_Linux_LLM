# Part 1 — Foundations

# Chapter 1: Assembly Definitions and Why They Matter

**Audience:** Junior Unity developers who know how to make scenes, write MonoBehaviours, and press play — but have never touched an `.asmdef` file.

**What you'll learn:** Why your project takes five seconds to recompile after changing one line of code, how to make it stop, and why your WebGL build will thank you.

---

## 1. The Problem — The Monolithic Assembly

When you create a new Unity project and start adding scripts to `Assets/Scripts/`, every single script — yours, the asset store package you bought, the tool from GitHub, that one-off editor script you wrote at 2 AM — gets compiled into **one giant assembly** called `Assembly-CSharp.dll`.

> 🧑‍💻 **Dev NPC:** "Ah yes, Assembly-CSharp — the `C:\Users\YourName\Desktop` of Unity compilation. Everything you own, tossed into one drawer, and God help you if you need to find anything."

### What that means in practice

**Full domain reload on every change.** Change one character in one file? Unity unloads the entire AppDomain, recompiles *every* script in the project, reloads all assemblies, re-initializes all serialized objects, and re-runs every `Awake()` and `OnEnable()` in your open scenes.

In our project, before we added asmdefs, that meant:

```
Changing: Assets/Scripts/Runtime/Dialogue/Core/NPCDialogueManager.cs
  → Compilation started... (0.8s)
  → Reloading domain... (2.1s)
  → Reloading scene objects... (1.4s)
  → Done: 4.3 seconds
```

Four seconds for a single-line comment change.

Over a day of active development, those pauses add up to **15–30 minutes of pure waiting**. And that's before we talk about the build.

### The IL2CPP / WebGL nightmare

IL2CPP is Unity's AOT (ahead-of-time) compiler that converts .NET IL to C++ before native compilation. It's required for **WebGL** (browsers don't run managed .NET code).

The problem: IL2CPP can't tell which code is actually *reachable* when everything lives in a single assembly. So it conservatively compiles and links **every class, every method, every property** in your project — even code that runs only in the Editor, or code behind feature flags you've never turned on.

> 🧑‍💻 **Dev NPC:** "Without asmdefs, IL2CPP treats your codebase like a Christmas shopper at Costco — *everything* goes in the cart. Your WASM bundle goes from 'reasonable' to 'did I just download a second Chrome?'"

**Real result from our project before asmdefs:**

| Metric | Before Asmdefs | After |
|---|---|---|
| Script compilation (code change) | 4.3 s | ~0.6 s |
| Domain reload on play | 3.1 s | 1.2 s |
| WebGL WASM size | 68 MB | 34 MB |
| WebGL browser memory (Chrome) | ~1.8 GB | ~850 MB |

The WASM heap limit in most browsers is around **2 GB** (hard browser-enforced cap). We were brushing up against it on the initial load. One more package, one more utility library, and the build would have been dead on arrival.

---

## 2. What Is an Assembly Definition?

An **Assembly Definition** (`.asmdef`) is a JSON file that tells Unity: *"Compile these scripts as their own .dll, separate from the default `Assembly-CSharp`."*

Think of your project as a building.

- **No asmdefs** = one giant open-plan office. Everybody hears everything. A sneeze in Accounting distracts Engineering.
- **Asmdefs** = walls between rooms. The Dialogue team has their room, the Network team has theirs, and the Monitoring team has a soundproof closet at the end of the hall.

Each `.asmdef` file marks a folder boundary. Unity creates one compilation unit (one `.dll`) for every asmdef, plus one for the remaining loose scripts.

> 🧑‍💻 **Dev NPC:** "Asmdefs are like zoning laws for your code. Without them, your project is a Wild West saloon where the bartender is also the sheriff, the piano player, and the horse."

### The `.asmdef` file

A `.asmdef` is a plain JSON file that looks like this (this is our actual `NPCSystem.Runtime.asmdef`):

```json
{
    "name": "NPCSystem.Runtime",
    "rootNamespace": "NPCSystem",
    "references": [
        "Unity.InputSystem",
        "Unity.Collections",
        "Unity.Netcode.Runtime",
        "Unity.DedicatedServer.MultiplayerRoles",
        "Unity.TextMeshPro",
        "EditorAttributes",
        "NPCSystem.Monitoring"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Newtonsoft.Json.dll",
        "Supabase.dll",
        "Supabase.Core.dll",
        "Supabase.Gotrue.dll",
        "Supabase.Postgrest.dll",
        "Supabase.Realtime.dll",
        "Supabase.Functions.dll",
        "Supabase.Storage.dll",
        "Microsoft.IdentityModel.Abstractions.dll",
        "Microsoft.IdentityModel.JsonWebTokens.dll",
        "Microsoft.IdentityModel.Logging.dll",
        "Microsoft.IdentityModel.Tokens.dll",
        "System.IdentityModel.Tokens.Jwt.dll",
        "MimeMapping.dll",
        "System.Reactive.dll",
        "Websocket.Client.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

We'll break every field down in §9 and §10. For now, just know this file lives at:

```
Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef
```

And it tells Unity: *"All scripts under `Assets/Scripts/Runtime/` — except the `Monitoring/Core/` subfolder — compile into `NPCSystem.Runtime.dll`."*

---

## 3. Step-by-Step: Creating an Asmdef

### Step 1: Create the file

In the Unity Editor Project window:

1. Right-click the folder where your scripts live (e.g., `Assets/Scripts/Runtime/`)
2. **Create → Assembly Definition**
3. A new file appears: `New Assembly Definition.asmdef`

> 🧑‍💻 **Dev NPC:** "Unity will auto-select the name for you. It'll be 'New Assembly Definition (1)' or something equally helpful. Rename it before your teammates see it in source control, please."

### Step 2: Name it

Rename to your project convention: **`NPCSystem.Runtime`**.

Unity naming conventions for asmdefs:

| Correct | Wrong |
|---|---|
| `NPCSystem.Runtime` | `NPC System Runtime` (spaces bad) |
| `MyCompany.MyProduct.Module` | `Module1` (too vague) |
| `NPCSystem.Monitoring` | `NPCSystem_Monitoring` (use dots) |

The name must be a valid C# identifier — no spaces, no special characters besides dots and underscores.

### Step 3: Set Assembly Definition References

Select the `.asmdef` file. In the Inspector, you'll see the **Assembly Definition References** list.

Click **+** to add references to other assemblies your code calls:

- `Unity.InputSystem` — because our NPC dialogue uses the Input System package
- `Unity.Netcode.Runtime` — because we have multiplayer bridges
- `Unity.TextMeshPro` — because our dialogue UI uses TMP text
- `EditorAttributes` — because we use inspector attributes throughout
- `NPCSystem.Monitoring` — because our runtime code logs telemetry events

> 🧑‍💻 **Dev NPC:** "Notice how you have to *explicitly* declare every dependency? Yes, it's tedious. Yes, that's the point. Every reference is a conscious decision, not an accident. Your future self, debugging a weird build, will thank you."

### Step 4: Add platform constraints (optional)

In the Inspector, you'll see **Platforms** settings:

- **Any Platform** (default): available everywhere
- **Exclude Platforms**: remove from specific targets (e.g., `WebGL` if the assembly uses `System.Net.Sockets` directly)
- **Include Platforms**: *only* available on these platforms

Our asmdefs use the default (any platform). The Dedicated Server package references handle platform-variant code internally.

### Step 5: Wait for compile — and notice the difference

Unity triggers a recompile when you create or modify an `.asmdef`. But future script changes will be **much faster**.

Time your next edit:

```
Before asmdefs:
  Change script → 4.3 s

After asmdefs:
  Change script in Runtime/ → 0.6 s
  Change script in Monitoring/ → 0.4 s
```

Why? Unity no longer needs to recompile `Assembly-CSharp.dll` — it only recompiles **the affected assembly** (or assemblies in the dependency chain). And since our assemblies are small and focused, that's fast.

> 🧑‍💻 **Dev NPC:** "That 4-second wait you used to fill by blinking? Gone. You're welcome. Now you have no excuse not to iterate."

---

## 4. Our Two-Asmdef Architecture

We run exactly **two** assembly definitions in the runtime layer:

### `NPCSystem.Runtime` — The Main Event

```
Path:  Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef
Covers: Everything in Runtime/ except Monitoring/Core/
Scope: ~12,000 lines of code across 15 domains
```

This is the workhorse. It contains all gameplay logic: dialogue, networking, characters, items, auth, LocalAI integration, RAG memory, initialization, and UI.

Its `rootNamespace` is `"NPCSystem"`, meaning script namespace suggestions start there.

### `NPCSystem.Monitoring` — The Soundproof Room

```
Path:  Assets/Scripts/Runtime/Monitoring/Core/NPCSystem.Monitoring.asmdef
Covers: Monitoring/Core/ and Monitoring/Datadog/
Scope: ~600 lines of code
```

This is our telemetry layer: structured logging, Datadog APM spans, flow-level analytics. It is **small, focused, and deliberately isolated**.

```json
{
    "name": "NPCSystem.Monitoring",
    "rootNamespace": "NPCSystem.Monitoring",
    "references": [
        "EditorAttributes",
        "UnityEngine.TestTools"
    ],
    "autoReferenced": true,
    ...
}
```

### Why only two?

This is the tension every project faces:

> 🧑‍💻 **Dev NPC:** "Every new asmdef is a new wall. Walls keep things clean. Too many walls and you can't find the bathroom. Two is usually the sweet spot for a project our size."

**The argument for many asmdefs:**
- Maximum isolation
- Fastest incremental compiles
- IL2CPP can tree-shake aggressively

**The argument for few asmdefs:**
- Less build system complexity
- Fewer circular dependency headaches
- Easier onboarding for new devs
- No cross-asmdef type duplication

**Our balance:** The Runtime assembly handles everything gameplay-related. The Monitoring assembly is separated because:
1. It depends on external SDKs (Datadog) that we don't want pulling into the main game assembly
2. Telemetry code is high-churn but low-risk — independent compile means faster iteration
3. If the Datadog SDK breaks a build (it has), gameplay code is unaffected

### The critical dependency direction

```
NPCSystem.Runtime ──depends-on──▶ NPCSystem.Monitoring
NPCSystem.Monitoring ──does-NOT-depend-on──▶ NPCSystem.Runtime
```

This is deliberate and important. Monitoring knows about *nothing* in the game. It has no reference to `NPCSystem.Dialogue.Core`, `NPCSystem.Auth`, or any gameplay type.

If you open `NPCSystem.Monitoring.asmdef` in the Inspector, the **Assembly Definition References** list contains only `EditorAttributes` and `UnityEngine.TestTools` — no reference to `NPCSystem.Runtime`.

This means:
- Changes in Runtime never force recompilation of Monitoring
- Monitoring can be pulled out, replaced, or deleted without touching gameplay code
- You can test monitoring independently

> 🧑‍💻 **Dev NPC:** "The dependency arrow points one way. Monitoring is a passive observer — like a Ring camera pointed at your codebase. It watches, it logs, but it doesn't need to know *why* the player pressed the talk button. It just records that they did."

---

## 5. The Circular Dependency Trap

### What happens

You have two asmdefs, `A` and `B`. Code in `A` references a type in `B`. Code in `B` references a type in `A`. You add the references in the Inspector and... boom:

```
Assembly with reference to 'A' would cause a circular dependency
because 'B' already references 'A'
```

> 🧑‍💻 **Dev NPC:** "Unity just told you 'no' in the most polite way possible. Circular dependencies are the compiler version of an infinite loop — except this one refuses to compile at all."

### Why it happens

Assembly definitions enforce **directed acyclic graphs** (DAGs). If `A` → `B` and `B` → `A`, the graph has a cycle, and the compiler can't determine what to compile first. This is the same reason you can't have two C# projects in a solution that reference each other.

### Our real-world example

During development, `NPCSystem.Monitoring` had a service called `SessionAnalyticsService` that tracked dialogue session analytics. It called into `NPCSystem.Dialogue.Core` to read session state.

But `NPCSystem.Runtime` (which contains `Dialogue.Core`) also called `SessionAnalyticsService` from Monitoring.

> 🧑‍💻 **Dev NPC:** "So let me get this straight — Monitoring talks to Runtime, and Runtime talks back to Monitoring? You just described a phone call where both people are talking at the same time. That's not a conversation, it's a noise complaint."

**The symptom:**
```
Attempting to add 'NPCSystem.Monitoring' to NPCSystem.Runtime's references
→ Assembly with reference to 'NPCSystem.Monitoring' would cause a
  circular dependency because 'NPCSystem.Monitoring' already references
  'NPCSystem.Runtime'
  → Wait — Monitoring doesn't reference Runtime...
```

Oh, actually — in our case Monitoring *never* referenced Runtime. The circular dependency was between Monitoring and a hypothetical third assembly. But the **conceptual** circular pattern was there: Monitoring needed data from Runtime, and Runtime needed Monitoring's analytics service.

### The fix

We moved `SessionAnalyticsService` out of `Monitoring/` into `Dialogue/Session/`. Now:

- `Dialogue.SessionAnalyticsService` lives in `NPCSystem.Dialogue.Session` namespace under `NPCSystem.Runtime`
- Monitoring receives analytics events via a **fire-and-forget interface** — it doesn't call back into Runtime
- Runtime calls into Monitoring through the one-way reference

> 🧑‍💻 **Dev NPC:** "We extracted the shared concern and put it where it belonged. The rule is simple: if two things need each other, they're probably the same thing. Find the common ground and put it somewhere neither depends on the other to reach."

**General strategies for fixing circular dependencies:**

| Strategy | When to use |
|---|---|
| **Move the shared type** into the consumer or a new third asmdef | Both assemblies genuinely need the same class |
| **Invert the dependency** via an interface | One side only needs an abstraction |
| **Use events / messaging** | The dependency is temporal (A triggers B, B needs data) |
| **Duplicate a small type** | It's a simple DTO/POCO and the third assembly is overkill |

---

## 6. Adding External References

Unity assemblies reference each other by name. Here's how to add them.

### In the Inspector

1. Select your `.asmdef` file
2. In the **Assembly Definition References** list, click **+**
3. Click the circle icon (object picker) or type the assembly name
4. Select the target `.asmdef` or predefined assembly from the picker

> 🧑‍💻 **Dev NPC:** "Pro tip: the object picker is tiny and easy to miss. Look for the little bullseye icon (◎). If you can't find it, type the assembly name manually — it works the same way."

### Common Unity assemblies we reference

| Assembly Name | What it provides | Why we need it |
|---|---|---|
| `Unity.InputSystem` | New input system (`PlayerInput`, `InputAction`) | Player movement & UI interaction |
| `Unity.Netcode.Runtime` | `NetworkBehaviour`, `NetworkVariable` | Multiplayer bridges & sync |
| `Unity.TextMeshPro` | `TMP_Text`, `TMP_InputField` | All dialogue UI |
| `Unity.Collections` | `NativeArray`, `NativeList` | Performance-critical network code |
| `EditorAttributes` | `[Foldout]`, `[Button]`, etc. | Inspector tooling across all domains |
| `UnityEngine.TestTools` | `[Test]`, `[UnityTest]` | Test assemblies (used by Monitoring) |
| `Unity.DedicatedServer.MultiplayerRoles` | Server role management | Dedicated server builds |

### Precompiled DLL references

When a reference is a third-party `.dll` (not a Unity package), you list it in **Precompiled References** — that's the `precompiledReferences` array in the JSON.

Our Runtime assembly precompiles references for:

```
Newtonsoft.Json.dll        — JSON serialization
Supabase*.dll              — Supabase client SDK (Auth, PostgREST, Realtime, Storage)
Microsoft.IdentityModel*   — JWT token handling
System.Reactive.dll        — Reactive streams for event pipelines
Websocket.Client.dll       — WebSocket client for real-time features
```

To add a precompiled reference:

1. Place the `.dll` in a folder like `Assets/Plugins/` or `Assets/Lib/`
2. In the asmdef Inspector, scroll to **Precompiled References**
3. Click **+** and type the `.dll` filename (e.g., `Newtonsoft.Json.dll`)
4. Unity will find it in the project

> 🧑‍💻 **Dev NPC:** "If you get a 'file not found' error on a precompiled reference, Unity is telling you the DLL isn't in any known path. Double-check your Plugins folder. We've all been there — the DLL is on your desktop, not in the project."

### Common pitfalls

| Pitfall | Symptom | Fix |
|---|---|---|
| **Missing reference** | `The type or namespace name 'X' could not be found` | Add the asmdef reference or precompiled DLL |
| **Typo in assembly name** | Same as above | Check spelling in the Inspector — `Unity.Netcode` is **not** the same as `Unity.Netcode.Runtime` |
| **Circular dependency** | `would cause a circular dependency` | Restructure (§5) |
| **Precompiled DLL not indexed** | DLL reference works in Editor but not in build | Check `overrideReferences` is `true` |
| **Platform stripping** | Type exists in Editor but missing in WebGL build | Add a `versionDefine` or check platform constraints |

---

## 7. Verification

How do you know your asmdefs are actually working?

### Method 1: Check the MonoScript Inspector

1. Select any `.cs` script file in the Project window
2. Look at the Inspector

You should see:

```
Assembly Information
  Assembly: NPCSystem.Runtime
```

Instead of:

```
Assembly Information
  Assembly: Assembly-CSharp
```

If it still says `Assembly-CSharp`, the script isn't covered by any asmdef. Either move it into a folder that has an asmdef, or create an asmdef for its current folder.

> 🧑‍💻 **Dev NPC:** "If you still see `Assembly-CSharp` after adding an asmdef, it means your script is in the wrong folder. The asmdef doesn't cast a magical force field — your script has to physically live inside the asmdef's folder tree."

### Method 2: Check the compiled DLLs

When Unity compiles, the resulting `.dll` files live in the `Library/ScriptAssemblies/` folder. You can open them in any .NET decompiler:

```
# From the project root:
ls Library/ScriptAssemblies/NPCSystem*.dll

Expected output:
  NPCSystem.Runtime.dll
  NPCSystem.Monitoring.dll
  # Assembly-CSharp.dll will still exist (for loose scripts in non-asmdef folders)
```

Open these in **ILSpy**, **dnSpy**, or **dotPeek** to verify the assembly contains exactly the types you expect — and nothing you don't.

### Method 3: The compile-time test

Make a change to a script in `Monitoring/Core/`. Observe the Console:

```
Compilation started at 10:32:15
  NPCSystem.Monitoring (1 script)
Compilation succeeded at 10:32:16
```

Note that only `NPCSystem.Monitoring` recompiled — not `NPCSystem.Runtime`, not `Assembly-CSharp`. That's the win in action.

---

## 8. WebGL-Specific — Why This Matters Most

WebGL is the harshest platform you will ever ship to. It's also why we invested in asmdefs early.

### IL2CPP and the WASM heap

Unity's WebGL backend uses **IL2CPP** to convert .NET IL to C++, then compiles that to WebAssembly (WASM) via Emscripten.

The WASM heap size is **hard-limited by the browser** — typically around **2 GB** on desktop Chrome, less on mobile (512 MB–1 GB).

> 🧑‍💻 **Dev NPC:** "The browser is not your gaming PC. It's a sandbox with a bedtime. WASM doesn't get virtual memory — what it allocates at startup is what it gets. Fill the heap, and the tab just... dies. No crash dialog, no stack trace. Just a sad gray page."

### How asmdefs save your build

Without asmdefs, IL2CPP sees a single `Assembly-CSharp.dll` with all types intermingled. It applies **conservative reachability analysis**:

- If class `A` references class `B`, and `B` is part of the same assembly, `B` is considered reachable.
- If any script anywhere uses `System.Linq`, the entire LINQ provider is linked.
- If you have an editor-only tool in your scripts folder, it's still compiled for WebGL.

**With asmdefs**, IL2CPP treats each assembly independently:

- `NPCSystem.Monitoring.dll` references `UnityEngine.TestTools`? That reference lives only in Monitoring's deps. If Monitoring isn't used in WebGL builds, TestTools doesn't ship.
- `Newtonsoft.Json` is only linked in `NPCSystem.Runtime.dll`. The tree-shaker can analyze *just the code paths* that Runtime actually uses.
- Editor-only scripts that are still in the project but under a different asmdef? They won't be compiled for WebGL at all.

### Real numbers from our project

| | Without asmdefs | With asmdefs |
|---|---|---|
| **Script compilation time** | ~4.3 s | ~0.6 s |
| **WebGL build time** | ~12 min | ~8 min |
| **WASM .wasm size** | 68 MB | 34 MB |
| **Browser memory (idle)** | ~1.8 GB | ~850 MB |
| **Mobile Safari (iPad)** | Crashed on load | Ran at 30 FPS |

> 🧑‍💻 **Dev NPC:** "The iPad test was the wake-up call. We loaded the build, the screen went white, and Safari cheerfully said 'A problem occurred with this webpage so it was reloaded.' That's browser-speak for 'your game is too fat.' Asmdefs slimmed us down by half. Half!"

### The golden rule

**If you're building for WebGL, asmdefs are not optional.** They are the difference between a build that loads and a build that dies in the browser's memory allocator.

Every script that doesn't need to be on WebGL should be behind an asmdef boundary. Every package dependency should be scoped to the assembly that uses it. The IL2CPP linker is smart — but only if you give it walls to work with.

---

## 9. Code Example — NPCSystem.Runtime.asmdef Annotated

Here's our actual `NPCSystem.Runtime.asmdef` with every field explained.

```json
{
    "name": "NPCSystem.Runtime",
    "rootNamespace": "NPCSystem",
    "references": [
        "Unity.InputSystem",
        "Unity.Collections",
        "Unity.Netcode.Runtime",
        "Unity.DedicatedServer.MultiplayerRoles",
        "Unity.TextMeshPro",
        "EditorAttributes",
        "NPCSystem.Monitoring"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Newtonsoft.Json.dll",
        "Supabase.dll",
        "Supabase.Core.dll",
        "Supabase.Gotrue.dll",
        "Supabase.Postgrest.dll",
        "Supabase.Realtime.dll",
        "Supabase.Functions.dll",
        "Supabase.Storage.dll",
        "Microsoft.IdentityModel.Abstractions.dll",
        "Microsoft.IdentityModel.JsonWebTokens.dll",
        "Microsoft.IdentityModel.Logging.dll",
        "Microsoft.IdentityModel.Tokens.dll",
        "System.IdentityModel.Tokens.Jwt.dll",
        "MimeMapping.dll",
        "System.Reactive.dll",
        "Websocket.Client.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Field-by-field breakdown

| Field | Our value | Why |
|---|---|---|
| `name` | `"NPCSystem.Runtime"` | The assembly name. This is what other asmdefs use to reference us. Must be unique. |
| `rootNamespace` | `"NPCSystem"` | When you create a new script in this asmdef's folder, Unity auto-suggests `NPCSystem.` as the namespace prefix. Empty string means no auto-suggestion. |
| `references` | `[...7 assemblies...]` | Other assemblies this code explicitly depends on. Each name must match another asmdef's `name` field exactly, or be a predefined Unity assembly. |
| `includePlatforms` | `[]` (empty = all) | If non-empty, *only* compile for these platforms. We use empty because our Runtime code targets all platforms. |
| `excludePlatforms` | `[]` (empty = none) | Skip compilation on these platforms. Empty = compile everywhere. |
| `allowUnsafeCode` | `false` | Allow `unsafe` blocks. We don't need them in our gameplay code. |
| `overrideReferences` | `true` | **This is important.** When `true`, Unity ignores the default `Assembly-CSharp` references and uses *only* what we list in `references` + `precompiledReferences`. We must set this to `true` because we use precompiled DLLs (Supabase, Newtonsoft). |
| `precompiledReferences` | `[...16 DLLs...]` | Third-party `.dll` files that are *not* distributed as Unity packages. These live in `Assets/Plugins/` or similar. Only used when `overrideReferences` is `true`. |
| `autoReferenced` | `true` | If `true`, other assemblies can reference us automatically without explicitly listing us. Safer to leave `true` for root assemblies. Set `false` for test or editor assemblies that shouldn't leak into player builds. |
| `defineConstraints` | `[]` | Custom defines that control whether this assembly compiles. Empty = always compile. Can use symbols like `UNITY_SERVER` or `NPC_USE_DATADOG`. |
| `versionDefines` | `[]` | Version-specific defines based on package versions. E.g., define `NEW_NETCODE` only when `com.unity.netcode.gameobjects` ≥ 2.0.0. |
| `noEngineReferences` | `false` | If `true`, the assembly won't auto-reference `UnityEngine.dll` or `UnityEngine.CoreModule.dll`. Useful for pure C# libraries. We keep it `false` because, well, it's a Unity game. |

---

## 10. Quick Reference Table

| Asmdef field | What it means | What we use |
|---|---|---|
| `name` | Unique assembly name (C# identifier rules) | `NPCSystem.Runtime`, `NPCSystem.Monitoring` |
| `rootNamespace` | Auto-suggested namespace prefix for new scripts | `NPCSystem` (Runtime), `NPCSystem.Monitoring` (Monitoring) |
| `references` | Explicit dependency list — assemblies your code calls | 7 assemblies for Runtime, 2 for Monitoring |
| `includePlatforms` | Whitelist: *only* compile for these platforms | `[]` (all platforms) |
| `excludePlatforms` | Blacklist: skip these platforms | `[]` (none excluded) |
| `allowUnsafeCode` | Enable C# `unsafe` blocks | `false` |
| `overrideReferences` | When `true`, ignore default assembly set and use only what you declare | `true` for Runtime (needs precompiled DLLs), `false` for Monitoring |
| `precompiledReferences` | Third-party `.dll` files to include (requires `overrideReferences: true`) | 16 DLLs (Supabase client, Newtonsoft, JWT, etc.) |
| `autoReferenced` | Allow other assemblies to reference this one without explicit listing | `true` (both assemblies) |
| `defineConstraints` | Conditional compilation symbols that gate this entire assembly | `[]` (always compile) |
| `versionDefines` | Per-package-version defines for conditional code | `[]` (not currently used) |
| `noEngineReferences` | Strip `UnityEngine.dll` auto-reference | `false` (we use Unity APIs) |

---

## Epilogue — The WebGL Build That Lived

> 🧑‍💻 **Dev NPC:** "I still remember the day we added asmdefs. We were two weeks from a milestone demo, the WebGL build was 68 megs, and it crashed on every browser except a fully-updated Chrome on a gaming PC. We spent an afternoon splitting the project into two assemblies — just two — and the build dropped to 34 megs. The iPad ran it. The CEO's MBA-specced laptop ran it. And the only code change was a JSON file with some square brackets.
>
> "One file. Half the size. Zero gameplay changes.
>
> "That's the thing about asmdefs. They don't make your code better. They make your *project* better. They're the cheapest performance optimization you will ever do — a JSON file that tells Unity where the walls are.
>
> "Now go create an Assembly Definition. Your WebGL build is waiting."

---

*Next: Part 1, Chapter 2 — Namespace Organization and Folder Conventions*
