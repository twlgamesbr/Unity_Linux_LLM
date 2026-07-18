using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NPCSystem.Tests
{
    public class NPCLocalAIEmbedderTests
    {
        [Test]
        public void DefaultConfiguration_IsValid()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();

            try
            {
                Assert.That(embedder.Host, Is.EqualTo("localhost"));
                Assert.That(embedder.Port, Is.EqualTo(8080));
                Assert.That(embedder.Model, Is.EqualTo("default-embedding"));
                Assert.That(embedder.NumRetries, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_NullQuery_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();

            try
            {
                List<float> result = embedder.Embeddings(null).GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_WhitespaceQuery_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();

            try
            {
                List<float> result = embedder.Embeddings("   ").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_WithMockResponse_ReturnsVector()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.Host = "127.0.0.1";
            embedder.Port = 19999;
            embedder.NumRetries = 0;
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1,0.2,0.3],\"index\":0}]}";

            try
            {
                List<float> result = embedder.Embeddings("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Count, Is.EqualTo(3));
                Assert.That(result[0], Is.EqualTo(0.1f).Within(0.001));
                Assert.That(result[1], Is.EqualTo(0.2f).Within(0.001));
                Assert.That(result[2], Is.EqualTo(0.3f).Within(0.001));
                Assert.That(embedder.lastRequestedUri, Does.Contain("127.0.0.1:19999"));
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_NullMockResponse_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.Host = "127.0.0.1";
            embedder.Port = 19999;
            embedder.NumRetries = 0;
            embedder.mockResponse = null;

            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[NPCLocalAIEmbedder\] Unexpected response format from http://127\.0\.0\.1:19999/v1/embeddings"));
                List<float> result = embedder.Embeddings("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_EmptyDataArray_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.Host = "127.0.0.1";
            embedder.Port = 19999;
            embedder.NumRetries = 0;
            embedder.mockResponse = "{}";

            try
            {
                List<float> result = embedder.Embeddings("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_CapturesRequestJson()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.Host = "127.0.0.1";
            embedder.Port = 19999;
            embedder.NumRetries = 0;
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.5],\"index\":0}]}";

            try
            {
                embedder.Embeddings("hello world").GetAwaiter().GetResult();
                Assert.That(embedder.lastRequestedJson, Does.Contain("\"input\":\"hello world\""));
                Assert.That(embedder.lastRequestedJson, Does.Contain("\"model\":\"default-embedding\""));
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }
    }

    public class TestableLocalAIEmbedder : NPCLocalAIEmbedder
    {
        public string mockResponse;
        public string lastRequestedUri;
        public string lastRequestedJson;

        protected override Task<string> SendEmbeddingRequestAsync(string uri, string json)
        {
            lastRequestedUri = uri;
            lastRequestedJson = json;
            return Task.FromResult(mockResponse);
        }
    }
}
