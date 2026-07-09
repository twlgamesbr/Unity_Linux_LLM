using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    /// <content>Network prefab reference resolution and registration extracted from NPCNetworkBootstrap.</content>
    /// <summary>
    /// Partial class providing the <see cref="PrefabRegistration"/> nested type,
    /// which allocates and tracks NetworkObject prefab slots for dynamic spawning.
    /// </summary>
    public partial class NPCNetworkBootstrap
    {
        public void ResolveReferences()
        {
            if (NetworkManager == null)
            {
                NetworkManager = GetComponent<NetworkManager>();
            }

            if (NetworkManager == null)
            {
                NetworkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (UnityTransport == null)
            {
                UnityTransport = GetComponent<UnityTransport>();
            }

            if (UnityTransport == null && NetworkManager != null)
            {
                UnityTransport = NetworkManager.GetComponent<UnityTransport>();
            }

            if (UnityTransport == null)
            {
                UnityTransport = FindAnyObjectByType<UnityTransport>(FindObjectsInactive.Include);
            }

            if (PlayerPrefab == null && !string.IsNullOrWhiteSpace(PlayerPrefabResourcesPath))
            {
                PlayerPrefab = Resources.Load<GameObject>(PlayerPrefabResourcesPath.Trim());
            }

            if (ServerNpcPrefab == null && !string.IsNullOrWhiteSpace(ServerNpcPrefabResourcesPath))
            {
                ServerNpcPrefab = Resources.Load<GameObject>(ServerNpcPrefabResourcesPath.Trim());
            }

            if (
                TransferableItemPrefab == null
                && !string.IsNullOrWhiteSpace(TransferableItemPrefabResourcesPath)
            )
            {
                TransferableItemPrefab = Resources.Load<GameObject>(
                    TransferableItemPrefabResourcesPath.Trim()
                );
            }
        }

        public void RegisterNetworkPrefabs()
        {
            if (NetworkManager == null)
            {
                return;
            }

            TryRegisterNetworkPrefab(PlayerPrefab, "player");
            TryRegisterNetworkPrefab(ServerNpcPrefab, "serverNpc");
            TryRegisterNetworkPrefab(TransferableItemPrefab, "transferableItem");
        }

        void TryRegisterNetworkPrefab(GameObject prefab, string label)
        {
            if (prefab == null || NetworkManager == null)
            {
                return;
            }

            if (IsPrefabAlreadyRegistered(prefab))
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Skipped,
                        NPCFlowLogLevel.Debug,
                        $"Skipped runtime registration for '{label}' because prefab '{prefab.name}' is already registered in NetworkConfig.",
                        source: nameof(NPCNetworkBootstrap),
                        data: new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["label"] = label,
                            ["prefab"] = prefab.name,
                        }
                    );
                return;
            }

            if (!prefab.TryGetComponent<NetworkObject>(out _))
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        $"Skipped network prefab registration for '{label}' because '{prefab.name}' has no NetworkObject.",
                        source: nameof(NPCNetworkBootstrap)
                    );
                return;
            }

            try
            {
                NetworkManager.PrefabHandler.AddNetworkPrefab(prefab);
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Debug,
                        $"Registered network prefab '{prefab.name}' for '{label}'.",
                        source: nameof(NPCNetworkBootstrap),
                        data: new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["label"] = label,
                            ["prefab"] = prefab.name,
                        }
                    );
            }
            catch (Exception ex)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Error,
                        $"Failed to register network prefab '{prefab.name}' for '{label}': {ex.Message}",
                        source: nameof(NPCNetworkBootstrap),
                        data: new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["label"] = label,
                            ["prefab"] = prefab.name,
                        }
                    );
                throw;
            }
        }

        bool IsPrefabAlreadyRegistered(GameObject prefab)
        {
            if (prefab == null || NetworkManager == null)
            {
                return false;
            }

            NetworkPrefabs prefabs = NetworkManager.NetworkConfig?.Prefabs;
            if (prefabs == null)
            {
                return false;
            }

            if (prefabs.Contains(prefab))
            {
                return true;
            }

            if (NetworkManager.NetworkConfig.PlayerPrefab == prefab)
            {
                return true;
            }

            for (int listIndex = 0; listIndex < prefabs.NetworkPrefabsLists.Count; listIndex++)
            {
                NetworkPrefabsList list = prefabs.NetworkPrefabsLists[listIndex];
                if (list == null)
                {
                    continue;
                }

                for (int prefabIndex = 0; prefabIndex < list.PrefabList.Count; prefabIndex++)
                {
                    NetworkPrefab networkPrefab = list.PrefabList[prefabIndex];
                    if (
                        networkPrefab != null
                        && (
                            networkPrefab.Prefab == prefab
                            || networkPrefab.SourcePrefabToOverride == prefab
                        )
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
