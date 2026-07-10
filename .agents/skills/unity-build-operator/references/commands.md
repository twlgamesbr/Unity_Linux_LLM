# Command and Artifact Reference

## Authority

- Editor version: `ProjectSettings/ProjectVersion.txt`
- Main scene: `Assets/Scenes/NPCDialoguePrototype1.unity`
- Build entry points: `Assets/Editor/NPCDialogueBuild.cs`
- Server profile: `Assets/Settings/Build Profiles/Linux Server.asset`
- WebGL profile: `Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset`
- Test assembly: `NPCSystem.Tests`

## Output Contract

| Command | Required output |
|---|---|
| `compile` | Exit 0 and no compiler-error marker in its log |
| `test` | A timestamped `Diagnostics/Logs/EditMode-test-results-*.xml` with no failed tests |
| `coverage` | Test XML plus generated coverage output under `Diagnostics/CodeCoverage/` |
| `audit` | A new report under `Diagnostics/ProjectAuditor/` |
| `build-server` | Executable `Builds/Server/NPCServer.x86_64` |
| `build-webgl` | `Builds/WebGL_client/WebGL/index.html` and Web Stripping Tool output in the build log |

Logs are timestamped in `Diagnostics/Logs/`. Use `logs test`, `logs build-server`, or `logs build-webgl` to inspect the newest matching run.

## Failure Triage

1. Confirm the failing command's log path from stdout.
2. Find the first compiler error, `BuildFailedException`, package error, licensing failure, or test failure. Later errors are often cascading.
3. For compilation errors, repair code and run `compile` before repeating tests or builds.
4. For test failures, run `test Full.Test.Name` to reduce iteration time.
5. For missing build artifacts with exit 0, treat the run as failed and inspect the build profile, output path, and build report log.
6. For license failures, verify Unity Hub licensing before changing project code.
7. For an Editor lock, close the interactive Editor. Never run two Editors against the same `Library`.
8. For a timeout, inspect the log before raising `UNITY_TIMEOUT_SECONDS`; a stalled import or modal licensing issue is not a slow build.

Unity exit codes are necessary but insufficient. Old artifacts can remain after a failed run, so always use the timestamped log and artifact check emitted by the same command.
