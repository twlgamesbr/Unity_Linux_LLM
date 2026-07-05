using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NPCSystem.Tests
{
    public class NPCDialogueManagerTests
    {
        [Test]
        public void SwitchingToKnownProfileSetsCurrentProfileAndRaisesNpcChangedEvent()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            var profile = CreateProfile("butler", "Butler");
            manager.profiles = new[] { profile };
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            string changedName = null;
            manager.onNPCChanged.AddListener(name => changedName = name);

            try
            {
                manager.SwitchToNPCAsync("butler").GetAwaiter().GetResult();

                Assert.That(manager.currentProfile, Is.SameAs(profile));
                Assert.That(changedName, Is.EqualTo("Butler"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SendingBlankMessage_DoesNotRaiseErrorOrStartResponse()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            var profile = CreateProfile("maid", "Maid");
            manager.profiles = new[] { profile };
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            string errorMessage = null;
            string responseStart = null;
            manager.onError.AddListener(message => errorMessage = message);
            manager.onResponseStart.AddListener(message => responseStart = message);

            try
            {
                manager.SwitchToNPCAsync("maid").GetAwaiter().GetResult();
                manager.SendMessage("   ");

                Assert.That(errorMessage, Is.Null);
                Assert.That(responseStart, Is.Null);
                Assert.That(manager.isResponding, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SwitchToNPCAsync_UnknownSlug_RaisesError()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            manager.profiles = Array.Empty<NPCProfile>();
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            string errorMessage = null;
            manager.onError.AddListener(message => errorMessage = message);

            try
            {
                manager.SwitchToNPCAsync("nonexistent").GetAwaiter().GetResult();
                Assert.That(errorMessage, Is.EqualTo("NPC 'nonexistent' not found"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SendMessage_NoProfileSelected_RaisesError()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            manager.profiles = Array.Empty<NPCProfile>();
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            string errorMessage = null;
            manager.onError.AddListener(message => errorMessage = message);

            try
            {
                manager.SendMessage("Hello");
                Assert.That(errorMessage, Is.EqualTo("No NPC selected"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void GetDefaultProfileSlug_WithNoProfiles_ReturnsEmpty()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            manager.profiles = Array.Empty<NPCProfile>();
            manager.initializeOnStart = false;

            try
            {
                string slug = manager.GetDefaultProfileSlug();
                Assert.That(slug, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void GetDefaultProfileSlug_WithProfiles_ReturnsFirstSlug()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            var butler = CreateProfile("butler", "Butler");
            var maid = CreateProfile("maid", "Maid");
            manager.profiles = new[] { butler, maid };
            manager.initializeOnStart = false;

            try
            {
                string slug = manager.GetDefaultProfileSlug();
                Assert.That(slug, Is.EqualTo("butler"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(butler);
                Object.DestroyImmediate(maid);
            }
        }

        [Test]
        public void GetNPCNames_ReturnsAllSlugs()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            var butler = CreateProfile("butler", "Butler");
            var maid = CreateProfile("maid", "Maid");
            manager.profiles = new[] { butler, maid };
            manager.initializeOnStart = false;

            try
            {
                string[] names = manager.GetNPCNames();
                Assert.That(names, Is.EquivalentTo(new[] { "butler", "maid" }));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(butler);
                Object.DestroyImmediate(maid);
            }
        }

        [Test]
        public void IsTechnicalCodebaseQuestion_DetectsTechnicalMarkers()
        {
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion("Where is the script for PlayerController?"), Is.True);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion("How does the RAG collection work?"), Is.True);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion("Which file implements NPCDialogueManager?"), Is.True);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion("Tell me about your day"), Is.False);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion(""), Is.False);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion("   "), Is.False);
            Assert.That(NPCDialogueManager.IsTechnicalCodebaseQuestion(null), Is.False);
        }

        [Test]
        public void CancelRequests_ClearsState()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            manager.initializeOnStart = false;

            try
            {
                manager.SetRuntimePlayerContext("Alice", 99ul);
                Assert.DoesNotThrow(() => manager.CancelRequests());

                manager.SendMessage("Hello");
                Assert.That(manager.isResponding, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SetRuntimePlayerContext_StoresValues()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            manager.initializeOnStart = false;

            try
            {
                manager.SetRuntimePlayerContext("Alice", 42ul);
                Assert.DoesNotThrow(() => manager.ClearRuntimePlayerContext());
                Assert.DoesNotThrow(() => manager.SetRuntimePlayerContext("", null));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void ClearHistory_WithName_ClearsSingleNpc()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            var profile = CreateProfile("butler", "Butler");
            profile.historySaveFile = $"NPCDialogue/test_clear{Guid.NewGuid():N}.json";
            manager.profiles = new[] { profile };
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            try
            {
                manager.SwitchToNPCAsync("butler").GetAwaiter().GetResult();
                List<DialogueEntry> history = manager.GetHistory("butler");
                Assert.That(history, Is.Empty);

                manager.ClearHistory("butler");
                Assert.DoesNotThrow(() => manager.ClearHistory(""));
                Assert.DoesNotThrow(() => manager.ClearHistory("nonexistent"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CaptureAndApplyHistorySnapshot_RoundTrips()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            var profile = CreateProfile("butler", "Butler");
            manager.profiles = new[] { profile };
            manager.persistHistory = true;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            try
            {
                manager.SwitchToNPCAsync("butler").GetAwaiter().GetResult();

                var snapshot = manager.CaptureHistorySnapshot();
                Assert.That(snapshot, Contains.Key("butler"));

                var modified = new Dictionary<string, List<DialogueEntry>>
                {
                    ["butler"] = new List<DialogueEntry>
                    {
                        new DialogueEntry("user", "Hello"),
                        new DialogueEntry("assistant", "Hi")
                    }
                };
                manager.ApplyHistorySnapshot(modified);

                List<DialogueEntry> loaded = manager.GetHistory("butler");
                Assert.That(loaded, Has.Count.EqualTo(2));
                Assert.That(loaded[0].content, Is.EqualTo("Hello"));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void InitializeAsync_CanBeCalledMultipleTimes()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            manager.profiles = Array.Empty<NPCProfile>();
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            try
            {
                var first = manager.InitializeAsync();
                var second = manager.InitializeAsync();
                Assert.That(first, Is.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SwitchToNPCAsync_FindsProfileByDisplayName()
        {
            var managerObject = new GameObject(nameof(NPCDialogueManagerTests));
            var manager = managerObject.AddComponent<NPCDialogueManager>();
            AttachMinimalChatClient(managerObject, manager);
            var profile = CreateProfile("chef", "Chef");
            manager.profiles = new[] { profile };
            manager.persistHistory = false;
            manager.enableRAG = false;
            manager.initializeOnStart = false;

            try
            {
                manager.SwitchToNPCAsync("Chef").GetAwaiter().GetResult();
                Assert.That(manager.currentProfile, Is.SameAs(profile));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
            }
        }

        static NPCProfile CreateProfile(string slug, string displayName)
        {
            var profile = ScriptableObject.CreateInstance<NPCProfile>();
            profile.npcSlug = slug;
            profile.displayName = displayName;
            profile.systemPrompt = "You are a helpful NPC.";
            profile.maxTokens = 64;
            profile.ragResults = 1;
            profile.historySaveFile = $"NPCDialogue/{slug}.json";
            return profile;
        }

        static void AttachMinimalChatClient(GameObject managerObject, NPCDialogueManager manager)
        {
            manager.chatClient = managerObject.AddComponent<NPCLocalAIClient>();
            manager.chatClient.numRetries = 0;
            manager.chatClient.host = "127.0.0.1";
            manager.chatClient.port = 19999;
        }
    }
}
