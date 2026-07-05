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
                Assert.That(endpoint, Is.EqualTo("http://localhost:6333/collections/npc_knowledge/points/search"));
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
                Assert.That(endpoint, Is.EqualTo("http://localhost:6333/collections/test/points/search"));
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
    }
}
