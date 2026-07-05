# Developer Diagnostics

`run-editor-tests-with-coverage.sh`

- Runs Unity tests in batch mode with Code Coverage enabled.
- Writes coverage artifacts to `Diagnostics/CodeCoverage`.
- Writes Unity logs and test result XML to `Diagnostics/Logs`.

`run-project-auditor.sh`

- Runs Project Auditor in batch mode through `NPCSystem.Editor.NPCDeveloperDiagnostics.RunProjectAuditorBatch`.
- Writes the Unity batch log to `Diagnostics/Logs/project-auditor.log`.
- Exports `.projectauditor` reports to `Diagnostics/ProjectAuditor`.

In the editor, use `NPC/Diagnostics/Developer Diagnostics` for the interactive window that opens Code Coverage, Profile Analyzer, Memory Profiler, records manual coverage, captures memory snapshots, and exports Project Auditor reports.
