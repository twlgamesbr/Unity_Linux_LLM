using System.Collections.Generic;
using System.Threading.Tasks;
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

        [Test]
        public void ChatAsync_WithMockResponse_ReturnsContent()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<TestableLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;
            client.mockResponse = "{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Hello there!\"}}]}";

            try
            {
                string result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                    result = await client.ChatAsync(messages);
                });
                Assert.That(result, Is.EqualTo("Hello there!"));
                Assert.That(client.lastRequestedUri, Does.Contain("127.0.0.1:19999"));
                Assert.That(client.lastRequestedJson, Does.Contain("\"model\":\"default-llm\""));
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }

        [Test]
        public void ChatAsync_WithNullMockResponse_ReturnsEmpty()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<TestableLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;
            client.mockResponse = null;

            try
            {
                string result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                    result = await client.ChatAsync(messages);
                });
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }

        [Test]
        public void ChatAsync_StripsThinkBlocks()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<TestableLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;
            client.mockResponse = "{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"<think>internal reasoning</think>Hello!\"}}]}";

            try
            {
                string result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                    result = await client.ChatAsync(messages);
                });
                Assert.That(result, Is.EqualTo("Hello!"));
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }

        [Test]
        public void ChatAsync_EmptyChoices_ReturnsEmpty()
        {
            var clientObject = new GameObject(nameof(NPCLocalAIClientTests));
            var client = clientObject.AddComponent<TestableLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;
            client.mockResponse = "{}";

            try
            {
                string result = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                    result = await client.ChatAsync(messages);
                });
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(clientObject);
            }
        }
    }

    public class TestableLocalAIClient : NPCLocalAIClient
    {
        public string mockResponse;
        public string lastRequestedUri;
        public string lastRequestedJson;

        protected override async Task<string> SendChatRequestAsync(string uri, string json)
        {
            await Task.Yield();
            lastRequestedUri = uri;
            lastRequestedJson = json;
            return mockResponse;
        }
    }
}
