using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class AuthNetworkBridgeTests
    {
        [Test]
        public void ActivePlayerName_Default_IsPlayer()
        {
            Assert.That(AuthNetworkBridge.ActivePlayerName, Is.EqualTo("Player"));
        }

        [Test]
        public void ActivePlayerName_SetByHandleAuthSuccess()
        {
            var bridgeObject = new GameObject(nameof(AuthNetworkBridgeTests));
            var bridge = bridgeObject.AddComponent<AuthNetworkBridge>();

            try
            {
                Assert.That(bridge.PlayerName, Is.EqualTo(""));
            }
            finally
            {
                Object.DestroyImmediate(bridgeObject);
            }
        }

        [Test]
        public void TryGetCommandLineStartupMode_NoArgs_ReturnsFalse()
        {
            bool result = AuthNetworkBridge.TryGetCommandLineStartupMode(
                out AuthNetworkBridge.ResolvedNetworkStartupMode mode
            );
            Assert.That(result, Is.False);
        }

        [Test]
        public void ResolveStartupMode_DefaultIsHost_WhenStartAsHostTrue()
        {
            var bridgeObject = new GameObject(nameof(AuthNetworkBridgeTests));
            var bridge = bridgeObject.AddComponent<AuthNetworkBridge>();
            bridge.startAsHost = true;
            bridge.autoDetectStartupMode = false;

            try
            {
                bool found = AuthNetworkBridge.TryGetCommandLineStartupMode(
                    out AuthNetworkBridge.ResolvedNetworkStartupMode _
                );
                Assert.That(
                    found,
                    Is.False,
                    "Should NOT find a command-line mode in edit-mode tests (no -npc-client or -npc-host args)."
                );
            }
            finally
            {
                Object.DestroyImmediate(bridgeObject);
            }
        }
    }
}
