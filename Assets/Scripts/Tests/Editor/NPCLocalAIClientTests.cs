using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCLocalAIClientTests
    {
        [Test]
        public void OpenAIMessage_StoresValues()
        {
            var msg = new NPCOpenAIMessage
            {
                role = "system",
                content = "You are an NPC."
            };

            Assert.That(msg.role, Is.EqualTo("system"));
            Assert.That(msg.content, Is.EqualTo("You are an NPC."));
        }

        [Test]
        public void ChatAsync_WithNullMessages_DoesNotThrow()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<NPCLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;

            try
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    string result = await client.ChatAsync(null);
                    Assert.That(result, Is.Empty);
                });
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }

        [Test]
        public void DefaultConfiguration_IsValid()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<NPCLocalAIClient>();

            try
            {
                Assert.That(client.host, Is.EqualTo("localhost"));
                Assert.That(client.port, Is.EqualTo(11435));
                Assert.That(client.model, Is.EqualTo("default-llm"));
                Assert.That(client.numRetries, Is.EqualTo(3));
                Assert.That(client.temperature, Is.EqualTo(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }
    }
}
