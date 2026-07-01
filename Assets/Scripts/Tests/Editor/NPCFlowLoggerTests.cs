using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCFlowLoggerTests
    {
        [Test]
        public void FlowEventSerializesStableSchemaAndData()
        {
            var flowEvent = new NPCFlowEvent
            {
                sessionId = "session-test",
                requestId = "req-000001",
                npcSlug = "butler",
                source = "NPCFlowLoggerTests",
                stage = NPCFlowStage.LocalRagSearch,
                status = NPCFlowStatus.Success,
                level = NPCFlowLogLevel.Info,
                message = "Local RAG returned results",
                durationMs = 42,
                data = new Dictionary<string, object>
                {
                    ["resultCount"] = 3,
                    ["embeddingLength"] = 384
                }
            };

            string json = flowEvent.ToJson();

            Assert.That(json, Does.Contain("\"schemaVersion\":1"));
            Assert.That(json, Does.Contain("\"stage\":\"LocalRagSearch\""));
            Assert.That(json, Does.Contain("\"status\":\"Success\""));
            Assert.That(json, Does.Contain("\"resultCount\":3"));
        }

        [Test]
        public void TextSanitizerOmitsSnippetUnlessRequested()
        {
            var summary = NPCFlowTextSanitizer.SummarizeText("hello\nworld", includeSnippet: false, maxSnippetChars: 80);

            Assert.That(summary["length"], Is.EqualTo(11));
            Assert.That(summary, Does.ContainKey("sha256"));
            Assert.That(summary.ContainsKey("snippet"), Is.False);
        }

        [Test]
        public void TextSanitizerTruncatesAndNormalizesSnippet()
        {
            var summary = NPCFlowTextSanitizer.SummarizeText("hello\nworld and more", includeSnippet: true, maxSnippetChars: 11);

            Assert.That(summary["length"], Is.EqualTo(20));
            Assert.That(summary["snippet"], Is.EqualTo("hello world"));
        }

        [Test]
        public void LoggerWritesJsonlAndMaintainsRingBuffer()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "NPCFlowLoggerTests", Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("NPCFlowLoggerTests");
            var logger = gameObject.AddComponent<NPCFlowLogger>();
            logger.logToUnityConsole = false;
            logger.logToJsonlFile = true;
            logger.overrideAbsoluteLogDirectory = tempDirectory;
            logger.maxInMemoryEvents = 2;

            try
            {
                logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Start, NPCFlowLogLevel.Info, "start", source: "test", requestId: "req-1");
                logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Success, NPCFlowLogLevel.Info, "success", source: "test", requestId: "req-1");
                logger.Log(NPCFlowStage.ResponseComplete, NPCFlowStatus.Success, NPCFlowLogLevel.Info, "done", source: "test", requestId: "req-1");
                logger.Flush();

                Assert.That(logger.GetRecentEvents().Count, Is.EqualTo(2));
                Assert.That(File.Exists(logger.CurrentLogPath), Is.True);
                string[] lines = File.ReadAllLines(logger.CurrentLogPath);
                Assert.That(lines.Length, Is.EqualTo(3));
                Assert.That(lines[0], Does.Contain("SceneBootstrap"));
                Assert.That(lines[2], Does.Contain("ResponseComplete"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Test]
        public void ScopeEmitsStartAndTerminalEventsWithSameRequestId()
        {
            var gameObject = new GameObject("NPCFlowScopeTests");
            var logger = gameObject.AddComponent<NPCFlowLogger>();
            logger.logToUnityConsole = false;
            logger.logToJsonlFile = false;
            logger.maxInMemoryEvents = 10;

            try
            {
                using (var scope = NPCFlowScope.Start(logger, NPCFlowStage.LLMChat, "test", requestId: "req-scope", npcSlug: "maid"))
                {
                    scope.Success("completed");
                }

                var events = logger.GetRecentEvents();
                Assert.That(events.Count, Is.EqualTo(2));
                Assert.That(events[0].requestId, Is.EqualTo("req-scope"));
                Assert.That(events[1].requestId, Is.EqualTo("req-scope"));
                Assert.That(events[0].status, Is.EqualTo(NPCFlowStatus.Start));
                Assert.That(events[1].status, Is.EqualTo(NPCFlowStatus.Success));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
