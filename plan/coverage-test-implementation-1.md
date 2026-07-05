---
goal: Raise Unity coverage for high-impact runtime classes
version: 1.0
date_created: 2026-07-05
last_updated: 2026-07-05
owner: AI Assistant
status: 'Planned'
tags: ['coverage', 'testing', 'unity', 'runtime']
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

This plan raises code coverage for the highest-value runtime classes identified in the latest Unity coverage report. The focus is on dialogue flow, networking, persistence, and retrieval paths that are most likely to regress in this project.

## 1. Requirements & Constraints

- **REQ-001**: Increase line coverage for the highest-impact runtime classes in the NPCSystem.Runtime assembly.
- **REQ-002**: Add tests that verify behavior, fallback paths, and error handling instead of only trivial branches.
- **SEC-001**: Tests must avoid real network dependency and must not mutate the user’s real persistent data directory.
- **CON-001**: All work must fit the existing Unity edit-mode test assembly under Assets/Scripts/Tests/Editor.
- **GUD-001**: Follow the repository’s existing NUnit-style test conventions and use test-first development.
- **PAT-001**: Prefer small isolated unit tests for pure logic and integration-style tests only where Unity scene wiring is the real behavior under test.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Establish shared test scaffolding and fixtures for runtime classes.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create shared test helpers under Assets/Scripts/Tests/Editor/NPCTestHelpers.cs for temporary directories, temporary history files, and simple fake profile data. | | |
| TASK-002 | Add a failing test file for NPCHistoryStore covering empty files, malformed JSON, alternating roles, and odd-entry trimming. | | |
| TASK-003 | Add a failing test file for NPCDialogueManager covering profile selection, missing profile fallback, and initialization without required references. | | |

### Implementation Phase 2

- GOAL-002: Cover dialogue and networking error handling.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Add failing tests for NPCDialogueNetworkBridge covering empty player messages, missing NPC slugs, and client disconnect cleanup. | | |
| TASK-005 | Add failing tests for NPCDialogueNetworkBridge request relay and notebook-state update behavior using local event hooks. | | |
| TASK-006 | Add minimal production seams only if tests expose coupling to Unity engine APIs, such as extracting pure methods or allowing injected dependencies. | | |

### Implementation Phase 3

- GOAL-003: Extend coverage for LocalAI and retrieval classes.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Add failing tests for NPCLocalAIClient and NPCLocalAIEmbedder covering successful responses, invalid payloads, and request failure handling. | | |
| TASK-008 | Add failing tests for QdrantRAGService and NPCSearchMethod for empty-result handling and malformed search payloads. | | |
| TASK-009 | Add failing tests for NPCNetworkSessionManager covering session selection and clearing state after disconnects. | | |

### Implementation Phase 4

- GOAL-004: Measure and refine coverage.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Run the coverage script and inspect CodeCoverage/Report/Summary.md and CodeCoverage/Report/index.html. | | |
| TASK-011 | Re-run the most valuable tests and adjust the smallest production code paths needed to make them pass. | | |
| TASK-012 | Repeat until the highest-priority classes reach at least 60% line coverage or the remaining gaps are clearly due to integration-only behavior. | | |

## 3. Alternatives

- **ALT-001**: Focus first on UI-heavy classes; rejected because runtime dialogue and networking are more likely to regress and currently show lower coverage.
- **ALT-002**: Add broad smoke tests only; rejected because they would not target the specific branches that are still untested.

## 4. Dependencies

- **DEP-001**: Unity Test Framework and NUnit support already available in the repository.
- **DEP-002**: Coverage scripts in Tools/Diagnostics/run-editor-tests-with-coverage.sh and Tools/Diagnostics/generate-coverage-report.sh.
- **DEP-003**: Existing test assets under Assets/Scripts/Tests/Editor and runtime classes under Assets/Scripts/Runtime.

## 5. Files

- **FILE-001**: Assets/Scripts/Tests/Editor/NPCTestHelpers.cs
- **FILE-002**: Assets/Scripts/Tests/Editor/NPCHistoryStoreTests.cs
- **FILE-003**: Assets/Scripts/Tests/Editor/NPCDialogueManagerTests.cs
- **FILE-004**: Assets/Scripts/Tests/Editor/NPCDialogueNetworkingTests.cs
- **FILE-005**: Assets/Scripts/Runtime/NPCDialogue/NPCHistoryStore.cs
- **FILE-006**: Assets/Scripts/Runtime/Networking/NPCDialogueNetworkBridge.cs
- **FILE-007**: Assets/Scripts/Runtime/NPCDialogue/NPCLocalAIClient.cs
- **FILE-008**: Assets/Scripts/Runtime/NPCDialogue/NPCLocalAIEmbedder.cs
- **FILE-009**: Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs
- **FILE-010**: Assets/Scripts/Runtime/Networking/NPCNetworkSessionManager.cs

## 6. Testing

- **TEST-001**: Add unit tests for history normalization and file I/O behavior.
- **TEST-002**: Add unit tests for manager selection and fallback decision paths.
- **TEST-003**: Add unit tests for networking request validation and disconnect cleanup.
- **TEST-004**: Add unit tests for request and response parsing in the LocalAI and retrieval classes.
- **TEST-005**: Re-run coverage and confirm the report generation workflow succeeds.

## 7. Risks & Assumptions

- **RISK-001**: Some classes depend on Unity engine APIs that are difficult to instantiate directly in edit mode.
- **RISK-002**: Existing runtime code may need small seams to make pure logic testable without over-engineering.
- **ASSUMPTION-001**: The existing edit-mode test assembly can run under the current Unity setup and coverage scripts.
- **ASSUMPTION-002**: The latest coverage report remains the baseline for prioritization until new results are generated.

## 8. Related Specifications / Further Reading

- [CodeCoverage/Report/Summary.md](CodeCoverage/Report/Summary.md)
- [Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs](Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs)
- [Assets/Scripts/Runtime/NPCDialogue/NPCHistoryStore.cs](Assets/Scripts/Runtime/NPCDialogue/NPCHistoryStore.cs)
- [Assets/Scripts/Runtime/Networking/NPCDialogueNetworkBridge.cs](Assets/Scripts/Runtime/Networking/NPCDialogueNetworkBridge.cs)
- [Tools/Diagnostics/run-editor-tests-with-coverage.sh](Tools/Diagnostics/run-editor-tests-with-coverage.sh)
