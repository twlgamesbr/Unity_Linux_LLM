# Collection Strategy

Use separate collections when one parsing or ranking strategy would otherwise destabilize the default collection.

## Stable Collection

- Name:
  `unity_linux_llm_codebase_experiment_v1`
- Purpose:
  default project-wide collection for normal Unity coding queries

## Experimental Collection Types

## Structural

- Example:
  `unity_linux_llm_codebase_structural_v1`
- Use when:
  improving namespace/reference/asmdef/symbol ownership answers
- Emphasis:
  `namespace`, `using_directive`, `file_overview`, `assembly`, `relation`

## Hierarchy

- Example:
  `unity_linux_llm_codebase_hierarchy_v1`
- Use when:
  namespace-level and type-level summaries should outrank raw docs
- Emphasis:
  `namespace_summary`, `namespace`, `file_overview`, `assembly`, `relation`

## Runtime

- Example:
  `unity_linux_llm_codebase_runtime_v1`
- Use when:
  backend/runtime ownership and scene wiring should dominate Editor/docs
- Emphasis:
  Runtime `unity_region`, `runtime_summary`, transport classes, service classes, live scene wiring, active dialogue flow

## NPC Dialogue

- Example:
  `unity_linux_llm_codebase_npcdialogue_v1`
- Use when:
  testing retrieval for `NPCDialogueManager`, Qdrant, Cognee, and profile orchestration

## LLMUnity Core

- Example:
  `unity_linux_llm_codebase_llmunity_v1`
- Use when:
  retrieval should focus on `Assets/LLMUnity/Runtime` and transport implementation details

## Promotion Rules

- Do not promote by one prompt.
- Use the smoke set from `smoke-prompts.md`.
- Recreate the collection if point-ID semantics changed.
- Keep a note of:
  - parser changes
  - ranking changes
  - payload fields added
  - smoke-query winners and losers
