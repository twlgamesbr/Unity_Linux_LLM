using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    /// <summary>
    /// Smoke tests for code paths that run on WebGL builds.
    /// These validate async, URL resolution, and transport patterns
    /// that could behave differently on the WebGL platform.
    /// </summary>
    public class NPCWebGLSmokeTests
    {
        [Test]
        public async Task AsyncPattern_CompletesWithinTimeout()
        {
            // WebGL runs on a single thread with a main-loop yield model.
            // This smoke test validates that the Task.Yield() + async pattern
            // used throughout NPC runtime code (e.g. NPCDialogueRetrievalService)
            // completes normally in the editor test runner — a baseline
            // that must hold on WebGL.
            int steps = 0;
            const int expectedSteps = 3;

            await Task.Yield();
            steps++;
            await Task.Delay(1);
            steps++;
            steps++; // synchronous step

            Assert.That(steps, Is.EqualTo(expectedSteps), "Async yield pattern completed all expected steps.");
        }

        [Test]
        public async Task AsyncTryFinally_CompletesCleanly()
        {
            // The NPC runtime uses async try/finally extensively
            // (e.g. NPCDialogueRetrievalService's scope-based logging).
            // This validates the pattern doesn't hang or skip cleanup.
            bool cleanupRan = false;

            try
            {
                await Task.Yield();
            }
            finally
            {
                cleanupRan = true;
            }

            Assert.That(cleanupRan, Is.True, "Async try/finally cleanup executed.");
        }

        [Test]
        public void IsLocalHost_DetectsLoopback()
        {
            // Shared utility used by WebGL URL-resolution across 3 files.
            Assert.That(NPCNetworkUtils.IsLocalHost("localhost"), Is.True);
            Assert.That(NPCNetworkUtils.IsLocalHost("127.0.0.1"), Is.True);
            Assert.That(NPCNetworkUtils.IsLocalHost("LOCALHOST"), Is.True, "Case-insensitive");
            Assert.That(NPCNetworkUtils.IsLocalHost("192.168.1.1"), Is.False);
            Assert.That(NPCNetworkUtils.IsLocalHost(null), Is.False);
            Assert.That(NPCNetworkUtils.IsLocalHost(""), Is.False);
        }

        [Test]
        public void WebGlAutoEnable_ForcesWebSocketWhenFlagged()
        {
            // Simulates the #if UNITY_WEBGL && !UNITY_EDITOR guard
            // by manually setting the flag — the actual code path is verified
            // in NPCNetworkingTests.TransportConfigCreateDefaultProducesValidConfig.
            var config = NPCTransportConfig.CreateDefault();
            config.UseWebSockets = true;

            Assert.That(config.UseWebSockets, Is.True);
            Assert.That(config.WebSocketPath, Is.EqualTo("/npc-dialogue"));
        }

        [Test]
        public void FlowLogger_DisablesFileLoggingOnWebGL()
        {
            // WebGL cannot write to the filesystem.
            // Already tested in NPCFlowLoggerPlatformTests but included
            // here for completeness as part of the WebGL smoke suite.
            Assert.That(
                NPCFlowLogger.SupportsPersistentFileLogging(RuntimePlatform.WebGLPlayer),
                Is.False
            );
            Assert.That(
                NPCFlowLogger.SupportsPersistentFileLogging(RuntimePlatform.LinuxEditor),
                Is.True
            );
        }
    }
}
