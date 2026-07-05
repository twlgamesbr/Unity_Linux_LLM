# Developer Diagnostics

`run-editor-tests-with-coverage.sh`

- Runs Unity tests in batch mode with Code Coverage enabled.
- Writes coverage artifacts to `Diagnostics/CodeCoverage`.
- Writes Unity logs and test result XML to `Diagnostics/Logs`.
- Automatically invokes `generate-coverage-report.sh` when coverage data is present, turning the Unity coverage output into HTML/JSON/LCOV-style reports.

`generate-coverage-report.sh`

- Uses the repository-local ReportGenerator tool from `dotnet-tools.json`.
- Discovers coverage artifacts from either `CodeCoverage/` or `Diagnostics/CodeCoverage/`.
- Writes the final HTML report to `CodeCoverage/Report/index.html` or `Diagnostics/CodeCoverage/Report/index.html`.

Example:

```bash
./Tools/Diagnostics/run-editor-tests-with-coverage.sh EditMode
```

Then open the generated report in your browser from the output directory reported by the script.

`run-project-auditor.sh`

- Runs Project Auditor in batch mode through `NPCSystem.Editor.NPCDeveloperDiagnostics.RunProjectAuditorBatch`.
- Writes the Unity batch log to `Diagnostics/Logs/project-auditor.log`.
- Exports `.projectauditor` reports to `Diagnostics/ProjectAuditor`.

In the editor, use `NPC/Diagnostics/Developer Diagnostics` for the interactive window that opens Code Coverage, Profile Analyzer, Memory Profiler, records manual coverage, captures memory snapshots, and exports Project Auditor reports.
