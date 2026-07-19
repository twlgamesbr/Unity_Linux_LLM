using System.Collections.Generic;
using System.Linq;
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
    /// Global registry mapping item IDs to NPCItemDefinition assets.
    /// Loaded via Resources.Load at initialization. Provides lookup
    /// for the ItemTradeService and dialogue action parser.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "NPC Dialogue/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField]
        NPCItemDefinition[] _definitions = new NPCItemDefinition[0];

        readonly Dictionary<string, NPCItemDefinition> _index = new Dictionary<string, NPCItemDefinition>();

        public NPCItemDefinition[] AllDefinitions => _definitions;

        /// <summary>
        /// Pre-build the lookup index. Call once during initialization.
        /// </summary>
        public void BuildIndex()
        {
            _index.Clear();
            foreach (var def in _definitions)
            {
                if (def == null) continue;
                string id = def.ItemId.ToLowerInvariant().Trim();
                if (!string.IsNullOrWhiteSpace(id) && !_index.ContainsKey(id))
                {
                    _index[id] = def;
                }
            }
        }

        /// <summary>
        /// Find an item definition by its itemId (case-insensitive).
        /// </summary>
        public NPCItemDefinition FindItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;
            string key = itemId.ToLowerInvariant().Trim();
            return _index.TryGetValue(key, out var def) ? def : null;
        }

        /// <summary>
        /// Check if an item ID exists in the catalog.
        /// </summary>
        public bool HasItem(string itemId) => FindItem(itemId) != null;

        /// <summary>
        /// Find all items matching a specific tag.
        /// </summary>
        public NPCItemDefinition[] FindByTag(string tag)
        {
            return _definitions
                .Where(d => d != null && d.HasTag(tag))
                .ToArray();
        }

        /// <summary>
        /// Find all items of a given category.
        /// </summary>
        public NPCItemDefinition[] FindByCategory(ItemCategory category)
        {
            return _definitions
                .Where(d => d != null && d.Category == category)
                .ToArray();
        }

        void OnEnable()
        {
            BuildIndex();
        }
    }
}
