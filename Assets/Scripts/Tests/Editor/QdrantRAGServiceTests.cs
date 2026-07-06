using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class QdrantRAGServiceTests
    {
        [Test]
        public void BuildSearchEndpoint_WithValidConfig_ReturnsCorrectUrl()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "npc_knowledge";

            try
            {
                string endpoint = service.BuildSearchEndpoint();
                Assert.That(
                    endpoint,
                    Is.EqualTo("http://localhost:6333/collections/npc_knowledge/points/search")
                );
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void BuildSearchEndpoint_WithMissingUrl_UsesPlaceholder()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();
            service.qdrantUrl = null;
            service.collectionName = "test";

            try
            {
                string endpoint = service.BuildSearchEndpoint();
                Assert.That(endpoint, Does.Contain("<missing-qdrant-url>"));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void BuildSearchEndpoint_WithMissingCollection_UsesPlaceholder()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = null;

            try
            {
                string endpoint = service.BuildSearchEndpoint();
                Assert.That(endpoint, Does.Contain("<missing-collection>"));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void BuildSearchEndpoint_TrimsTrailingSlashFromUrl()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333/";
            service.collectionName = "test";

            try
            {
                string endpoint = service.BuildSearchEndpoint();
                Assert.That(
                    endpoint,
                    Is.EqualTo("http://localhost:6333/collections/test/points/search")
                );
                Assert.That(endpoint, Does.Not.Contain("//collections"));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void HasValidQdrantUrl_AcceptsHttpAndHttps()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();

            try
            {
                service.qdrantUrl = "http://localhost:6333";
                Assert.That(service.HasValidQdrantUrl(), Is.True);

                service.qdrantUrl = "https://qdrant.example.com";
                Assert.That(service.HasValidQdrantUrl(), Is.True);

                service.qdrantUrl = "localhost:6333";
                Assert.That(service.HasValidQdrantUrl(), Is.False);

                service.qdrantUrl = "";
                Assert.That(service.HasValidQdrantUrl(), Is.False);

                service.qdrantUrl = null;
                Assert.That(service.HasValidQdrantUrl(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void HasValidCollectionName_RejectsSpaces()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();

            try
            {
                service.collectionName = "npc_knowledge";
                Assert.That(service.HasValidCollectionName(), Is.True);

                service.collectionName = "my collection";
                Assert.That(service.HasValidCollectionName(), Is.False);

                service.collectionName = "";
                Assert.That(service.HasValidCollectionName(), Is.False);

                service.collectionName = null;
                Assert.That(service.HasValidCollectionName(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void DefaultConfiguration_IsValid()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var service = serviceObject.AddComponent<QdrantRAGService>();

            try
            {
                Assert.That(service.qdrantUrl, Is.EqualTo("http://localhost:6333"));
                Assert.That(service.collectionName, Is.EqualTo("npc_knowledge"));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_WithMockEmbedderAndResponse_ReturnsText()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1,0.2,0.3],\"index\":0}]}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = embedder;
            service.mockResponse =
                "{\"result\":[{\"score\":0.95,\"payload\":{\"text\":\"Found knowledge\"}}]}";

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo("Found knowledge"));
                Assert.That(
                    service.lastRequestedEndpoint,
                    Does.Contain("test_collection/points/search")
                );
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_WithNullMockResponse_ReturnsEmpty()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1,0.2,0.3],\"index\":0}]}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = embedder;
            service.mockResponse = null;

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_WithEmptyQdrantResult_ReturnsEmpty()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1,0.2,0.3],\"index\":0}]}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = embedder;
            service.mockResponse = "{\"result\":[]}";

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_EmbedsderReturnsEmptyVector_ReturnsEmpty()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = embedder;

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_NoEmbedderAssigned_FindsInScene()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1],\"index\":0}]}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = null;
            service.mockResponse = "{\"result\":[]}";

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void SearchMemoryAsync_ReturnsMultipleResults()
        {
            var serviceObject = new GameObject(nameof(QdrantRAGServiceTests));
            var embedderObject = new GameObject("MockEmbedder");
            var embedder = embedderObject.AddComponent<TestableLocalAIEmbedder>();
            embedder.mockResponse = "{\"data\":[{\"embedding\":[0.1,0.2,0.3],\"index\":0}]}";

            var service = serviceObject.AddComponent<TestableQdrantRAGService>();
            service.qdrantUrl = "http://localhost:6333";
            service.collectionName = "test_collection";
            service.embedder = embedder;
            service.mockResponse =
                "{\"result\":[{\"score\":0.95,\"payload\":{\"text\":\"First result\"}},{\"score\":0.80,\"payload\":{\"text\":\"Second result\"}}]}";

            try
            {
                string result = service.SearchMemoryAsync("test query").GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo("First result\nSecond result"));
            }
            finally
            {
                Object.DestroyImmediate(serviceObject);
                Object.DestroyImmediate(embedderObject);
            }
        }
    }

    public class TestableQdrantRAGService : QdrantRAGService
    {
        public string mockResponse;
        public string lastRequestedEndpoint;
        public string lastRequestedJson;

        protected override Task<string> SendSearchRequestAsync(string endpoint, string json)
        {
            lastRequestedEndpoint = endpoint;
            lastRequestedJson = json;
            return Task.FromResult(mockResponse);
        }
    }
}
