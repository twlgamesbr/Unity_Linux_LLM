using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Netcode;
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
namespace NPCSystem.Items
{
    /// <summary>
    /// Server-authoritative service that processes NPC dialogue item-trade
    /// actions. Triggered by parsing <c>[give_item:id=xxx]</c> or
    /// <c>[trade_item:id=xxx,require=yyy]</c> tags in LLM responses.
    /// 
    /// Requires an ItemCatalog assigned at edit time.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ItemTradeService : NetworkBehaviour
    {
        [Header("Catalog")]
        [SerializeField]
        ItemCatalog _catalog;

        [Header("Logging")]
        [SerializeField]
        bool _verboseLogging = true;

        static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

        void Awake()
        {
            if (_catalog != null)
            {
                _catalog.BuildIndex();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (_catalog != null)
            {
                _catalog.BuildIndex();
            }
        }

        /// <summary>
        /// Check if the NPC can give an item to a player.
        /// </summary>
        public bool ServerCanGiveItem(ulong playerClientId, string itemId)
        {
            if (!IsServer)
                return false;

            if (_catalog == null)
            {
                LogWarning($"ItemCatalog not assigned — cannot fulfill trade for '{itemId}'.");
                return false;
            }

            NPCItemDefinition def = _catalog.FindItem(itemId);
            if (def == null)
            {
                LogWarning($"Unknown item '{itemId}' requested — not in catalog.");
                return false;
            }

            // Check prerequisites
            foreach (string requiredId in def.RequiredItemIds)
            {
                if (!PlayerHasItem(playerClientId, requiredId))
                {
                    LogWarning($"Player {playerClientId} lacks prerequisite '{requiredId}' for '{itemId}'.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Server-authoritative give-item flow. Validates prereqs, adds to player,
        /// creates world item if the definition has a physical prefab.
        /// Returns true on success.
        /// </summary>
        public bool ServerTryGiveItem(ulong playerClientId, string itemId)
        {
            if (!IsServer)
                return false;

            if (!ServerCanGiveItem(playerClientId, itemId))
                return false;

            NPCPlayerInventory inventory = FindPlayerInventory(playerClientId);
            if (inventory == null)
            {
                LogWarning($"Player {playerClientId} has no NPCPlayerInventory.");
                return false;
            }

            bool added = inventory.ServerTryAddItem(itemId);
            if (added)
            {
                NPCNetworkSessionManager sessionManager = FindAnyObjectByType<NPCNetworkSessionManager>(FindObjectsInactive.Include);
                sessionManager?.AddInventoryItem(playerClientId, itemId);

                LogInfo($"Gave item '{itemId}' to player {playerClientId}.");
            }
            else
            {
                LogWarning($"Failed to add item '{itemId}' to player {playerClientId} (duplicate?).");
            }

            return added;
        }

        /// <summary>
        /// Server-authoritative remove-item flow. Removes item from player inventory.
        /// </summary>
        public bool ServerTryRemoveItem(ulong playerClientId, string itemId)
        {
            if (!IsServer)
                return false;

            NPCPlayerInventory inventory = FindPlayerInventory(playerClientId);
            if (inventory == null)
                return false;

            bool removed = inventory.ServerTryRemoveItem(itemId);
            if (removed)
            {
                NPCNetworkSessionManager sessionManager = FindAnyObjectByType<NPCNetworkSessionManager>(FindObjectsInactive.Include);
                sessionManager?.RemoveInventoryItem(playerClientId, itemId);

                LogInfo($"Removed item '{itemId}' from player {playerClientId}.");
            }

            return removed;
        }

        /// <summary>
        /// Check if a player already has an item.
        /// </summary>
        public bool PlayerHasItem(ulong playerClientId, string itemId)
        {
            NPCPlayerInventory inventory = FindPlayerInventory(playerClientId);
            return inventory != null && inventory.ContainsItem(itemId);
        }

        /// <summary>
        /// Parse an LLM response for item-trade action tags and execute them.
        /// Returns the cleaned response text (tags stripped).
        /// Format: [give_item:id=item-slug] or [trade_item:id=item-slug,require=req-id]
        /// </summary>
        public string ProcessDialogueActions(string responseText, ulong playerClientId)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return responseText;

            string result = responseText;

            // Pattern: [give_item:id=code-snippet-optimization]
            var giveMatch = Regex.Match(result, @"\[give_item:id=([a-z0-9_-]+)\]");
            while (giveMatch.Success)
            {
                string itemId = giveMatch.Groups[1].Value;
                if (IsServer)
                {
                    ServerTryGiveItem(playerClientId, itemId);
                }
                result = result.Replace(giveMatch.Value, "");
                giveMatch = giveMatch.NextMatch();
            }

            // Pattern: [trade_item:id=item-slug,require=req-slug]
            var tradeMatch = Regex.Match(result, @"\[trade_item:id=([a-z0-9_-]+)(?:,require=([a-z0-9_-]+))?\]");
            while (tradeMatch.Success)
            {
                string itemId = tradeMatch.Groups[1].Value;
                string requireId = tradeMatch.Groups[2].Success ? tradeMatch.Groups[2].Value : null;

                if (IsServer)
                {
                    if (requireId != null && !PlayerHasItem(playerClientId, requireId))
                    {
                        LogWarning($"Trade '{itemId}' skipped — player lacks required '{requireId}'.");
                    }
                    else
                    {
                        ServerTryGiveItem(playerClientId, itemId);
                        if (requireId != null)
                        {
                            ServerTryRemoveItem(playerClientId, requireId);
                        }
                    }
                }

                result = result.Replace(tradeMatch.Value, "");
                tradeMatch = tradeMatch.NextMatch();
            }

            return result.Trim();
        }

        NPCPlayerInventory FindPlayerInventory(ulong clientId)
        {
            NPCPlayerNetworkAvatar[] avatars = FindObjectsByType<NPCPlayerNetworkAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var avatar in avatars)
            {
                if (avatar != null && avatar.IsSpawned && avatar.OwnerClientId == clientId)
                {
                    return avatar.GetComponent<NPCPlayerInventory>();
                }
            }
            return null;
        }

        void LogInfo(string msg)
        {
            if (_verboseLogging)
                Logger?.Log(NPCFlowStage.ResponseComplete, NPCFlowStatus.Success, NPCFlowLogLevel.Info, msg, source: nameof(ItemTradeService));
        }

        void LogWarning(string msg)
        {
            Logger?.Log(NPCFlowStage.ResponseComplete, NPCFlowStatus.Fallback, NPCFlowLogLevel.Warning, msg, source: nameof(ItemTradeService));
        }
    }
}
