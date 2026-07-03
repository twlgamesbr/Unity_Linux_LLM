using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCDialogueNetworkingTests
    {
        [Test]
        public void DialogueRequestSanitizeInPlaceTrimsValuesAndGeneratesRequestId()
        {
            var request = new NPCDialogueRequestMessage
            {
                requestId = "   ",
                npcSlug = "  Butler  ",
                playerMessage = "  Where were you last night?  "
            };

            request.SanitizeInPlace();

            Assert.That(request.requestId, Is.Not.Null.And.Not.Empty);
            Assert.That(request.npcSlug, Is.EqualTo("butler"));
            Assert.That(request.playerMessage, Is.EqualTo("Where were you last night?"));
        }

        [Test]
        public void DialogueSelectionSanitizeInPlaceTrimsAndLowercasesNpcSlug()
        {
            var selection = new NPCDialogueSelectionMessage
            {
                npcSlug = "  Chef  "
            };

            selection.SanitizeInPlace();

            Assert.That(selection.npcSlug, Is.EqualTo("chef"));
        }

        [Test]
        public void NetworkSessionManagerStoresAndRetrievesPerClientSelection()
        {
            var gameObject = new GameObject("NPCNetworkSessionManagerTests");
            var sessionManager = gameObject.AddComponent<NPCNetworkSessionManager>();

            try
            {
                sessionManager.SetSelectedNpcSlug(11ul, "Butler");
                sessionManager.SetSelectedNpcSlug(22ul, "Chef");

                Assert.That(sessionManager.TryGetSelectedNpcSlug(11ul, out string firstNpc), Is.True);
                Assert.That(firstNpc, Is.EqualTo("butler"));
                Assert.That(sessionManager.TryGetSelectedNpcSlug(22ul, out string secondNpc), Is.True);
                Assert.That(secondNpc, Is.EqualTo("chef"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NetworkSessionManagerClearClientSessionRemovesSelection()
        {
            var gameObject = new GameObject("NPCNetworkSessionManagerTests");
            var sessionManager = gameObject.AddComponent<NPCNetworkSessionManager>();

            try
            {
                sessionManager.SetSelectedNpcSlug(11ul, "Maid");
                sessionManager.ClearClientSession(11ul);

                Assert.That(sessionManager.TryGetSelectedNpcSlug(11ul, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NetworkSessionManagerStoresIndependentHistorySnapshotsPerClient()
        {
            var gameObject = new GameObject("NPCNetworkSessionManagerTests");
            var sessionManager = gameObject.AddComponent<NPCNetworkSessionManager>();

            try
            {
                sessionManager.SetHistorySnapshot(11ul, "butler", new System.Collections.Generic.List<DialogueEntry>
                {
                    new DialogueEntry("user", "Where were you?"),
                    new DialogueEntry("assistant", "In the study.")
                });
                sessionManager.SetHistorySnapshot(22ul, "butler", new System.Collections.Generic.List<DialogueEntry>
                {
                    new DialogueEntry("user", "Did you hear a noise?"),
                    new DialogueEntry("assistant", "Only the clock.")
                });

                var firstHistory = sessionManager.GetHistorySnapshot(11ul, "butler");
                var secondHistory = sessionManager.GetHistorySnapshot(22ul, "butler");

                Assert.That(firstHistory, Has.Count.EqualTo(2));
                Assert.That(firstHistory[0].content, Is.EqualTo("Where were you?"));
                Assert.That(secondHistory, Has.Count.EqualTo(2));
                Assert.That(secondHistory[0].content, Is.EqualTo("Did you hear a noise?"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NetworkSessionManagerStoresIndependentEvidenceSnapshotsPerClient()
        {
            var gameObject = new GameObject("NPCNetworkSessionManagerTests");
            var sessionManager = gameObject.AddComponent<NPCNetworkSessionManager>();

            try
            {
                var firstSnapshot = new NPCEvidenceStateSnapshot();
                firstSnapshot.discoveredClues.Add(new ClueEntry("butler", "The study window was open.", "observation", 1f));
                firstSnapshot.obtainedItems.Add("rusty-key");

                var secondSnapshot = new NPCEvidenceStateSnapshot();
                secondSnapshot.visitedLocations.Add("kitchen");
                secondSnapshot.npcTrustKeys.Add("chef");
                secondSnapshot.npcTrustValues.Add(72);

                sessionManager.SetEvidenceSnapshot(11ul, firstSnapshot);
                sessionManager.SetEvidenceSnapshot(22ul, secondSnapshot);

                var loadedFirst = sessionManager.GetEvidenceSnapshot(11ul);
                var loadedSecond = sessionManager.GetEvidenceSnapshot(22ul);

                Assert.That(loadedFirst.discoveredClues, Has.Count.EqualTo(1));
                Assert.That(loadedFirst.obtainedItems, Has.Count.EqualTo(1));
                Assert.That(loadedSecond.visitedLocations, Has.Count.EqualTo(1));
                Assert.That(loadedSecond.npcTrustValues, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NetworkSessionManagerStoresIndependentPlayerNamesPerClient()
        {
            var gameObject = new GameObject("NPCNetworkSessionManagerTests");
            var sessionManager = gameObject.AddComponent<NPCNetworkSessionManager>();

            try
            {
                sessionManager.SetPlayerDisplayName(11ul, "Alice");
                sessionManager.SetPlayerDisplayName(22ul, "Bob");

                Assert.That(sessionManager.GetPlayerDisplayName(11ul), Is.EqualTo("Alice"));
                Assert.That(sessionManager.GetPlayerDisplayName(22ul), Is.EqualTo("Bob"));
                Assert.That(sessionManager.GetPlayerDisplayName(33ul), Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
