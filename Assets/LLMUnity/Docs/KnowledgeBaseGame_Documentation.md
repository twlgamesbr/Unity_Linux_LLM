**KnowledgeBaseGame Scene — LLM Integration Overview**

- **Scene file:** [Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGameScene.unity](Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGameScene.unity)
- **Primary sample script:** `Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGame.cs`
- **Key runtime scripts:**
  - `Assets/LLMUnity/Runtime/LLMClient.cs` — core LLM client abstraction (completion parameters, tokenization, embeddings, request lifecycle)
  - `Assets/LLMUnity/Runtime/LLMAgent.cs` — chat-oriented agent built on `LLMClient`, maintains history, overflow strategies, warmup
  - `Assets/LLMUnity/Runtime/RAG/RAG.cs` — retrieval-augmented generation wrapper (search, chunking, persistence)

**Important GameObjects / Components in the scene**

- `LLMAgent` (GameObject) — has a `LLMAgent` MonoBehaviour (GUID b4326d5ae3b03ff55847035351559f4e, fileID 1275496425)
  - Inspector values (from scene YAML): remote/local flags, host `localhost`, port `13333`, sampling params (temperature 0.2, topK 40, topP 0.9, etc.), overflowStrategy and overflowSummarizePrompt fields.
  - This is the runtime agent instance used by the sample UI to perform chat completions.

- `LLMRAG` (GameObject) — has an `LLM`/RAG-related component (GUID a50e3140c3ecaaf1c848dbf141cc2074, fileID 758599402)
  - Contains configuration for context size, batching, embeddingsOnly, etc. Used by `KnowledgeBaseGame` to create/load the vector store and to search for relevant chunks.

- `KnowledgeBaseGame` (GameObject) — sample controller (script: `KnowledgeBaseGame.cs`)
  - Inspector links (scene YAML): `llmAgent` -> fileID 1275496425, `rag` -> fileID 1400265393
  - Hooks UI elements: input field, submit/stop buttons, result text fields (`AIText`, `PlayerText`, `ButlerText`, etc.).
  - Key runtime flow:
    1. Start(): calls `InitRAG()` (loads embeddings/data) and `InitLLM()` (ensures `llmAgent` is assigned and warmed up).
    2. `OnInputFieldSubmit()` called on user submit: constructs a prompt via `ConstructPrompt(question)` which may call `rag.Search()` to fetch top-k relevant chunks and insert them into the prompt template.
    3. Calls `llmAgent.Chat(prompt, SetAIText, AIReplyComplete)` — streaming or async completion; `SetAIText` updates the UI with partial streaming tokens; `AIReplyComplete` finalizes the response and persists to history if enabled.
    4. `CancelRequests()` calls `llmAgent.CancelRequests()` to abort in-flight completions.

**How data flows at runtime**

- UI (InputField / Submit button) → `KnowledgeBaseGame.OnInputFieldSubmit()` → `ConstructPrompt()`
  - If RAG is enabled, `ConstructPrompt()` invokes `rag.Search(query, numResults)` to get the most relevant chunks.
  - The retrieved chunks are formatted and injected into the prompt (for example, as context or citation blocks).

- Prompt → `LLMAgent.Chat()`
  - `LLMAgent.Chat()` (wrapper) delegates to an underlying `UndreamAI.LlamaLib.LLMAgent`/`LLMClient` implementation (created in `SetupCallerObject()` inside `LLMClient`/`LLMAgent`).
  - `LLMClient.SetCompletionParameters()` builds a `JObject` with temperature, top_k, top_p, n_predict, stop tokens, streaming flags, and any model-specific fields, then passes it to the low-level LLM client.
  - The underlying LLM client sends the request to a local or remote LLM (configured by `LLM` component settings: host, port, local model file, etc.).

- Streaming & UI updates
  - The `LLMAgent` and `LLMClient` support streaming callbacks. Partial chunks are passed back to `SetAIText()` in `KnowledgeBaseGame` to update UI incrementally.
  - On completion, the sample stores the assistant message in `LLMAgent` history (if enabled) and may call `rag.Add()` to persist any generated embeddings or data.

**RAG lifecycle in sample**

- `InitRAG()` loads existing vector store files (if present) or calls `CreateEmbeddings()` which reads included `TextAsset` notes and chunks them using the configured `Chunking` strategy.
- `rag.Add()` persists chunk embeddings; `rag.Save()` writes the store to disk for faster subsequent loads.
- `rag.Search()` returns the top-N relevant chunks which `ConstructPrompt()` includes in the prompt.

**Where to change models or parameters**

- To change sampling/completion settings (temperature, topK, topP, n_predict), edit the `LLMAgent` component fields in the Inspector (scene) or change defaults in `Assets/LLMUnity/Runtime/LLMAgent.cs` / `LLMClient.cs`.
- To switch model or remote host/port, change the `LLM`/`LLMAgent` inspector fields (host, port, _model, local vs remote).
- To change RAG chunking/search behavior, update `Assets/LLMUnity/Runtime/RAG/*` classes or modify `KnowledgeBaseGame.CreateEmbeddings()` parameters.

**Files to inspect for deeper customization**

- [Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGame.cs](Assets/LLMUnity/Samples/KnowledgeBaseGame/KnowledgeBaseGame.cs#L1-L400)
- [Assets/LLMUnity/Runtime/LLMAgent.cs](Assets/LLMUnity/Runtime/LLMAgent.cs#L1-L400)
- [Assets/LLMUnity/Runtime/LLMClient.cs](Assets/LLMUnity/Runtime/LLMClient.cs#L1-L800)
- [Assets/LLMUnity/Runtime/RAG/RAG.cs](Assets/LLMUnity/Runtime/RAG/RAG.cs#L1-L400)

**Notes & caveats**

- The project depends on `Newtonsoft.Json` (used to build JSON `JObject` completion parameters). Ensure Unity package `com.unity.nuget.newtonsoft-json` or other Newtonsoft provider is installed and resolved in Unity's Package Manager before compiling.
- Scene YAML component `fileID`s and GUIDs map inspector instances to assets/scripts — the key mappings discovered:
  - `LLMAgent` MonoBehaviour GUID: `b4326d5ae3b03ff55847035351559f4e` (fileID 1275496425)
  - `LLMRAG` component GUID: `a50e3140c3ecaaf1c848dbf141cc2074` (fileID 758599402)
  - `KnowledgeBaseGame` GameObject references `llmAgent` (fileID 1275496425) and `rag` (fileID 1400265393)
- If you want a diagram or step-by-step labeled screenshots of the Inspector wiring, open the scene in the Unity Editor and I can extract exact Inspector values or create annotated screenshots.

---

If you want, I can now:
- Commit this document into the repo (it's saved at `Assets/LLMUnity/Docs/KnowledgeBaseGame_Documentation.md`).
- Generate a Mermaid diagram showing component interactions.
- Produce an annotated list mapping every UI element to its script field (full scene-to-field cross-reference).

Which next step would you like?"