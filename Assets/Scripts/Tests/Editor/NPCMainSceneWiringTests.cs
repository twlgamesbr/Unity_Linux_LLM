using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCMainSceneWiringTests
    {
        [Test]
        public void MainSceneContainsCriticalNetworkAuthAndDialogueComponents()
        {
            NPCTestHelpers.OpenMainScene();

            var networkManager = NPCTestHelpers.RequireComponent<NetworkManager>("Network_Manager");
            var unityTransport = NPCTestHelpers.RequireComponent<UnityTransport>("Network_Manager");
            var networkBootstrap = NPCTestHelpers.RequireComponent<NPCNetworkBootstrap>("Network_Manager");

            Assert.That(networkBootstrap.networkManager, Is.SameAs(networkManager));
            Assert.That(networkBootstrap.unityTransport, Is.SameAs(unityTransport));
            Assert.That(networkBootstrap.playerPrefab, Is.Not.Null, "Network bootstrap must have Player Prefab assigned.");
            Assert.That(networkBootstrap.serverNpcPrefab, Is.Not.Null, "Network bootstrap must have Server NPC Prefab assigned.");
            Assert.That(networkBootstrap.transferableItemPrefab, Is.Not.Null, "Network bootstrap must have Transferable Item Prefab assigned.");

            var sceneInitialization = NPCTestHelpers.RequireComponent<NPCSceneInitializationController>("NPCSceneInitialization");
            Assert.That(sceneInitialization.networkBootstrap, Is.SameAs(networkBootstrap));
            Assert.That(sceneInitialization.dialogueManager, Is.Not.Null);
            Assert.That(sceneInitialization.backendReadiness, Is.Not.Null);
            Assert.That(sceneInitialization.networkBridge, Is.Not.Null);
            Assert.That(sceneInitialization.smokeValidator, Is.Not.Null);

            NPCTestHelpers.RequireComponent<NPCFlowLogger>("NPCFlowLogger");
            NPCTestHelpers.RequireComponent<NetworkObject>("NPCDialogueRuntimeBridge");
            NPCTestHelpers.RequireComponent<NPCNetworkSessionManager>("NPCDialogueRuntimeBridge");
            NPCTestHelpers.RequireComponent<NPCDialogueNetworkBridge>("NPCDialogueRuntimeBridge");

#if !UNITY_SERVER
            var authController = NPCTestHelpers.RequireComponent<AuthUIController>("Canvas/AuthUI");
            NPCTestHelpers.RequireComponent<PlayerAuthService>("Canvas/AuthUI");
            var authBridge = NPCTestHelpers.RequireComponent<AuthNetworkBridge>("Canvas/AuthUI");
            Assert.That(authBridge.authController, Is.SameAs(authController));
            Assert.That(authBridge.networkBootstrap, Is.SameAs(networkBootstrap));
#endif
        }
    }
}
