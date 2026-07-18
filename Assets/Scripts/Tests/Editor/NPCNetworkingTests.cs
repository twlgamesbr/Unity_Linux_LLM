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
            Assert.That(config.ConnectAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(config.ListenAddress, Is.EqualTo("0.0.0.0"));
            Assert.That(config.Port, Is.EqualTo((ushort)11474));
            Assert.That(config.WebSocketPath, Is.EqualTo("/npc-dialogue"));
            Assert.That(config.AutoStartMode, Is.EqualTo(NPCNetworkAutoStartMode.Manual));
        }

        [Test]
        public void TransportConfigNormalizeInPlaceAddsLeadingSlashAndFallsBackToRoot()
        {
            NPCTransportConfig config = NPCTransportConfig.CreateDefault();
            config.WebSocketPath = "npc-dialogue";

            config.NormalizeInPlace();
            Assert.That(config.WebSocketPath, Is.EqualTo("/npc-dialogue"));

            config.WebSocketPath = "   ";
            config.NormalizeInPlace();
            Assert.That(config.WebSocketPath, Is.EqualTo("/"));
        }

        [Test]
        public void TransportConfigTryValidateFailsWhenConnectAddressIsBlank()
        {
            NPCTransportConfig config = NPCTransportConfig.CreateDefault();
            config.ConnectAddress = "   ";

            bool isValid = config.TryValidate(out string errorMessage);

            Assert.That(isValid, Is.False);
            Assert.That(errorMessage, Does.Contain("ConnectAddress"));
        }

        [Test]
        public void NetworkBootstrapApplyTransportConfigurationWritesConfigIntoUnityTransport()
        {
            var gameObject = new GameObject("NPCNetworkingTests");
            var playerPrefab = new GameObject("NPCPlayerPrefab");
            playerPrefab.AddComponent<NetworkObject>();
            var serverNpcPrefab = new GameObject("NPCServerPrefab");
            serverNpcPrefab.AddComponent<NetworkObject>();
            var transferableItemPrefab = new GameObject("NPCTransferableItemPrefab");
            transferableItemPrefab.AddComponent<NetworkObject>();
            var networkManager = gameObject.AddComponent<NetworkManager>();
            var unityTransport = gameObject.AddComponent<UnityTransport>();
            var bootstrap = gameObject.AddComponent<NPCNetworkBootstrap>();

            try
            {
                bootstrap.NetworkManager = networkManager;
                bootstrap.UnityTransport = unityTransport;
                bootstrap.PlayerPrefab = playerPrefab;
                bootstrap.ServerNpcPrefab = serverNpcPrefab;
                bootstrap.TransferableItemPrefab = transferableItemPrefab;
                bootstrap.TransportConfig = new NPCTransportConfig
                {
                    ConnectAddress = "10.0.0.25",
                    ListenAddress = "0.0.0.0",
                    Port = 8900,
                    UseWebSockets = true,
                    WebSocketPath = "npc-dialogue",
                    AutoStartMode = NPCNetworkAutoStartMode.Manual
                };
                bootstrap.ApplyTransportConfiguration();

                Assert.That(bootstrap.NetworkManager, Is.Not.Null);
                Assert.That(bootstrap.UnityTransport, Is.Not.Null);
                Assert.That(bootstrap.UnityTransport.ConnectionData.Address, Is.EqualTo("10.0.0.25"));
                Assert.That(bootstrap.UnityTransport.ConnectionData.Port, Is.EqualTo((ushort)8900));
                Assert.That(bootstrap.UnityTransport.ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(playerPrefab);
                Object.DestroyImmediate(serverNpcPrefab);
                Object.DestroyImmediate(transferableItemPrefab);
            }
        }
    }
}
