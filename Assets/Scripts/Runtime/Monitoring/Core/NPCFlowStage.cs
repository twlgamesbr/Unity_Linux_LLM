namespace NPCSystem.Monitoring
{
    public enum NPCFlowStage
    {
        SceneBootstrap,
        ReferenceResolution,
        ConfigurationValidation,
        ProfileIndexBuild,
        HistoryLoad,
        HistoryRestore,
        NPCSwitch,
        UIInput,
        AuthRequest,
        AuthSession,
        RequestStart,
        ClientSession,
        OwnershipAuthority,
        RpcTraffic,
        DialogueRouting,
        ActionSelection,
        ActionExecution,
        GrammarOverride,
        GrammarRestore,
        ContextRetrieval,
        QdrantEmbedding,
        QdrantSearch,
        LocalRagReady,
        LocalRagSearch,
        PromptBuild,
        DialogueGeneration,
        BackendRequest,
        LLMChat,
        LLMStream,
        ResponseComplete,
        HistoryPersist,
        SmokeValidation,
        NetworkHost,
        PlayerSpawn,
        NpcSpawn,
        PlayerNameRegistration,
        EditorWorkflow,

        // ── Animation & Input ──────────────────────────────────────
        /// <summary>Animator snapshot submitted from owner client to server (20 Hz).</summary>
        AnimationSync,
        /// <summary>Server-side velocity fallback activated when owner RPC is late.</summary>
        AnimationFallback,
        /// <summary>Player input system switched between Gameplay and UI dialogue mode.</summary>
        InputModeSwitch,

        // ── WebGL Lifecycle ────────────────────────────────────────
        /// <summary>WebGLGameplayLoadController began or completed deferred scene load.</summary>
        WebGLGameplayLoad,

        // ── RAG Health ────────────────────────────────────────────
        /// <summary>Embedding dimension validated against Qdrant collection at startup.</summary>
        RagDimensionCheck,
    }
}
