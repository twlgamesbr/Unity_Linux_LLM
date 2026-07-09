using System.Collections.Generic;
using EditorAttributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DisallowMultipleComponent]
    public sealed class NPCServerCharacterSpawner : MonoBehaviour
    {
        [Title("NPC Server Character Spawner")]
        [HelpBox(
            "Server-authoritative spawner for visible NPC network characters. Registers the prefab on all instances via NPCNetworkBootstrap, then the server instantiates one spawned character per configured NPC profile.",
            MessageMode.Log,
            drawAbove: true
        )]
        [Header("References")]
        [FormerlySerializedAs("NetworkManager")]
        public NetworkManager NetworkManager;
        [FormerlySerializedAs("NetworkBootstrap")]
        public NPCNetworkBootstrap NetworkBootstrap;
        [FormerlySerializedAs("DialogueManager")]
        public NPCDialogueManager DialogueManager;
        [FormerlySerializedAs("NpcPrefab")]
        public GameObject NpcPrefab;
        [FormerlySerializedAs("NpcPrefabResourcesPath")]
        public string NpcPrefabResourcesPath = "Networking/NPCServerCharacter";

        [Header("Spawn Layout")]
        public Vector3 spawnOrigin = new Vector3(-4f, 1f, 6f);
        public Vector3 spawnSpacing = new Vector3(2.75f, 0f, 2.5f);
        public int maxColumns = 3;
        public bool clearExistingNpcCharactersBeforeSpawn = true;

        [Header("Diagnostics")]
        [SerializeField, ReadOnly]
        string lastSpawnStatus = "Idle";

        [SerializeField, ReadOnly]
        int lastSpawnedCount;

        bool _callbacksRegistered;

        [ShowInInspector]
        string PrefabPreview => NpcPrefab != null ? NpcPrefab.name : NpcPrefabResourcesPath;

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

        [Button("Spawn Server NPC Characters")]
        public void SpawnServerNpcCharacters()
        {
            ResolveReferences();

            if (NetworkManager == null || !NetworkManager.IsServer)
            {
                lastSpawnStatus = "Spawn skipped because this instance is not the server.";
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.NpcSpawn,
                        NPCFlowStatus.Skipped,
                        NPCFlowLogLevel.Warning,
                        lastSpawnStatus,
                        source: nameof(NPCServerCharacterSpawner)
                    );
                return;
            }

            if (NpcPrefab == null)
            {
                lastSpawnStatus =
                    $"NPC prefab could not be loaded from Resources/{NpcPrefabResourcesPath}.";
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.NpcSpawn,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        lastSpawnStatus,
                        source: nameof(NPCServerCharacterSpawner)
                    );
                return;
            }

            NPCProfile[] profiles =
                DialogueManager == null
                    ? System.Array.Empty<NPCProfile>()
                    : DialogueManager.Profiles;
            if (profiles.Length == 0)
            {
                lastSpawnStatus = "No NPC profiles are configured on the dialogue manager.";
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.NpcSpawn,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        lastSpawnStatus,
                        source: nameof(NPCServerCharacterSpawner)
                    );
                return;
            }

            if (clearExistingNpcCharactersBeforeSpawn)
            {
                ClearExistingNpcCharacters();
            }

            lastSpawnedCount = 0;
            for (int i = 0; i < profiles.Length; i++)
            {
                NPCProfile profile = profiles[i];
                if (profile == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(
                    NpcPrefab,
                    GetSpawnPosition(i),
                    Quaternion.identity
                );
                if (!instance.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
                {
                    Destroy(instance);
                    lastSpawnStatus =
                        $"Spawned prefab '{NpcPrefab.name}' is missing a NetworkObject.";
                    NPCFlowLogger
                        .FindOrCreate()
                        ?.Log(
                            NPCFlowStage.NpcSpawn,
                            NPCFlowStatus.Error,
                            NPCFlowLogLevel.Error,
                            lastSpawnStatus,
                            source: nameof(NPCServerCharacterSpawner)
                        );
                    return;
                }

                NPCServerCharacter character = instance.GetComponent<NPCServerCharacter>();
                networkObject.Spawn(destroyWithScene: true);
                if (character != null)
                {
                    character.InitializeIdentity(profile);
                }

                lastSpawnedCount++;
            }

            lastSpawnStatus = $"Spawned {lastSpawnedCount} server NPC character(s).";
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.NpcSpawn,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    lastSpawnStatus,
                    source: nameof(NPCServerCharacterSpawner),
                    data: new Dictionary<string, object> { ["spawnedCount"] = lastSpawnedCount }
                );
        }

        void ResolveReferences()
        {
            if (NetworkManager == null)
            {
                NetworkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (NetworkBootstrap == null)
            {
                NetworkBootstrap = FindAnyObjectByType<NPCNetworkBootstrap>(
                    FindObjectsInactive.Include
                );
            }

            if (DialogueManager == null)
            {
                DialogueManager = FindAnyObjectByType<NPCDialogueManager>(
                    FindObjectsInactive.Include
                );
            }

            if (NpcPrefab == null && !string.IsNullOrWhiteSpace(NpcPrefabResourcesPath))
            {
                NpcPrefab = Resources.Load<GameObject>(NpcPrefabResourcesPath.Trim());
            }
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
            SpawnServerNpcCharacters();
        }

        void ClearExistingNpcCharacters()
        {
            NPCServerCharacter[] characters = FindObjectsByType<NPCServerCharacter>(
                FindObjectsInactive.Include
            );
            foreach (NPCServerCharacter character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                NetworkObject networkObject = character.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                    continue;
                }

                Destroy(character.gameObject);
            }
        }

        Vector3 GetSpawnPosition(int index)
        {
            int columnCount = Mathf.Max(1, maxColumns);
            int row = index / columnCount;
            int column = index % columnCount;
            return spawnOrigin + new Vector3(spawnSpacing.x * column, 0f, spawnSpacing.z * row);
        }
    }
}
