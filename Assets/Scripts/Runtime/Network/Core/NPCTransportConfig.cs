using System;
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
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem.Network.Core
{
    public enum NPCNetworkAutoStartMode
    {
        Manual,
        Client,
        Host,
        Server,
    }

    [Serializable]
    public struct NPCTransportConfig
    {
        [FormerlySerializedAs("connectAddress")]
        [SerializeField]
        string _connectAddress;
        public string ConnectAddress
        {
            get => _connectAddress;
            set => _connectAddress = value;
        }

        [FormerlySerializedAs("listenAddress")]
        [SerializeField]
        string _listenAddress;
        public string ListenAddress
        {
            get => _listenAddress;
            set => _listenAddress = value;
        }

        [FormerlySerializedAs("port")]
        [SerializeField]
        ushort _port;
        public ushort Port
        {
            get => _port;
            set => _port = value;
        }

        [FormerlySerializedAs("useWebSockets")]
        [SerializeField]
        bool _useWebSockets;
        public bool UseWebSockets
        {
            get => _useWebSockets;
            set => _useWebSockets = value;
        }

        [FormerlySerializedAs("webSocketPath")]
        [SerializeField]
        string _webSocketPath;
        public string WebSocketPath
        {
            get => _webSocketPath;
            set => _webSocketPath = value;
        }

        [FormerlySerializedAs("autoStartMode")]
        [SerializeField]
        NPCNetworkAutoStartMode _autoStartMode;
        public NPCNetworkAutoStartMode AutoStartMode
        {
            get => _autoStartMode;
            set => _autoStartMode = value;
        }

        public static NPCTransportConfig CreateDefault()
        {
            return new NPCTransportConfig
            {
                ConnectAddress = "127.0.0.1",
                ListenAddress = "0.0.0.0",
                Port = 11474, // non-standard, avoids common port conflicts
                UseWebSockets = false,
                WebSocketPath = "/npc-dialogue",
                AutoStartMode = NPCNetworkAutoStartMode.Manual,
            };
        }

        public void NormalizeInPlace()
        {
            _connectAddress = string.IsNullOrWhiteSpace(_connectAddress) ? string.Empty : _connectAddress.Trim();
            _listenAddress = string.IsNullOrWhiteSpace(_listenAddress) ? string.Empty : _listenAddress.Trim();

            string normalizedPath = string.IsNullOrWhiteSpace(_webSocketPath) ? "/" : _webSocketPath.Trim();
            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizedPath = "/" + normalizedPath;
            }

            _webSocketPath = normalizedPath;
        }

        public bool TryValidate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(_connectAddress))
            {
                errorMessage = "NPCTransportConfig.connectAddress must not be blank.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_listenAddress))
            {
                errorMessage = "NPCTransportConfig.listenAddress must not be blank.";
                return false;
            }

            if (_port == 0)
            {
                errorMessage = "NPCTransportConfig.port must be greater than 0.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_webSocketPath))
            {
                errorMessage = "NPCTransportConfig.webSocketPath must not be blank.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
