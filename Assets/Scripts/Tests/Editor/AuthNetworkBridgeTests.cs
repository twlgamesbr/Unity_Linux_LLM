using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class AuthNetworkBridgeTests
    {
        [Test]
        public void AuthNetworkBridgeDefaultsToDedicatedClientMode()
        {
            var gameObject = new GameObject("AuthBridgeDefaults");
            var bridge = gameObject.AddComponent<AuthNetworkBridge>();

            try
            {
                Assert.That(bridge.autoDetectStartupMode, Is.False, "Default auth startup must not auto-resolve Player 1 to Host in Docker dedicated-server flow.");
                Assert.That(bridge.startAsHost, Is.False, "Default auth startup must be StartClient, not StartHost.");
                Assert.That(bridge.hostAddress, Is.EqualTo("127.0.0.1"));
                Assert.That(bridge.hostPort, Is.EqualTo((ushort)0), "0 means defer to NPCNetworkBootstrap transportConfig.port.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

#if !UNITY_SERVER
        [Test]
        public void AuthNetworkBridgeResolveReferencesPrefersAuthControllerOnSameGameObject()
        {
            var gameObject = new GameObject("AuthBridgeSameGameObject");
            var authController = gameObject.AddComponent<AuthUIController>();
            var bridge = gameObject.AddComponent<AuthNetworkBridge>();

            try
            {
                typeof(AuthNetworkBridge)
                    .GetMethod("ResolveReferences", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(bridge, null);

                Assert.That(bridge.authController, Is.SameAs(authController));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MainSceneAuthNetworkBridgeIsConfiguredForDockerDedicatedServerClientMode()
        {
            NPCTestHelpers.OpenMainScene();

            AuthNetworkBridge bridge = NPCTestHelpers.RequireComponent<AuthNetworkBridge>("AuthUI");

            Assert.That(bridge.authController, Is.Not.Null, "AuthNetworkBridge.authController must be assigned so auth success can bind events.");
            Assert.That(bridge.networkBootstrap, Is.Not.Null, "AuthNetworkBridge.networkBootstrap must be assigned so networking is delegated to NPCNetworkBootstrap.");
            Assert.That(bridge.autoDetectStartupMode, Is.False, "Docker dedicated-server clients must not auto-resolve to Host.");
            Assert.That(bridge.startAsHost, Is.False, "Unity clients must StartClient(), not StartHost(), when Docker server is running.");
            Assert.That(bridge.hostAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(bridge.hostPort, Is.EqualTo((ushort)0), "0 means use NPCNetworkBootstrap configured port.");
        }
#endif
    }
}
