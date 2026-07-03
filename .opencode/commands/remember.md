---
description: Store a fact in long-term memory
---

Add this fact to `.opencode/memory/long-term.json`:

1. Read the current long-term.json
2. Append a new entry to the `facts` array with `id` (increment), `timestamp` (now), `fact` (the content from $ARGUMENTS), and `category` (ask me if not specified)
3. Write back to long-term.json
4. Confirm with the fact summary

$ARGUMENTS
