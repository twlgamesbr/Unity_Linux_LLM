# Smoke Prompts

Use these prompts to evaluate a parser/ranking/collection experiment.

## Structural

- `list namespaces used by NPCSystem`
- `which asmdefs own npc dialogue runtime`
- `which scripts reference LLMAgent`
- `list all namespaces and references in the project`

## Runtime/Backend

- `where is LocalAI chat transport implemented`
- `what classes handle qdrant rag`
- `which script manages embeddings for llm rag`
- `where is cognee memory service implemented`

## Acceptance

- Top results should contain the correct record types for the prompt class.
- Structural prompts should surface `namespace`, `file_overview`, `assembly`, or `relation` records near the top.
- Runtime prompts should still surface the real runtime owners, not editor helpers or docs.
- If an experiment helps one class of prompt but harms another, keep it in a separate collection.

