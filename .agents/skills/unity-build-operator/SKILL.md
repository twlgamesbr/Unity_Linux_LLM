---
name: unity-build-operator
description: Operate and repair the Unity_Linux_LLM Editor automation surface. Use when Codex needs to compile scripts, run EditMode tests or coverage, run Project Auditor, build the Linux dedicated server or WebGL client, inspect Unity batch logs, diagnose build failures, or validate and use Unity Web Stripping Tool submodule stripping in this project.
---

# Unity Build Operator

Drive this repository through its installed Unity Editor and checked-in build profiles. Treat a zero exit code, a clean log, and the expected artifact as separate required checks.

## Start Here

Run commands from the repository root:

```bash
.agents/skills/unity-build-operator/scripts/unity-project.sh doctor
.agents/skills/unity-build-operator/scripts/unity-project.sh test
.agents/skills/unity-build-operator/scripts/unity-project.sh build-server
.agents/skills/unity-build-operator/scripts/unity-project.sh build-webgl
```

Set `UNITY_EDITOR` only to override the Editor resolved from `ProjectSettings/ProjectVersion.txt`. Set `UNITY_PROJECT_ROOT` only when invoking the script from a copied skill.

## Required Workflow

1. Run `doctor` before the first Unity invocation in a turn.
2. If the Editor has this project open, use GladeKit MCP for live scene/component inspection and compilation. Close the Editor before batch commands; never remove `Library/UnityLockfile` while an Editor process owns it.
3. Run `compile`, then the narrowest relevant `test [filter]` command.
4. Run `build-server` or `build-webgl` through the checked-in Build Profile and `NPCDialogueBuild` entry point.
5. Inspect the emitted log and verify the output artifact. Do not infer success from source or an old `Builds/` directory.
6. For build failures, fix the first compiler or build error, then repeat the same command. Do not hide errors by weakening stripping or tests.

Use `build-all` only after `compile` and `test` pass. It intentionally builds sequentially because Unity projects cannot share one `Library` between concurrent Editor processes.

## Command Selection

- `doctor`: validate Editor/version, packages, profiles, scene, stripping settings, lock state, and disk space.
- `compile`: import the project and compile scripts without producing a player.
- `test [filter]`: run `NPCSystem.Tests` EditMode tests, optionally filtered by full or partial test name.
- `coverage`: run the project coverage entry point and generate artifacts under `Diagnostics/`.
- `audit`: run Project Auditor and save its report under `Diagnostics/ProjectAuditor/`.
- `build-server`: produce `Builds/Server/NPCServer.x86_64` from `Linux Server.asset`.
- `build-webgl`: produce `Builds/WebGL_client/WebGL/index.html` from the desktop WebGL profile and apply configured submodule stripping.
- `build-all`: run server then WebGL builds sequentially.
- `strip-status`: validate configured submodule names against the installed package definitions.
- `logs [name]`: show the newest log, or the newest log whose filename contains `name`.

Read [references/commands.md](references/commands.md) when diagnosing exit codes, logs, artifact paths, or stale results. Read [references/webgl-stripping.md](references/webgl-stripping.md) before changing WebGL stripping settings, profiling submodule use, or optimizing release builds.

## Repair Boundaries

- Preserve `Assets/Scenes/NPCDialoguePrototype1.unity` and use GladeKit for scene mutations.
- Preserve the Build Profile assets as the authority for target-specific Player Settings.
- Keep `WebGLStripPostBuild` as a configuration hook; Unity Web Stripping Tool owns debug-symbol preparation, compatibility settings, backup/restore, stripping, and build failure propagation.
- Do not delete build folders, package caches, `Library`, or user assets without explicit approval.
- Do not run dedicated-server and WebGL builds in parallel.
