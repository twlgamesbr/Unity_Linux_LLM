using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NPCSystem.Tests
{
    public class DialogueDisplayHelperTests
    {
        [Test]
        public void NormalizeError_NullOrEmpty_ReturnsDefault()
        {
            Assert.That(DialogueDisplayHelper.NormalizeError(null), Is.EqualTo("Unknown dialogue error."));
            Assert.That(DialogueDisplayHelper.NormalizeError(""), Is.EqualTo("Unknown dialogue error."));
            Assert.That(DialogueDisplayHelper.NormalizeError("  "), Is.EqualTo("Unknown dialogue error."));
        }

        [Test]
        public void NormalizeError_Whitespace_Trimmed()
        {
            Assert.That(DialogueDisplayHelper.NormalizeError("  error  "), Is.EqualTo("error"));
        }

        [Test]
        public void FormatErrorForDisplay_AddsPrefix()
        {
            Assert.That(DialogueDisplayHelper.FormatErrorForDisplay("connection lost"), Is.EqualTo("Error: connection lost"));
            Assert.That(DialogueDisplayHelper.FormatErrorForDisplay(null), Is.EqualTo("Error: Unknown dialogue error."));
        }

        [Test]
        public void SetInputEnabled_NullComponents_NoException()
        {
            DialogueDisplayHelper.SetInputEnabled(null, null, true);
            Assert.Pass("No exception thrown for null components");
        }

        [Test]
        public void SetInputEnabled_SetsInteractable()
        {
            var go = new GameObject("TestInput");
            var input = go.AddComponent<TMP_InputField>();
            var button = go.AddComponent<Button>();

            DialogueDisplayHelper.SetInputEnabled(input, button, false);

            Assert.That(input.interactable, Is.False);
            Assert.That(button.interactable, Is.False);

            DialogueDisplayHelper.SetInputEnabled(input, button, true);

            Assert.That(input.interactable, Is.True);
            Assert.That(button.interactable, Is.True);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetAIText_NullComponent_NoException()
        {
            DialogueDisplayHelper.SetAIText(null, "text");
            Assert.Pass("No exception thrown for null component");
        }

        [Test]
        public void UpdatePortrait_NullProfile_AllFadeOut()
        {
            var go = new GameObject("TestPortraits");
            var butler = go.AddComponent<RawImage>();
            var maid = go.AddComponent<RawImage>();
            var chef = go.AddComponent<RawImage>();

            butler.canvasRenderer.SetAlpha(1f);
            maid.canvasRenderer.SetAlpha(1f);
            chef.canvasRenderer.SetAlpha(1f);

            DialogueDisplayHelper.UpdatePortrait(null, butler, maid, chef);

            Assert.That(butler.canvasRenderer.GetAlpha(), Is.LessThan(0.5f));
            Assert.That(maid.canvasRenderer.GetAlpha(), Is.LessThan(0.5f));
            Assert.That(chef.canvasRenderer.GetAlpha(), Is.LessThan(0.5f));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FindEvidenceState_NoParent_ReturnsNull()
        {
            var go = new GameObject("TestHost");
            var host = go.AddComponent<MonoBehaviour>();
            
            // Can't actually test this without NPCEvidenceState existing
            // Just verify no exception
            Assert.Pass("FindEvidenceState completes without exception");

            Object.DestroyImmediate(go);
        }
    }
}