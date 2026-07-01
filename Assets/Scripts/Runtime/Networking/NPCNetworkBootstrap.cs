using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(-2500)]
    [DisallowMultipleComponent]
    public class NPCNetworkBootstrap : MonoBehaviour
    {
        [Header("References")]
        public NetworkManager networkManager;
        public UnityTransport unityTransport;
        public GameObject playerPrefab;
        public string playerPrefabResourcesPath = "Networking/NPCPlayerAvatar";

        [Header("Transport")]
        public NPCTransportConfig transportConfig = default;
        public bool configureOnAwake = true;
        public bool autoStartInPlayMode = false;

        void Reset()
        {
            transportConfig = NPCTransportConfig.CreateDefault();
            playerPrefabResourcesPath = "Networking/NPCPlayerAvatar";
            ResolveReferences();
        }

        void Awake()
        {
            if (transportConfig.port == 0)
            {
                transportConfig = NPCTransportConfig.CreateDefault();
            }

            ResolveReferences();

            if (configureOnAwake)
            {
                ApplyTransportConfiguration();
            }

            if (autoStartInPlayMode && Application.isPlaying)
            {
                StartConfiguredMode();
            }
        }

        void OnValidate()
        {
            if (transportConfig.port == 0)
            {
                transportConfig = NPCTransportConfig.CreateDefault();
            }

            if (string.IsNullOrWhiteSpace(playerPrefabResourcesPath))
            {
                playerPrefabResourcesPath = "Networking/NPCPlayerAvatar";
            }

            transportConfig.NormalizeInPlace();

            if (!Application.isPlaying)
            {
                ResolveReferences();
            }
        }

        public void ResolveReferences()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
            }

            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (unityTransport == null)
            {
                unityTransport = GetComponent<UnityTransport>();
            }

            if (unityTransport == null && networkManager != null)
            {
                unityTransport = networkManager.GetComponent<UnityTransport>();
            }

            if (unityTransport == null)
            {
                unityTransport = FindAnyObjectByType<UnityTransport>(FindObjectsInactive.Include);
            }

            if (playerPrefab == null && !string.IsNullOrWhiteSpace(playerPrefabResourcesPath))
            {
                playerPrefab = Resources.Load<GameObject>(playerPrefabResourcesPath.Trim());
            }
        }

        public void ApplyTransportConfiguration()
        {
            ResolveReferences();

            if (unityTransport == null)
            {
                Debug.LogError("NPCNetworkBootstrap could not find a UnityTransport component.", this);
                return;
            }

            if (networkManager != null)
            {
                networkManager.NetworkConfig.NetworkTransport = unityTransport;
                if (playerPrefab != null)
                {
                    networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
                }
            }

            transportConfig.NormalizeInPlace();
            if (!transportConfig.TryValidate(out string errorMessage))
            {
                Debug.LogError(errorMessage, this);
                return;
            }

            unityTransport.UseWebSockets = transportConfig.useWebSockets;

            UnityTransport.ConnectionAddressData connectionData = unityTransport.ConnectionData;
            connectionData.Address = transportConfig.connectAddress;
            connectionData.Port = transportConfig.port;
            connectionData.ServerListenAddress = transportConfig.listenAddress;
            connectionData.WebSocketPath = transportConfig.webSocketPath;
            unityTransport.ConnectionData = connectionData;
        }

        public bool StartConfiguredMode()
        {
            ResolveReferences();

            if (networkManager == null)
            {
                Debug.LogError("NPCNetworkBootstrap could not find a NetworkManager component.", this);
                return false;
            }

            if (networkManager.IsListening)
            {
                return true;
            }

            ApplyTransportConfiguration();

            switch (transportConfig.autoStartMode)
            {
                case NPCNetworkAutoStartMode.Client:
                    return networkManager.StartClient();
                case NPCNetworkAutoStartMode.Host:
                    return networkManager.StartHost();
                case NPCNetworkAutoStartMode.Server:
                    return networkManager.StartServer();
                default:
                    return false;
            }
        }

        [ContextMenu("Start Configured Mode")]
        void StartConfiguredModeFromContextMenu()
        {
            StartConfiguredMode();
        }
    }
}
