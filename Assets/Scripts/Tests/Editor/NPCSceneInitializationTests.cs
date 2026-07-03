using System;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCSceneInitializationTests
    {
        [Test]
        public void SceneInitializationPhasesRemainInExpectedOrder()
        {
            Assert.That(NPCSceneInitializationController.OrderedPhases, Is.EqualTo(new[]
            {
                NPCSceneInitializationPhase.Logger,
                NPCSceneInitializationPhase.SceneReferences,
                NPCSceneInitializationPhase.NetworkTransport,
                NPCSceneInitializationPhase.DialogueServices,
                NPCSceneInitializationPhase.BackendReadiness,
                NPCSceneInitializationPhase.NetworkBridge,
                NPCSceneInitializationPhase.Validation,
                NPCSceneInitializationPhase.Spawning
            }));
        }

        [Test]
        public void RuntimeBootstrapScriptsHaveDeterministicExecutionOrder()
        {
            AssertExecutionOrder<NPCFlowLogger>(-3000);
            AssertExecutionOrder<NPCNetworkBootstrap>(-2500);
            AssertExecutionOrder<NPCSceneInitializationController>(-2000);
            AssertExecutionOrder<NPCDialogueManager>(-1500);
            AssertExecutionOrder<NPCDialogueBootstrapper>(-1000);
            AssertExecutionOrder<NPCDialogueNetworkBridge>(-900);
            AssertExecutionOrder<AuthNetworkBridge>(-500);
#if !UNITY_SERVER
            AssertExecutionOrder<NPCDialogueUIController>(-400);
#endif
            AssertExecutionOrder<PlayerAuthService>(-350);
#if !UNITY_SERVER
            AssertExecutionOrder<AuthUIController>(-300);
#endif
            AssertExecutionOrder<NPCDialogueSmokeValidator>(500);
        }

        static void AssertExecutionOrder<T>(int expectedOrder)
        {
            var attribute = Attribute.GetCustomAttribute(typeof(T), typeof(DefaultExecutionOrder)) as DefaultExecutionOrder;
            Assert.That(attribute, Is.Not.Null, $"{typeof(T).Name} is missing DefaultExecutionOrderAttribute.");
            Assert.That(attribute.order, Is.EqualTo(expectedOrder), $"{typeof(T).Name} has the wrong execution order.");
        }
    }
}
