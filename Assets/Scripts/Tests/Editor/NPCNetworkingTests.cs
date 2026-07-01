using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCNetworkingTests
    {
        [Test]
        public void TransportConfigCreateDefaultProducesValidConfig()
        {
            NPCTransportConfig config = NPCTransportConfig.CreateDefault();

            bool isValid = config.TryValidate(out string errorMessage);

            Assert.That(isValid, Is.True, errorMessage);
            Assert.That(config.connectAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(config.listenAddress, Is.EqualTo("0.0.0.0"));
            Assert.That(config.port, Is.EqualTo((ushort)7777));
            Assert.That(config.webSocketPath, Is.EqualTo("/npc-dialogue"));
            Assert.That(config.autoStartMode, Is.EqualTo(NPCNetworkAutoStartMode.Manual));
        }

        [Test]
        public void TransportConfigNormalizeInPlaceAddsLeadingSlashAndFallsBackToRoot()
        {
            NPCTransportConfig config = NPCTransportConfig.CreateDefault();
            config.webSocketPath = "npc-dialogue";

            config.NormalizeInPlace();
            Assert.That(config.webSocketPath, Is.EqualTo("/npc-dialogue"));

            config.webSocketPath = "   ";
            config.NormalizeInPlace();
            Assert.That(config.webSocketPath, Is.EqualTo("/"));
        }

        [Test]
        public void TransportConfigTryValidateFailsWhenConnectAddressIsBlank()
        {
            NPCTransportConfig config = NPCTransportConfig.CreateDefault();
            config.connectAddress = "   ";

            bool isValid = config.TryValidate(out string errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("connectAddress"));
        }

        [Test]
        public void NetworkBootstrapApplyTransportConfigurationWritesConfigIntoUnityTransport()
        {
            var gameObject = new GameObject("NPCNetworkingTests");
            var playerPrefab = new GameObject("NPCPlayerPrefab");
            var networkManager = gameObject.AddComponent<NetworkManager>();
            var unityTransport = gameObject.AddComponent<UnityTransport>();
            var bootstrap = gameObject.AddComponent<NPCNetworkBootstrap>();

            try
            {
                bootstrap.networkManager = networkManager;
                bootstrap.unityTransport = unityTransport;
                bootstrap.playerPrefab = playerPrefab;
                bootstrap.transportConfig = new NPCTransportConfig
                {
                    connectAddress = "10.0.0.25",
                    listenAddress = "0.0.0.0",
                    port = 8900,
                    useWebSockets = true,
                    webSocketPath = "npc-dialogue",
                    autoStartMode = NPCNetworkAutoStartMode.Manual
                };

                bootstrap.ApplyTransportConfiguration();

                Assert.That(unityTransport.UseWebSockets, Is.True);
                Assert.That(unityTransport.ConnectionData.Address, Is.EqualTo("10.0.0.25"));
                Assert.That(unityTransport.ConnectionData.Port, Is.EqualTo((ushort)8900));
                Assert.That(unityTransport.ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
                Assert.That(unityTransport.ConnectionData.WebSocketPath, Is.EqualTo("/npc-dialogue"));
                Assert.That(networkManager.NetworkConfig.NetworkTransport, Is.SameAs(unityTransport));
                Assert.That(networkManager.NetworkConfig.PlayerPrefab, Is.SameAs(playerPrefab));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(playerPrefab);
            }
        }
    }
}
