---
description: Semantic search across long-term memory
---

Search memory for: $ARGUMENTS

1. Get an embedding: `bash .opencode/scripts/localai.sh embed "$ARGUMENTS"`
2. Compare against existing entries in `.opencode/memory/index.json` using cosine similarity
3. Also read `.opencode/memory/long-term.json` facts array for keyword matches
4. Return the top 3-5 most relevant facts

$ARGUMENTS
