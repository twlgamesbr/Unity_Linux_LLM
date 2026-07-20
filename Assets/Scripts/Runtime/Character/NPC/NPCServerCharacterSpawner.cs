using System.Collections.Generic;
using EditorAttributes;
using NPCSystem.Auth;
using NPCSystem.Character.NPC;
using NPCSystem.Character.Player;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Initialization;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Monitoring;
using NPCSystem.Network.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace NPCSystem.Character.NPC
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
        [SerializeField]
        NetworkManager _networkManager;
        public NetworkManager NetworkManager
        {
            get => _networkManager;
            set => _networkManager = value;
        }

        [FormerlySerializedAs("NetworkBootstrap")]
        [SerializeField]
        NPCNetworkBootstrap _networkBootstrap;
        public NPCNetworkBootstrap NetworkBootstrap
        {
            get => _networkBootstrap;
            set => _networkBootstrap = value;
        }

        [FormerlySerializedAs("DialogueManager")]
        [SerializeField]
        NPCDialogueManager _dialogueManager;
        public NPCDialogueManager DialogueManager
        {
            get => _dialogueManager;
            set => _dialogueManager = value;
        }

        [FormerlySerializedAs("NpcPrefab")]
        [SerializeField]
        GameObject _npcPrefab;
        public GameObject NpcPrefab
        {
            get => _npcPrefab;
            set => _npcPrefab = value;
        }

        [FormerlySerializedAs("NpcPrefabResourcesPath")]
        [SerializeField]
        string _npcPrefabResourcesPath = "Networking/NPCServerCharacter";
        public string NpcPrefabResourcesPath
        {
            get => _npcPrefabResourcesPath;
            set => _npcPrefabResourcesPath = value;
        }

        [Header("Spawn Layout")]
        [FormerlySerializedAs("spawnOrigin")]
        [SerializeField]
        Vector3 _spawnOrigin = new Vector3(-4f, 0f, 6f);
        public Vector3 SpawnOrigin
        {
            get => _spawnOrigin;
            set => _spawnOrigin = value;
        }

        [FormerlySerializedAs("spawnSpacing")]
        [SerializeField]
        Vector3 _spawnSpacing = new Vector3(2.75f, 0f, 2.5f);
        public Vector3 SpawnSpacing
        {
            get => _spawnSpacing;
            set => _spawnSpacing = value;
        }

        [FormerlySerializedAs("MaxColumns")]
        [SerializeField]
        int _maxColumns = 3;
        public int MaxColumns
        {
            get => _maxColumns;
            set => _maxColumns = value;
        }

        [FormerlySerializedAs("ClearExistingNpcCharactersBeforeSpawn")]
        [SerializeField]
        bool _clearExistingNpcCharactersBeforeSpawn = true;
        public bool ClearExistingNpcCharactersBeforeSpawn
        {
            get => _clearExistingNpcCharactersBeforeSpawn;
            set => _clearExistingNpcCharactersBeforeSpawn = value;
        }

        [FormerlySerializedAs("SnapToGround")]
        [SerializeField]
        [Tooltip(
            "Raycast down to snap spawned NPCs to ground level. Disable if using a NavMesh with automatic ground placement."
        )]
        bool _snapToGround = true;
        public bool SnapToGround
        {
            get => _snapToGround;
            set => _snapToGround = value;
        }

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

            if (_networkManager == null || !_networkManager.IsServer)
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

            if (_npcPrefab == null)
            {
                lastSpawnStatus = $"NPC prefab could not be loaded from Resources/{_npcPrefabResourcesPath}.";
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
                _dialogueManager == null ? System.Array.Empty<NPCProfile>() : _dialogueManager.Profiles;
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

            if (_clearExistingNpcCharactersBeforeSpawn)
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

                GameObject instance = Instantiate(_npcPrefab, GetSpawnPosition(i), Quaternion.identity);
                if (!instance.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
                {
                    Destroy(instance);
                    lastSpawnStatus = $"Spawned prefab '{_npcPrefab.name}' is missing a NetworkObject.";
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

                // Snap to ground via raycast so NPCs don't float
                if (_snapToGround)
                {
                    SnapToGroundLevel(instance);
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
            if (_networkManager == null)
            {
                _networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (_networkBootstrap == null)
            {
                _networkBootstrap = FindAnyObjectByType<NPCNetworkBootstrap>(FindObjectsInactive.Include);
            }

            if (_dialogueManager == null)
            {
                _dialogueManager = FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
            }

            if (_npcPrefab == null && !string.IsNullOrWhiteSpace(_npcPrefabResourcesPath))
            {
                _npcPrefab = Resources.Load<GameObject>(_npcPrefabResourcesPath.Trim());
            }
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
            SpawnServerNpcCharacters();
        }

        void ClearExistingNpcCharacters()
        {
            NPCServerCharacter[] characters = FindObjectsByType<NPCServerCharacter>(FindObjectsInactive.Include);
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

        /// <summary>
        /// Raycast from the transform position downward to find ground level.
        /// If the ground is found, places the object on it.
        /// </summary>
        void SnapToGroundLevel(GameObject instance)
        {
            Vector3 origin = instance.transform.position;
            float maxDist = 20f;
            LayerMask groundMask = LayerMask.GetMask("Default");

            if (Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, maxDist, groundMask))
            {
                Vector3 pos = instance.transform.position;
                pos.y = hit.point.y;
                instance.transform.position = pos;

                NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(pos);
                }
            }
        }

        Vector3 GetSpawnPosition(int index)
        {
            int columnCount = Mathf.Max(1, _maxColumns);
            int row = index / columnCount;
            int column = index % columnCount;
            return _spawnOrigin + new Vector3(_spawnSpacing.x * column, 0f, _spawnSpacing.z * row);
        }
    }
}
