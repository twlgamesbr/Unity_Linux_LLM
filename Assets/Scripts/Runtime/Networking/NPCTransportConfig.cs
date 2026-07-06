using System;

namespace NPCSystem
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
        public string connectAddress;
        public string listenAddress;
        public ushort port;
        public bool useWebSockets;
        public string webSocketPath;
        public NPCNetworkAutoStartMode autoStartMode;

        public static NPCTransportConfig CreateDefault()
        {
            return new NPCTransportConfig
            {
                connectAddress = "127.0.0.1",
                listenAddress = "0.0.0.0",
                port = 11474, // non-standard, avoids common port conflicts
                useWebSockets = false,
                webSocketPath = "/npc-dialogue",
                autoStartMode = NPCNetworkAutoStartMode.Manual,
            };
        }

        public void NormalizeInPlace()
        {
            connectAddress = string.IsNullOrWhiteSpace(connectAddress)
                ? string.Empty
                : connectAddress.Trim();
            listenAddress = string.IsNullOrWhiteSpace(listenAddress)
                ? string.Empty
                : listenAddress.Trim();

            string normalizedPath = string.IsNullOrWhiteSpace(webSocketPath)
                ? "/"
                : webSocketPath.Trim();
            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizedPath = "/" + normalizedPath;
            }

            webSocketPath = normalizedPath;
        }

        public bool TryValidate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(connectAddress))
            {
                errorMessage = "NPCTransportConfig.connectAddress must not be blank.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(listenAddress))
            {
                errorMessage = "NPCTransportConfig.listenAddress must not be blank.";
                return false;
            }

            if (port == 0)
            {
                errorMessage = "NPCTransportConfig.port must be greater than 0.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(webSocketPath))
            {
                errorMessage = "NPCTransportConfig.webSocketPath must not be blank.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
