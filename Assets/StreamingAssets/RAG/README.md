# NPC Dialogue RAG Indexes

This folder stores generated LLMUnity RAG indexes for the NPC dialogue prototype.

Current index:

- `NPCDialogues-minilm-chunked.rag`

Each active `.rag` file should have a matching `.json` metadata sidecar with:

- embedding model
- embedding length
- chunk size
- source knowledge files
- source file hashes
- build time
- importer version

Do not reuse an old `.rag` file after changing the embedding model, embedding length, chunk size, or knowledge source files. Rebuild to a fresh filename or let `NPCDialogueManager` rebuild when metadata is stale.

The old `NPCDialogues.rag` index was removed because it contained invalid vectors from earlier whole-document embedding attempts.
