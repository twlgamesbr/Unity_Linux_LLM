using NUnit.Framework;
using NPCSystem;
using System.Linq;

namespace NPCTests
{
    [TestFixture]
    public class NPCSparseVectorEncoderTests
    {
        [Test]
        public void Encode_EmptyString_ReturnsSentinel()
        {
            var result = NPCSparseVectorEncoder.Encode("");
            Assert.That(result.indices, Is.EqualTo(new[] { 0 }));
            Assert.That(result.values, Is.EqualTo(new[] { 0f }));
        }

        [Test]
        public void Encode_OnlyStopWords_ReturnsSentinel()
        {
            var result = NPCSparseVectorEncoder.Encode("the is a");
            Assert.That(result.indices, Is.EqualTo(new[] { 0 }));
            Assert.That(result.values, Is.EqualTo(new[] { 0f }));
        }

        [Test]
        public void Encode_SingleToken_ReturnsValueOne()
        {
            var result = NPCSparseVectorEncoder.Encode("Unity");
            Assert.That(result.indices.Length, Is.EqualTo(1));
            Assert.That(result.values[0], Is.EqualTo(1f));
        }

        [Test]
        public void Encode_MultipleTokens_CalculatesTF()
        {
            // "unity unity script"
            // freq: unity=2, script=1
            // maxFreq: 2
            // values: unity=1.0, script=0.5
            var result = NPCSparseVectorEncoder.Encode("unity unity script");
            Assert.That(result.indices.Length, Is.EqualTo(2));
            Assert.That(result.values, Contains.Item(1f));
            Assert.That(result.values, Contains.Item(0.5f));
        }

        [Test]
        public void Encode_TokenIndex_IsDeterministic()
        {
            var result1 = NPCSparseVectorEncoder.Encode("collision");
            var result2 = NPCSparseVectorEncoder.Encode("collision");
            Assert.That(result1.indices, Is.EqualTo(result2.indices));
            Assert.That(result1.values, Is.EqualTo(result2.values));
        }
    }
}
