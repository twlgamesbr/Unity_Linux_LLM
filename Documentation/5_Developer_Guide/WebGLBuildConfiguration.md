# WebGL Build Configuration

## Background

The WebGL build was crashing during Unity engine initialization with a WASM `index out of bounds` error at `wasm-function[6767]` — a null-pointer dereference in IL2CPP-generated virtual dispatch code. The crash occurred after engine subsystems initialized (graphics, physics, input, audio) but before any project-level managed code ran.

## Root Cause

The project included an embedded DOTS-based `com.unity.charactercontroller` package as a direct dependency (pulling in `com.unity.entities` transitively). The project's own code never used any DOTS APIs — it only used the built-in `UnityEngine.CharacterController` component. Under `ManagedStrippingLevel.High`, IL2CPP generated dispatch tables referencing DOTS types that the managed-code linker had stripped, producing a null-pointer crash in the generated WASM.

## Fix Applied

- Removed the unused embedded package (`Packages/com.unity.charactercontroller/`)
- Removed the manifest entry (`com.unity.charactercontroller: "1.4.2"`)
- Deleted sample scripts in `Assets/Samples/Character Controller/` that referenced DOTS types
- Removed `Unity.Entities` and `Unity.CharacterController` assembly preservation from `Assets/link.xml`

## Build Setting Improvements

Each setting below was evaluated during the debugging process. The current production values and their impact:

- **Managed Stripping → High** — Aggressively removes unused managed code before IL2CPP conversion. Reduces WASM binary size and download time. The crash was caused by incomplete linker preservation of transitive DOTS dependencies, not by stripping itself. Keeping High is safe now that the unused DOTS package is gone.

- **Wasm Code Optimization → DiskSizeLTO** — Enables link-time optimization for the WASM backend, producing the smallest possible WASM binary through cross-module inlining and dead-code elimination. Shrinks the `.wasm.br` file from ~12.6 MB (DiskSize without LTO) to ~7.7 MB.

- **Exception Support → None** — Disables IL2CPP exception-handling support code, which removes the C++ exception-handling tables and unwind metadata from the WASM binary. Reduces both `.wasm.br` size and initial download. This is safe because managed exceptions during startup are handled by the engine's own error paths; full stack traces are not needed in release builds.

- **Il2Cpp Code Generation → OptimizeSize** — Directs IL2CPP to favor binary size over raw throughput in its generated C++ code. Produces a smaller WASM module without meaningful runtime cost for the project's workload (dialogue UI, not real-time rendering).

- **Brotli Compression → On** — Compresses all build artifacts (`.wasm`, `.data`, `.framework.js`) with Brotli at maximum quality. Reduces over-the-wire size from ~30 MB uncompressed to ~12 MB compressed. The nginx server is configured to serve Brotli with correct `Content-Encoding: br` headers, and all modern browsers support Brotli decompression natively.

- **Data Caching → On** — Enables Unity's IndexedDB caching for the `.data` and `.wasm` files in the browser. On repeat visits, the browser loads from local cache instead of re-downloading, making subsequent loads near-instant. Controlled by `Application.unityVersion` — a new Unity version triggers a fresh download.

- **Debug Symbols → Off** — Strips DWARF debug information from the WASM binary. The original build had symbols enabled (for the diagnostic `FullWithStacktrace` build), which inflated the `.wasm.br` from ~7.7 MB to ~12.6 MB. Symbols are only useful for crash triage and should remain off in release builds.

- **Maximum Memory Size → 4096 MB** — Allows the WebAssembly memory heap to grow to 4 GB (the maximum WebGL supports). The dialogue system loads LLM inference context and NPC knowledge assets at runtime; sufficient headroom prevents out-of-memory crashes during gameplay. Keep the initial heap lower so startup does not reserve more memory than needed.

## Build Profiles

The project uses Unity 6 Build Profiles (`UnityEditor.Build.Profile.BuildProfile`) to manage per-target settings, stored under `Assets/Settings/Build Profiles/`:

| Profile | Target | Subtarget | Key Overrides |
|---|---|---|---|
| `WebGL - Desktop - Development` | WebGL | Player | Stripping High, CodeGen OptimizeSize, Compression Brotli, CodeOpt DiskSizeLTO, MaxMem 4096 |
| `Linux` | StandaloneLinux64 | Player | Stripping High, CodeGen OptimizeSize, IL2CPP |
| `Linux Server` | StandaloneLinux64 | Server | Stripping High, CodeGen OptimizeSize, IL2CPP, DedicatedServerOpt On |

Each profile carries its own PlayerSettings YAML, scene list, and quality/graphics settings. The build script (`NPCDialogueBuild.cs`) loads profiles by path and builds using `BuildPlayerWithProfileOptions`, eliminating the need for manual `PlayerSettings.Set*()` overrides.

To build via CLI with an explicit profile:
```bash
/path/to/Unity -quit -batchmode -nographics \
  -projectPath /path/to/project \
  -activeBuildProfile "Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset" \
  -executeMethod NPCDialogueBuild.PerformWebGLBuild
```

## Build Artifact Comparison

| Setting | `.wasm.br` | `.data.br` | Total | Status |
|---|---|---|---|---|
| Pre-fix (production) | 8.3 MB | 6.3 MB | 15 MB | Crashed |
| Diagnostic (stripping off, symbols on) | 12.6 MB | 5.2 MB | 19 MB | Loaded |
| Post-fix (production) | 7.7 MB | 5.2 MB | 12 MB | Loads |
