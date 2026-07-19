using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;



using NPCSystem.Monitoring;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Dialogue.Session
{
    /// <summary>
    /// Owns all RAG and retrieval logic — Qdrant search, local .rag index
    /// load/rebuild, embedder readiness polling, and index save.
    ///
    /// Created / initialized by NPCDialogueManager (Phase 2 extraction).
    /// </summary>
    [DefaultExecutionOrder(-1300)]
    public class NPCDialogueRetrievalService : MonoBehaviour
    {
        bool _useQdrantRag;
        QdrantRAGService _qdrantRag;
        string _remoteEmbeddingHost;
        int _remoteEmbeddingPort;

        static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

        public bool IsRagAvailable => _useQdrantRag && _qdrantRag != null;

        // ── Initialisation ───────────────────────────────────────────────

        /// <summary>
        /// Set all dependencies. Call once during NPCDialogueManager
        /// initialisation before any other method.
        /// </summary>
        public void Initialize(
            bool useQdrantRag,
            QdrantRAGService qdrantRag,
            string remoteEmbeddingHost,
            int remoteEmbeddingPort
        )
        {
            _useQdrantRag = useQdrantRag;
            _qdrantRag = qdrantRag;
            _remoteEmbeddingHost = remoteEmbeddingHost;
            _remoteEmbeddingPort = remoteEmbeddingPort;
        }

        /// <summary>
        /// Sync the embedder host/port on the LLM embedder.
        /// </summary>
        public void SyncEmbedderHost()
        {
            if (_useQdrantRag && _qdrantRag != null && _qdrantRag.Embedder != null)
            {
                _qdrantRag.Embedder.Host = _remoteEmbeddingHost;
                _qdrantRag.Embedder.Port = _remoteEmbeddingPort;
            }
        }

        // ── Search ───────────────────────────────────────────────────────

        /// <summary>
        /// Search for relevant knowledge across Qdrant hybrid search. 
        /// Returns the concatenated knowledge string, or <see cref="string.Empty"/> 
        /// when nothing is found.
        /// </summary>
        public async Task<string> SearchAsync(
            NPCProfile profile,
            string playerMessage,
            string reqId = null
        )
        {
            if (!_useQdrantRag || _qdrantRag == null || (profile != null && !profile.UseQdrantRag))
            {
                return string.Empty;
            }

            try
            {
                SyncEmbedderHost();
                
                string qdrantResult = await _qdrantRag.SearchMemoryAsync(
                    playerMessage,
                    Mathf.Max(1, profile != null ? profile.RagResults : 5),
                    reqId,
                    profile?.GetNpcSlug()
                );

                return qdrantResult ?? string.Empty;
            }
            catch (Exception e)
            {
                Logger.Log(
                    NPCFlowStage.ContextRetrieval,
                    NPCFlowStatus.Fallback,
                    NPCFlowLogLevel.Warning,
                    $"Qdrant search failed: {e.Message}",
                    source: nameof(NPCDialogueRetrievalService),
                    requestId: reqId,
                    npcSlug: profile?.GetNpcSlug(),
                    data: new Dictionary<string, object>
                    {
                        ["exceptionType"] = e.GetType().Name,
                        ["exceptionMessage"] = e.Message,
                        ["source"] = "Qdrant",
                    }
                );
                return string.Empty;
            }
        }

        // ── Index lifecycle (Legacy/Local RAG removed) ─────────────────────

        public Task LoadOrBuildIndexAsync(NPCProfile[] profiles) => Task.CompletedTask;
        public Task AddKnowledgeAsync(string npcName, string knowledgeText, NPCProfile[] profiles) => Task.CompletedTask;
        public void SaveIndex() { }

        // ── Internal helpers ─────────────────────────────────────────────

        static NPCProfile FindProfileInArray(string npcName, NPCProfile[] profiles)
        {
            return NPCProfile.FindProfileInArray(npcName, profiles);
        }
    }
}
