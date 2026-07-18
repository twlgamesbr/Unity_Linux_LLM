namespace NPCSystem
{
    /// <summary>
    /// Semantic category for NPCFlowEvent routing, filtering, and analysis.
    /// Maps to log file subdirectories and Datadog span service names.
    /// </summary>
    public enum NPCFlowCategory
    {
        Infrastructure,     // Bootstrap, config validation, connectivity
        Auth,               // Login, register, session restore
        Dialogue,           // Turn processing, message flow
        LLM,                // LocalAI HTTP requests, retries, streaming
        RAG,                // Embedding, Qdrant search, local RAG
        Network,            // NGO transport, RPC, multiplayer
        Memory,             // History load/save, Cognee
        UI,                 // Input actions, UI state, player interaction
        Monitoring,         // Datadog trace/metric lifecycle
        EditorWorkflow,     // Editor-only operations
    }
}
