using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            var client = clientObject.AddComponent<TestableLocalAIClient>();
            client.host = "127.0.0.1";
            client.port = 19999;
            client.numRetries = 0;
            client.mockResponse = null;

            try
            {
                string result = client.ChatAsync(null).GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
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
                var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                string result = client.ChatAsync(messages).GetAwaiter().GetResult();
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
                LogAssert.Expect(LogType.Error, new Regex(@"\[NPCLocalAIClient\] Unexpected response format from http://127\.0\.0\.1:19999/v1/chat/completions"));
                var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                string result = client.ChatAsync(messages).GetAwaiter().GetResult();
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
                var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                string result = client.ChatAsync(messages).GetAwaiter().GetResult();
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
                var messages = new[] { new NPCOpenAIMessage { role = "user", content = "Hi" } };
                string result = client.ChatAsync(messages).GetAwaiter().GetResult();
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

        protected override Task<string> SendChatRequestAsync(string uri, string json)
        {
            lastRequestedUri = uri;
            lastRequestedJson = json;
            return Task.FromResult(mockResponse);
        }
    }
}
