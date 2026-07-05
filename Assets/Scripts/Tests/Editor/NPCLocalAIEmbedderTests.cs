using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCLocalAIEmbedderTests
    {
        [Test]
        public void Embeddings_WithNullQuery_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();
            embedder.host = "127.0.0.1";
            embedder.port = 19998;
            embedder.numRetries = 0;

            try
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    var result = await embedder.Embeddings(null);
                    Assert.That(result, Is.Empty);
                });
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void Embeddings_WithWhitespace_ReturnsEmpty()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();
            embedder.host = "127.0.0.1";
            embedder.port = 19998;
            embedder.numRetries = 0;

            try
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    var result = await embedder.Embeddings("   ");
                    Assert.That(result, Is.Empty);
                });
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }

        [Test]
        public void DefaultConfiguration_IsValid()
        {
            var embedderObject = new GameObject(nameof(NPCLocalAIEmbedderTests));
            var embedder = embedderObject.AddComponent<NPCLocalAIEmbedder>();

            try
            {
                Assert.That(embedder.host, Is.EqualTo("localhost"));
                Assert.That(embedder.port, Is.EqualTo(8080));
                Assert.That(embedder.model, Is.EqualTo("default-embedding"));
                Assert.That(embedder.numRetries, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(embedderObject);
            }
        }
    }
}
