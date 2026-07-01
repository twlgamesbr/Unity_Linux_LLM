# Query Taxonomy

Map the user question type to the record types and ranking signals it needs.

## Structural Questions

Examples:

- `list all namespaces`
- `which asmdefs reference LLMUnity`
- `what files use NPCSystem`
- `which scripts reference LLMAgent`

Prefer:

- `namespace`
- `using_directive`
- `file_overview`
- `assembly`
- `relation`

## Ownership Questions

Examples:

- `where is LocalAI chat transport implemented`
- `what class owns Qdrant retrieval`
- `which manager controls NPC dialogue`

Prefer:

- `file_overview`
- `type`
- `member`
- `assembly`
- `runtime_summary`

## Behavior Questions

Examples:

- `how does the dialogue request reach LocalAI`
- `what happens before qdrant search`

Prefer:

- `member`
- `type`
- `relation`
- runtime `file_overview`
- runtime `runtime_summary`

## Scene/Integration Questions

Examples:

- `which scene objects use Qdrant`
- `which components must stay remote`

Prefer:

- GladeKit MCP scene hierarchy/component output
- scene audit output
- runtime classes
- `runtime_summary`
- selected structural records for ownership

## Ranking Rules

- Structural questions should not be answered from generic member hits alone.
- Runtime beats Editor when scores are close.
- Repetitive `using` hits from one file should be diversified.
- `file_overview` is often the best bridge between symbol precision and developer comprehension.
