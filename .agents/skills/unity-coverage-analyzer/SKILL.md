---
name: unity-coverage-analyzer
description: Use when reviewing Unity code coverage reports, interpreting the generated HTML/JSON summaries, and proposing targeted test or refactoring improvements for this project.
---

# Unity Coverage Analyzer

Use this skill when the task is to understand the current coverage state of the Unity project and turn that report into concrete, high-value recommendations.

## Goals

- Read coverage output from the generated report rather than relying on intuition.
- Focus on the most impactful gaps first: runtime-critical code, gameplay flows, networking, persistence, and editor automation that is likely to regress.
- Suggest specific tests or refactors that can raise coverage effectively without over-testing trivial code.

## Inputs to inspect first

Prefer these artifacts in order:

1. [CodeCoverage/Report/index.html](CodeCoverage/Report/index.html)
2. [CodeCoverage/Report/Summary.json](CodeCoverage/Report/Summary.json)
3. [CodeCoverage/Report/Summary.md](CodeCoverage/Report/Summary.md)
4. The relevant test assemblies under [Assets/Scripts/Tests](Assets/Scripts/Tests)

If the report is stale or missing, regenerate it with:

```bash
./Tools/Diagnostics/run-editor-tests-with-coverage.sh EditMode
```

## Analysis workflow

1. Start with the summary metrics
   - Line coverage
   - Method coverage
   - Assembly/module coverage
   - The most under-covered files or classes

2. Prioritize by impact
   - High-impact runtime files first: dialogue flow, networking, persistence, auth, scene wiring, and NPC runtime logic.
   - Secondary priority: editor utilities, diagnostics, and non-critical convenience code.
   - Treat coverage for core gameplay and regression-prone paths as more valuable than for one-off helpers.
   - For this repository, the first targets should be classes such as NPCDialogueManager, NPCDialogueNetworkBridge, NPCHistoryStore, NPCLocalAIClient, NPCLocalAIEmbedder, NPCSearchMethod, QdrantRAGService, and NPCNetworkSessionManager because they show very low coverage and sit on critical runtime paths.

3. Identify the likely gap type
   - Missing happy-path tests
   - Missing edge-case tests
   - Missing failure-path tests
   - Unreachable or overly coupled logic that should be refactored into smaller units

4. Recommend the smallest effective test plan
   - Unit tests for pure logic and data transforms
   - Integration tests for scene wiring or service orchestration
   - PlayMode tests for gameplay branches and UI interaction when relevant
   - Regression tests for issues already observed or recently changed

5. Suggest improvements beyond tests when appropriate
   - Extract logic into smaller pure methods that are easy to cover
   - Reduce branching in high-risk code paths
   - Separate I/O and platform-dependent behavior from decision logic
   - Introduce test seams for services and network boundaries

## Quality bar for recommendations

A good recommendation should include:

- the file or class to target
- the specific gap or behavior to cover
- the test type to add
- why it matters for the project
- a suggested next step that is small and actionable

## Example output style

When reporting findings, structure them like this:

- Target: [Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs](Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs)
- Gap: low coverage around fallback and error handling branches
- Suggested improvement: add unit tests for invalid history payloads, missing profile data, and remote server failure paths
- Why it matters: these paths are likely to break during runtime and are difficult to validate manually

For this repository, the analysis should also include a short priority ranking such as:

1. Highest priority: runtime dialogue, networking, and persistence classes with < 30% coverage
2. Medium priority: scene wiring and service orchestration classes with 30–60% coverage
3. Lower priority: UI-only or highly stable helper classes with already decent coverage

## Guardrails

- Do not recommend tests for code that is intentionally throwaway or trivial unless the risk justifies it.
- Prefer targeted coverage improvements over blanket “add more tests everywhere” advice.
- When the report shows low coverage in a file that is already well-tested indirectly, look for missing branches rather than duplicating tests.
- If a file is highly coupled to Unity engine APIs, suggest a seam or adapter to make it more testable.
