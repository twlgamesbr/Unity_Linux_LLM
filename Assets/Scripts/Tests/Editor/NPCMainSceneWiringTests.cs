using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

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

            Assert.That(networkBootstrap.NetworkManager, Is.SameAs(networkManager));
            Assert.That(networkBootstrap.UnityTransport, Is.SameAs(unityTransport));
            Assert.That(networkBootstrap.PlayerPrefab, Is.Not.Null, "Network bootstrap must have Player Prefab assigned.");
            Assert.That(networkBootstrap.ServerNpcPrefab, Is.Not.Null, "Network bootstrap must have Server NPC Prefab assigned.");
            Assert.That(networkBootstrap.TransferableItemPrefab, Is.Not.Null, "Network bootstrap must have Transferable Item Prefab assigned.");

            var sceneInitialization = NPCTestHelpers.RequireComponent<NPCSceneInitializationController>("NPCSceneInitialization");
            Assert.That(sceneInitialization.NetworkBootstrap, Is.SameAs(networkBootstrap));
            Assert.That(sceneInitialization.DialogueManager, Is.Not.Null);
            Assert.That(sceneInitialization.BackendReadiness, Is.Not.Null);
            Assert.That(sceneInitialization.NetworkBridge, Is.Not.Null);
            Assert.That(sceneInitialization.SmokeValidator, Is.Not.Null);

            NPCTestHelpers.RequireComponent<NPCFlowLogger>("NPCFlowLogger");
            NPCTestHelpers.RequireComponent<NetworkObject>("NPCDialogueRuntimeBridge");
            NPCTestHelpers.RequireComponent<NPCNetworkSessionManager>("NPCDialogueRuntimeBridge");
            NPCTestHelpers.RequireComponent<NPCDialogueNetworkBridge>("NPCDialogueRuntimeBridge");

#if !UNITY_SERVER
            var authController = NPCTestHelpers.RequireComponent<AuthUIController>("AuthUI");
            NPCTestHelpers.RequireComponent<PlayerAuthService>("AuthUI");
            var authBridge = NPCTestHelpers.RequireComponent<AuthNetworkBridge>("AuthUI");
            Assert.That(authBridge.AuthController, Is.SameAs(authController));
            Assert.That(authBridge.NetworkBootstrap, Is.SameAs(networkBootstrap));
#endif
        }
    }
}
