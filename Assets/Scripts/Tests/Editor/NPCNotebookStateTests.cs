using NUnit.Framework;

namespace NPCSystem.Tests
{
    public class NPCNotebookStateTests
    {
        [Test]
        public void NotebookStateMessageSanitizeInPlaceTrimsDisplayFields()
        {
            var message = new NPCNotebookStateMessage
            {
                npcSlug = "  Butler  ",
                notesPageLeft = "  Left page  ",
                notesPageRight = "  Right page  "
            };

            message.SanitizeInPlace();

            Assert.That(message.npcSlug, Is.EqualTo("butler"));
            Assert.That(message.notesPageLeft, Is.EqualTo("Left page"));
            Assert.That(message.notesPageRight, Is.EqualTo("Right page"));
        }

        [Test]
        public void NotebookStateFormatterBuildIncludesTrustCluesItemsAndLocations()
        {
            var snapshot = new NPCEvidenceStateSnapshot();
            snapshot.discoveredClues.Add(new ClueEntry("butler", "The study window was open.", "observation", 1f));
            snapshot.obtainedItems.Add("rusty-key");
            snapshot.visitedLocations.Add("study");
            snapshot.npcMoodKeys.Add("butler");
            snapshot.npcMoodValues.Add("nervous");
            snapshot.npcTrustKeys.Add("butler");
            snapshot.npcTrustValues.Add(72);

            NPCNotebookStateMessage message = NPCNotebookStateFormatter.Build(snapshot, "butler");

            Assert.That(message.npcSlug, Is.EqualTo("butler"));
            Assert.That(message.notesPageLeft, Does.Contain("trust=cooperative"));
            Assert.That(message.notesPageLeft, Does.Contain("mood=nervous"));
            Assert.That(message.notesPageRight, Does.Contain("The study window was open."));
            Assert.That(message.notesPageRight, Does.Contain("rusty-key"));
            Assert.That(message.notesPageRight, Does.Contain("study"));
        }
    }
}
