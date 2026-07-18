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
        [SerializeField]
        NetworkManager _networkManager;
        public NetworkManager NetworkManager { get => _networkManager; set => _networkManager = value; }

        [FormerlySerializedAs("ItemPrefab")]
        [SerializeField]
        GameObject _itemPrefab;
        public GameObject ItemPrefab { get => _itemPrefab; set => _itemPrefab = value; }

        [FormerlySerializedAs("ItemPrefabResourcesPath")]
        [SerializeField]
        string _itemPrefabResourcesPath = "Networking/NPCTransferableItem";
        public string ItemPrefabResourcesPath { get => _itemPrefabResourcesPath; set => _itemPrefabResourcesPath = value; }

        [FormerlySerializedAs("InitialNpcSlug")]
        [SerializeField]
        string _initialNpcSlug = "butler";
        public string InitialNpcSlug { get => _initialNpcSlug; set => _initialNpcSlug = value; }

        [FormerlySerializedAs("fallbackWorldPosition")]
        [SerializeField]
        Vector3 _fallbackWorldPosition = new Vector3(0f, 1f, 4f);
        public Vector3 FallbackWorldPosition { get => _fallbackWorldPosition; set => _fallbackWorldPosition = value; }

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
            if (_networkManager == null)
            {
                _networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (_itemPrefab == null && !string.IsNullOrWhiteSpace(_itemPrefabResourcesPath))
            {
                _itemPrefab = Resources.Load<GameObject>(_itemPrefabResourcesPath.Trim());
            }

            _initialNpcSlug = string.IsNullOrWhiteSpace(_initialNpcSlug)
                ? "butler"
                : _initialNpcSlug.Trim().ToLowerInvariant();
        }

        void RegisterCallbacks()
        {
            if (_callbacksRegistered || _networkManager == null)
            {
                return;
            }

            _networkManager.OnServerStarted += HandleServerStarted;
            _callbacksRegistered = true;
        }

        void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || _networkManager == null)
            {
                return;
            }

            _networkManager.OnServerStarted -= HandleServerStarted;
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

            if (_networkManager == null || !_networkManager.IsServer)
            {
                lastSpawnStatus =
                    "Transferable item spawn skipped because this instance is not the server.";
                return;
            }

            if (_itemPrefab == null)
            {
                lastSpawnStatus =
                    $"Transferable item prefab could not be loaded from Resources/{_itemPrefabResourcesPath}.";
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
                _itemPrefab,
                _fallbackWorldPosition,
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
                if (npc != null && npc.Slug == _initialNpcSlug)
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
                    item.PlaceInWorld(_fallbackWorldPosition);
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
                    data: new Dictionary<string, object> { ["InitialNpcSlug"] = _initialNpcSlug }
                );
        }
    }
}
