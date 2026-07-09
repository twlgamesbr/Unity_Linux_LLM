using System.Collections.Generic;
using EditorAttributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DisallowMultipleComponent]
    public sealed class NPCTransferableItemSpawner : MonoBehaviour
    {
        [Title("NPC Transferable Item Spawner")]
        [HelpBox(
            "Spawns a single testable network item for ownership-transfer validation. The item starts on the configured NPC, can be picked up by a player with Interact, given to the nearest player with Previous (1), and returned to the nearest NPC with Next (2).",
            MessageMode.Log,
            drawAbove: true
        )]
        [FormerlySerializedAs("NetworkManager")]
        public NetworkManager NetworkManager;
        [FormerlySerializedAs("ItemPrefab")]
        public GameObject ItemPrefab;
        [FormerlySerializedAs("ItemPrefabResourcesPath")]
        public string ItemPrefabResourcesPath = "Networking/NPCTransferableItem";
        [FormerlySerializedAs("InitialNpcSlug")]
        public string InitialNpcSlug = "butler";
        public Vector3 fallbackWorldPosition = new Vector3(0f, 1f, 4f);

        [SerializeField, ReadOnly]
        string lastSpawnStatus = "Idle";

        bool _callbacksRegistered;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            RegisterCallbacks();
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResolveReferences();
            }
        }

        void OnDestroy()
        {
            UnregisterCallbacks();
        }

        void ResolveReferences()
        {
            if (NetworkManager == null)
            {
                NetworkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (ItemPrefab == null && !string.IsNullOrWhiteSpace(ItemPrefabResourcesPath))
            {
                ItemPrefab = Resources.Load<GameObject>(ItemPrefabResourcesPath.Trim());
            }

            InitialNpcSlug = string.IsNullOrWhiteSpace(InitialNpcSlug)
                ? "butler"
                : InitialNpcSlug.Trim().ToLowerInvariant();
        }

        void RegisterCallbacks()
        {
            if (_callbacksRegistered || NetworkManager == null)
            {
                return;
            }

            NetworkManager.OnServerStarted += HandleServerStarted;
            _callbacksRegistered = true;
        }

        void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || NetworkManager == null)
            {
                return;
            }

            NetworkManager.OnServerStarted -= HandleServerStarted;
            _callbacksRegistered = false;
        }

        void HandleServerStarted()
        {
            SpawnTestItem();
        }

        [Button("Spawn Transferable Test Item")]
        public void SpawnTestItem()
        {
            ResolveReferences();

            if (NetworkManager == null || !NetworkManager.IsServer)
            {
                lastSpawnStatus =
                    "Transferable item spawn skipped because this instance is not the server.";
                return;
            }

            if (ItemPrefab == null)
            {
                lastSpawnStatus =
                    $"Transferable item prefab could not be loaded from Resources/{ItemPrefabResourcesPath}.";
                return;
            }

            NPCTransferableItem[] existingItems = FindObjectsByType<NPCTransferableItem>(
                FindObjectsInactive.Include
            );
            foreach (NPCTransferableItem existing in existingItems)
            {
                if (existing == null)
                {
                    continue;
                }

                NetworkObject existingNetworkObject = existing.GetComponent<NetworkObject>();
                if (existingNetworkObject != null && existingNetworkObject.IsSpawned)
                {
                    existingNetworkObject.Despawn(true);
                }
                else
                {
                    Destroy(existing.gameObject);
                }
            }

            GameObject instance = Instantiate(
                ItemPrefab,
                fallbackWorldPosition,
                Quaternion.identity
            );
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            NPCTransferableItem item = instance.GetComponent<NPCTransferableItem>();
            networkObject.Spawn(destroyWithScene: true);

            NPCServerCharacter[] npcs = FindObjectsByType<NPCServerCharacter>(
                FindObjectsInactive.Include
            );
            NPCServerCharacter initialNpc = null;
            foreach (NPCServerCharacter npc in npcs)
            {
                if (npc != null && npc.Slug == InitialNpcSlug)
                {
                    initialNpc = npc;
                    break;
                }
            }

            if (item != null)
            {
                if (initialNpc != null)
                {
                    item.AssignToNpc(initialNpc.Slug);
                    lastSpawnStatus = $"Spawned transferable item on NPC '{initialNpc.Slug}'.";
                }
                else
                {
                    item.PlaceInWorld(fallbackWorldPosition);
                    lastSpawnStatus =
                        "Spawned transferable item in fallback world position because the configured NPC was not found.";
                }
            }

            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.NpcSpawn,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    lastSpawnStatus,
                    source: nameof(NPCTransferableItemSpawner),
                    data: new Dictionary<string, object> { ["InitialNpcSlug"] = InitialNpcSlug }
                );
        }
    }
}
