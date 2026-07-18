using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Owns per-NPC conversation history — loading, saving, trimming, snapshots, and
    /// optional Supabase persistence. Created / initialized by NPCDialogueManager.
    ///
    /// This is the first extracted service from the original monolithic
    /// NPCDialogueManager (Phase 1 of the refactoring).
    /// </summary>
    [DefaultExecutionOrder(-1400)]
    public class NPCDialogueHistoryService : MonoBehaviour
    {
        SupabaseDialogueRepository _supabaseRepo;
        bool _persistHistory = true;
        int _maxHistoryPerNPC = 20;

        readonly Dictionary<string, List<DialogueEntry>> _historyByNpc =
            new Dictionary<string, List<DialogueEntry>>(StringComparer.OrdinalIgnoreCase);

        static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

        /// <summary>
        /// Initialise the service with dependencies from NPCDialogueManager.
        /// Call once during manager initialisation before any other methods.
        /// </summary>
        public void Initialize(
            SupabaseDialogueRepository supabaseRepo,
            bool persistHistory,
            int maxHistoryPerNPC
        )
        {
            _supabaseRepo = supabaseRepo;
            _persistHistory = persistHistory;
            _maxHistoryPerNPC = maxHistoryPerNPC;
        }

        // ────────────────────────────────────────────── Load ────

        /// <summary>
        /// Load persisted history for every profile. Clears any in-memory state first.
        /// Tries Supabase first (if configured), then falls back to local JSON files.
        /// </summary>
        public async Task LoadAllHistoriesAsync(NPCProfile[] profiles)
        {
            _historyByNpc.Clear();

            foreach (NPCProfile profile in profiles)
            {
                if (profile == null)
                    continue;

                string slug = profile.GetNpcSlug();

                if (!_persistHistory)
                {
                    _historyByNpc[slug] = new List<DialogueEntry>();
                    continue;
                }

                // Try Supabase first if configured and authenticated
                if (_supabaseRepo != null && _supabaseRepo.IsConfigured)
                {
                    List<DialogueEntry> supabaseHistory = await _supabaseRepo.LoadHistoryAsync(
                        slug
                    );
                    if (supabaseHistory != null)
                    {
                        _historyByNpc[slug] = supabaseHistory;
                        Logger.Log(
                            NPCFlowStage.HistoryLoad,
                            NPCFlowStatus.Success,
                            NPCFlowLogLevel.Debug,
                            $"Loaded {supabaseHistory.Count} turns from Supabase for NPC '{slug}'.",
                            source: nameof(NPCDialogueHistoryService)
                        );
                        continue;
                    }

                    Logger.Log(
                        NPCFlowStage.HistoryLoad,
                        NPCFlowStatus.Fallback,
                        NPCFlowLogLevel.Debug,
                        $"Supabase history unavailable for NPC '{slug}', falling back to local file.",
                        source: nameof(NPCDialogueHistoryService)
                    );
                }

                _historyByNpc[slug] = NPCHistoryStore.Load(profile.GetHistorySaveFile());
            }
        }

        // ────────────────────────────────────────────── Read ────

        /// <summary>
        /// Returns the raw (uncloned) history list for a given NPC slug.
        /// Used by the dialogue flow to build OpenAI message arrays.
        /// Returns an empty list if no history exists for that slug.
        /// </summary>
        public List<DialogueEntry> GetHistoryForSlug(string slug)
        {
            return _historyByNpc.TryGetValue(slug, out List<DialogueEntry> history)
                ? history
                : new List<DialogueEntry>();
        }

        /// <summary>
        /// Public read accessor — returns a cloned snapshot of the history
        /// for the given profile so callers cannot mutate internal state.
        /// </summary>
        public List<DialogueEntry> GetHistory(NPCProfile profile)
        {
            if (profile == null)
                return new List<DialogueEntry>();

            return CloneEntries(GetOrCreateHistory(profile));
        }

        // ────────────────────────────────────────────── Append ────

        /// <summary>
        /// Append a user/assistant turn pair to the history, persist locally
        /// (and to Supabase when configured), then trim to the max allowed size.
        /// </summary>
        public async Task AppendConversationAsync(
            NPCProfile profile,
            string playerMessage,
            string response
        )
        {
            if (profile == null)
                return;

            if (!_persistHistory)
                return;

            List<DialogueEntry> history = GetOrCreateHistory(profile);
            history.Add(new DialogueEntry("user", playerMessage));
            history.Add(new DialogueEntry("assistant", response));
            TrimHistory(history);
            NPCHistoryStore.Save(profile.GetHistorySaveFile(), history);

            // Also persist to Supabase if configured
            if (_supabaseRepo != null && _supabaseRepo.IsConfigured)
            {
                string slug = profile.GetNpcSlug();
                await _supabaseRepo.SaveTurnAsync(slug, "user", playerMessage);
                await _supabaseRepo.SaveTurnAsync(slug, "assistant", response);
            }
        }

        // ────────────────────────────────────────── Snapshot ────

        /// <summary>
        /// Capture a deep-cloned snapshot of every NPC's history.
        /// Used by NPCDialogueNetworkBridge to seed client sessions.
        /// </summary>
        public Dictionary<string, List<DialogueEntry>> CaptureHistorySnapshot(
            NPCProfile[] profiles
        )
        {
            var snapshot = new Dictionary<string, List<DialogueEntry>>(
                StringComparer.OrdinalIgnoreCase
            );

            foreach (NPCProfile profile in profiles)
            {
                if (profile == null)
                    continue;

                string slug = profile.GetNpcSlug();
                snapshot[slug] = CloneEntries(GetOrCreateHistory(profile));
            }

            return snapshot;
        }

        /// <summary>
        /// Replace the entire in-memory history with a previously-captured snapshot.
        /// Histories are normalised for chat-template compliance during application.
        /// </summary>
        public void ApplyHistorySnapshot(
            Dictionary<string, List<DialogueEntry>> historyByNpc,
            NPCProfile[] profiles
        )
        {
            _historyByNpc.Clear();

            foreach (NPCProfile profile in profiles)
            {
                if (profile == null)
                    continue;

                string slug = profile.GetNpcSlug();

                if (
                    historyByNpc != null
                    && historyByNpc.TryGetValue(slug, out List<DialogueEntry> history)
                )
                {
                    _historyByNpc[slug] = NPCHistoryStore.NormalizeForChatTemplate(
                        CloneEntries(history),
                        out _
                    );
                }
                else
                {
                    _historyByNpc[slug] = new List<DialogueEntry>();
                }
            }
        }

        // ────────────────────────────────────────────── Clear ────

        /// <summary>
        /// Clear history for one NPC (by slug/name) or for all profiles when
        /// npcName is null/empty. Removes both in-memory state and persisted files.
        /// </summary>
        public async Task ClearHistoryAsync(string npcName, NPCProfile[] profiles)
        {
            if (string.IsNullOrWhiteSpace(npcName))
            {
                foreach (NPCProfile profile in profiles)
                {
                    if (profile == null)
                        continue;

                    string slug = profile.GetNpcSlug();
                    _historyByNpc[slug] = new List<DialogueEntry>();
                    NPCHistoryStore.Delete(profile.GetHistorySaveFile());

                    if (_supabaseRepo != null)
                        await _supabaseRepo.DeleteHistoryAsync(slug);
                }
            }
            else
            {
                NPCProfile profile = FindProfileInArray(npcName, profiles);
                if (profile == null)
                    return;

                string slug = profile.GetNpcSlug();
                _historyByNpc[slug] = new List<DialogueEntry>();
                NPCHistoryStore.Delete(profile.GetHistorySaveFile());

                if (_supabaseRepo != null)
                    await _supabaseRepo.DeleteHistoryAsync(slug);
            }
        }

        // ────────────────────────────────────────── Internal ────

        List<DialogueEntry> GetOrCreateHistory(NPCProfile profile)
        {
            string slug = profile.GetNpcSlug();

            if (!_historyByNpc.TryGetValue(slug, out List<DialogueEntry> history))
            {
                history = new List<DialogueEntry>();
                _historyByNpc[slug] = history;
            }

            return history;
        }

        static List<DialogueEntry> CloneEntries(List<DialogueEntry> history)
        {
            var clone = new List<DialogueEntry>();

            foreach (DialogueEntry entry in history ?? new List<DialogueEntry>())
            {
                if (entry == null)
                    continue;

                clone.Add(
                    new DialogueEntry
                    {
                        Role = entry.Role,
                        Content = entry.Content,
                        TimestampUtc = entry.TimestampUtc,
                    }
                );
            }

            return clone;
        }

        void TrimHistory(List<DialogueEntry> history)
        {
            int maxEntries = Mathf.Max(1, _maxHistoryPerNPC) * 2;

            if (history.Count > maxEntries)
            {
                history.RemoveRange(0, history.Count - maxEntries);
            }
        }

        static NPCProfile FindProfileInArray(string npcName, NPCProfile[] profiles)
        {
            return NPCProfile.FindProfileInArray(npcName, profiles);
        }
    }
}
