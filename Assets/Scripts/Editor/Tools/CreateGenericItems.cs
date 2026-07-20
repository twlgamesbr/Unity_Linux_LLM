using System.Linq;
using UnityEditor;
using UnityEngine;
using NPCSystem.Items;

namespace NPCSystem.Editor.Tools
{
    /// <summary>
    /// Creates generic game item assets and populates the ItemCatalog on first
    /// domain reload after a Library rebuild.
    ///
    /// Items created:
    ///   - Health Potion   (Consumable, stack 10)
    ///   - Iron Ore        (Material,   stack 99)
    ///   - Silver Key      (Key,        stack 1)
    ///   - Wooden Sword    (Weapon,     stack 1)
    ///   - Gold Coin       (Currency,   stack 999)
    ///
    /// Also populates the Developer NPC profile's inventory with starting items.
    ///
    /// Manual trigger: Tools > Items > Create Generic Items
    /// </summary>
    [InitializeOnLoad]
    public static class CreateGenericItems
    {
        static readonly string ItemsFolder = "Assets/Resources/NPCItems";
        static readonly string CatalogPath = "Assets/Resources/ItemCatalog.asset";
        static readonly string ProfilePath = "Assets/Resources/NPCProfiles/DeveloperNPC.asset";

        static CreateGenericItems()
        {
            // Auto-run once per project after Library rebuild.
            // Uses EditorApplication.delayCall so AssetDatabase is ready.
            EditorApplication.delayCall += OnDelayCall;
        }

        static void OnDelayCall()
        {
            AutoCreateIfNeeded();
        }

        static void AutoCreateIfNeeded()
        {
            // Only run once per session
            if (SessionState.GetBool("GenericItemsCreated", false))
                return;

            // Only run if items don't already exist
            if (AssetDatabase.FindAssets("t:NPCItemDefinition").Length > 5)
            {
                SessionState.SetBool("GenericItemsCreated", true);
                return;
            }

            CreateAll();
            SessionState.SetBool("GenericItemsCreated", true);
        }

        [MenuItem("Tools/Items/Create Generic Items", false, 50)]
        public static void CreateAll()
        {
            EnsureFolder();

            // 1. Create item definition assets
            var healthPotion = CreateItem("health-potion", "Health Potion",
                "Restores 50 HP when consumed. A staple for any adventurer.",
                ItemCategory.Consumable, 10, 5, new[] { "healing", "consumable" });

            var ironOre = CreateItem("iron-ore", "Iron Ore",
                "A chunk of raw iron. Can be smelted into bars and forged into weapons.",
                ItemCategory.Material, 99, 1, new[] { "ore", "crafting", "material" });

            var silverKey = CreateItem("silver-key", "Silver Key",
                "A gleaming silver key. It looks like it opens something important.",
                ItemCategory.Key, 1, 0, new[] { "key", "quest" });

            var woodenSword = CreateItem("wooden-sword", "Wooden Sword",
                "A simple practice sword. Better than nothing in a fight.",
                ItemCategory.Weapon, 1, 10, new[] { "weapon", "melee", "starter" });

            var goldCoin = CreateItem("gold-coin", "Gold Coin",
                "A shiny gold coin. The universal currency of the realm.",
                ItemCategory.Currency, 999, 0, new[] { "currency", "trade" });

            Debug.Log($"[CreateGenericItems] Created {5} item definition(s).");

            // 2. Populate ItemCatalog
            var allItems = new[] { healthPotion, ironOre, silverKey, woodenSword, goldCoin };

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog != null)
            {
                SetSerializedArray(catalog, "_definitions", allItems);
                EditorUtility.SetDirty(catalog);
                Debug.Log($"[CreateGenericItems] Assigned {allItems.Length} items to ItemCatalog.");
            }
            else
            {
                Debug.LogError($"[CreateGenericItems] ItemCatalog not found at {CatalogPath}");
            }

            // 3. Populate DeveloperNPC profile inventory
            // Give the NPC a few starting items to offer
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            if (profile != null)
            {
                var profileType = profile.GetType();
                var field = profileType.GetField("_inventoryItems",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var startingItems = new NPCItemDefinition[] { healthPotion, ironOre, goldCoin };
                    field.SetValue(profile, startingItems);
                    EditorUtility.SetDirty(profile);
                    Debug.Log($"[CreateGenericItems] Assigned starting inventory to DeveloperNPC profile.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateGenericItems] Complete. All generic items created and wired.");
        }

        static NPCItemDefinition CreateItem(
            string itemId, string displayName, string description,
            ItemCategory category, int maxStackSize, int tradeValue,
            string[] tags)
        {
            var path = $"{ItemsFolder}/{itemId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NPCItemDefinition>(path);
            if (existing != null)
            {
                Debug.Log($"[CreateGenericItems] Item already exists, skipping: {path}");
                return existing;
            }

            var item = ScriptableObject.CreateInstance<NPCItemDefinition>();
            item.ItemId = itemId;
            item.name = displayName;

            // Use reflection to set private fields
            SetSerializedField(item, "_displayName", displayName);
            SetSerializedField(item, "_description", description);
            SetSerializedField(item, "_category", category);
            SetSerializedField(item, "_maxStackSize", maxStackSize);
            SetSerializedField(item, "_tradeValue", tradeValue);
            SetSerializedField(item, "_tags", tags);

            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        static void SetSerializedField(Object obj, string fieldName, object value)
        {
            var serialized = new SerializedObject(obj);
            var prop = serialized.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[CreateGenericItems] Field '{fieldName}' not found on {obj.name}");
                return;
            }

            switch (value)
            {
                case string s:
                    prop.stringValue = s;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case ItemCategory cat:
                    prop.enumValueIndex = (int)cat;
                    break;
                case string[] arr:
                    prop.ClearArray();
                    for (int i = 0; i < arr.Length; i++)
                    {
                        prop.InsertArrayElementAtIndex(i);
                        prop.GetArrayElementAtIndex(i).stringValue = arr[i];
                    }
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSerializedArray(Object obj, string fieldName, Object[] items)
        {
            var serialized = new SerializedObject(obj);
            var prop = serialized.FindProperty(fieldName);
            if (prop == null)
                return;

            prop.ClearArray();
            for (int i = 0; i < items.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/NPCItems"))
                AssetDatabase.CreateFolder("Assets/Resources", "NPCItems");
        }
    }
}
