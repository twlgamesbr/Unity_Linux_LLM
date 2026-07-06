using System.Collections.Generic;
using EditorAttributes;
using Unity.Netcode;
using UnityEngine;

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
        public NetworkManager networkManager;
        public GameObject itemPrefab;
        public string itemPrefabResourcesPath = "Networking/NPCTransferableItem";
        public string initialNpcSlug = "butler";
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
            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (itemPrefab == null && !string.IsNullOrWhiteSpace(itemPrefabResourcesPath))
            {
                itemPrefab = Resources.Load<GameObject>(itemPrefabResourcesPath.Trim());
            }

            initialNpcSlug = string.IsNullOrWhiteSpace(initialNpcSlug)
                ? "butler"
                : initialNpcSlug.Trim().ToLowerInvariant();
        }

        void RegisterCallbacks()
        {
            if (_callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            _callbacksRegistered = true;
        }

        void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted -= HandleServerStarted;
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

            if (networkManager == null || !networkManager.IsServer)
            {
                lastSpawnStatus =
                    "Transferable item spawn skipped because this instance is not the server.";
                return;
            }

            if (itemPrefab == null)
            {
                lastSpawnStatus =
                    $"Transferable item prefab could not be loaded from Resources/{itemPrefabResourcesPath}.";
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
                itemPrefab,
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
                if (npc != null && npc.Slug == initialNpcSlug)
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
                    data: new Dictionary<string, object> { ["initialNpcSlug"] = initialNpcSlug }
                );
        }
    }
}
