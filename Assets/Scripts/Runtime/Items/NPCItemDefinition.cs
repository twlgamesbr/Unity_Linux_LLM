using System;
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
    /// Defines a tradeable item that the Developer NPC can exchange with players.
    /// Items represent code snippets, documentation patterns, architecture knowledge,
    /// and optimization techniques found in the codebase.
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
        string _displayName = "Code Item";
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        [SerializeField, TextArea(1, 3)]
        string _description = "";
        public string Description => _description;

        [Header("Item Properties")]
        [SerializeField, Tooltip("AI-generated summary of the code or pattern this item represents")]
        [TextArea(3, 8)]
        string _codeSummary = "";
        public string CodeSummary => _codeSummary;

        [SerializeField]
        [Tooltip("The Qdrant collection this item's knowledge lives in (default: unity_linux_llm_codebase_v2)")]
        string _qdrantCollection = "unity_linux_llm_codebase_v2";
        public string QdrantCollection => string.IsNullOrWhiteSpace(_qdrantCollection)
            ? "unity_linux_llm_codebase_v2"
            : _qdrantCollection;

        [SerializeField, Tooltip("Qdrant point ID if referencing a specific search result")]
        string _qdrantPointId = "";
        public string QdrantPointId => _qdrantPointId;

        [Header("Trading")]
        [SerializeField]
        [Tooltip("0 = free, higher = rarer item")]
        int _tradeValue = 1;
        public int TradeValue => Mathf.Max(0, _tradeValue);

        [SerializeField, Tooltip("Items the player must have before this one can be traded")]
        string[] _requiredItemIds = Array.Empty<string>();
        public string[] RequiredItemIds => _requiredItemIds ?? Array.Empty<string>();

        [Header("Category")]
        [SerializeField]
        ItemCategory _category = ItemCategory.CodeSnippet;
        public ItemCategory Category => _category;

        [SerializeField, Tooltip("Tags for dynamic filtering (e.g. 'networking', 'animation', 'webgl')")]
        string[] _tags = Array.Empty<string>();
        public string[] Tags => _tags ?? Array.Empty<string>();

        public bool HasTag(string tag) =>
            !string.IsNullOrWhiteSpace(tag) && Tags.Any(t =>
                string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
    }

    public enum ItemCategory
    {
        CodeSnippet,
        DocumentationPattern,
        ArchitectureKnowledge,
        OptimizationTechnique,
        DebugTool,
        GameMechanic
    }
}
