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
            Assert.That(config.port, Is.EqualTo((ushort)11474));
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
                Assert.That(networkManager.NetworkConfig.Prefabs.Contains(serverNpcPrefab), Is.True);
                Assert.That(networkManager.NetworkConfig.Prefabs.Contains(transferableItemPrefab), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(playerPrefab);
                Object.DestroyImmediate(serverNpcPrefab);
                Object.DestroyImmediate(transferableItemPrefab);
            }
        }

        [Test]
        public void PlayModeResolverParsesPlayerIndexFromPlayerName()
        {
            Assert.That(NPCPlayModeInstanceResolver.TryParsePlayerIndex("Player1", out int playerOne), Is.True);
            Assert.That(playerOne, Is.EqualTo(1));
            Assert.That(NPCPlayModeInstanceResolver.TryParsePlayerIndex("Player3", out int playerThree), Is.True);
            Assert.That(playerThree, Is.EqualTo(3));
            Assert.That(NPCPlayModeInstanceResolver.TryParsePlayerIndex("Editor", out _), Is.False);
        }

        [Test]
        public void PlayModeResolverAssignsUniqueClientBindPortsForAdditionalPlayers()
        {
            Assert.That(NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(1, 11474), Is.EqualTo((ushort)0));
            Assert.That(NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(2, 11474), Is.EqualTo((ushort)11475));
            Assert.That(NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(3, 11474), Is.EqualTo((ushort)11476));
            Assert.That(NPCPlayModeInstanceResolver.ResolveClientBindPortForPlayerIndex(4, 11474, 13000), Is.EqualTo((ushort)13000));
        }
    }
}
