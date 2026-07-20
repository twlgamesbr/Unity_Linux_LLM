using System;
using System.Linq;
using UnityEngine;

namespace NPCSystem.Items
{
    /// <summary>
    /// Defines a tradeable item that the Developer NPC can exchange with players.
    /// Supports generic game items (weapons, consumables, keys, materials, currency)
    /// alongside code-based knowledge items.
    /// </summary>
    [CreateAssetMenu(fileName = "NPCItem", menuName = "NPC Dialogue/Tradeable Item")]
    public class NPCItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        string _itemId = "";
        public string ItemId
        {
            get => string.IsNullOrWhiteSpace(_itemId) ? name : _itemId;
            set => _itemId = value;
        }

        [SerializeField]
        string _displayName = "Item";
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        [SerializeField, TextArea(1, 3)]
        string _description = "";
        public string Description => _description;

        [Header("Item Properties")]
        [SerializeField, Tooltip("Max items that can stack in one inventory slot (1 = unique/non-stackable)")]
        int _maxStackSize = 1;
        public int MaxStackSize => Mathf.Max(1, _maxStackSize);

        [SerializeField]
        ItemCategory _category = ItemCategory.Material;
        public ItemCategory Category => _category;

        [SerializeField, Tooltip("Tags for dynamic filtering (e.g. 'healing', 'key', 'weapon', 'rare')")]
        string[] _tags = Array.Empty<string>();
        public string[] Tags => _tags ?? Array.Empty<string>();

        [Header("Trading")]
        [SerializeField, Tooltip("0 = free, higher = rarer item")]
        int _tradeValue = 1;
        public int TradeValue => Mathf.Max(0, _tradeValue);

        [SerializeField, Tooltip("Items the player must have before this one can be traded")]
        string[] _requiredItemIds = Array.Empty<string>();
        public string[] RequiredItemIds => _requiredItemIds ?? Array.Empty<string>();

        // ── Code-specific fields (used only when Category is CodeSnippet etc.) ──

        [Header("Code Knowledge (code items only)")]
        [SerializeField, Tooltip("AI-generated summary of the code or pattern this item represents")]
        [TextArea(3, 8)]
        string _codeSummary = "";
        public string CodeSummary => _codeSummary;

        [SerializeField]
        [Tooltip("The Qdrant collection this item's knowledge lives in (default: unity_linux_llm_codebase_v2)")]
        string _qdrantCollection = "unity_linux_llm_codebase_v2";
        public string QdrantCollection =>
            string.IsNullOrWhiteSpace(_qdrantCollection) ? "unity_linux_llm_codebase_v2" : _qdrantCollection;

        [SerializeField, Tooltip("Qdrant point ID if referencing a specific search result")]
        string _qdrantPointId = "";
        public string QdrantPointId => _qdrantPointId;

        // ── Helpers ──

        public bool HasTag(string tag) =>
            !string.IsNullOrWhiteSpace(tag) && Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));

        public bool IsStackable => MaxStackSize > 1;

        public bool IsCodeItem =>
            Category == ItemCategory.CodeSnippet
            || Category == ItemCategory.DocumentationPattern
            || Category == ItemCategory.ArchitectureKnowledge
            || Category == ItemCategory.OptimizationTechnique;
    }

    /// <summary>
    /// Item categories covering both code knowledge and generic game items.
    /// </summary>
    public enum ItemCategory
    {
        // Code knowledge (existing)
        CodeSnippet,
        DocumentationPattern,
        ArchitectureKnowledge,
        OptimizationTechnique,
        DebugTool,
        GameMechanic,

        // Generic game items (new)
        Weapon,
        Consumable,
        Key,
        Material,
        Currency,
        QuestItem,
    }
}
