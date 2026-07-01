using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace NPCSystem
{
    public static class NPCRAGImporter
    {
        public const int MaxChunkCharacters = 1200;

        static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

        public static async Task<bool> RebuildAsync(RAG rag, IEnumerable<NPCProfile> profiles, string ragEmbeddingPath)
        {
            if (rag == null)
            {
                Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    "RAG reference is missing; import skipped.", source: nameof(NPCRAGImporter));
                return false;
            }

            if (profiles == null)
            {
                Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    "No NPC profiles were provided; import skipped.", source: nameof(NPCRAGImporter));
                return false;
            }

            rag.Clear();
            NPCProfile[] profileArray = profiles as NPCProfile[] ?? new List<NPCProfile>(profiles).ToArray();
            NPCRAGMetadata metadata = NPCRAGMetadataStore.CreateExpected(
                ragEmbeddingPath,
                GetEmbeddingLLM(rag),
                profileArray,
                MaxChunkCharacters
            );
            int importedProfileCount = 0;
            int importedChunkCount = 0;

            foreach (NPCProfile profile in profileArray)
            {
                if (profile == null) continue;

                string relativePath = profile.GetKnowledgeSourcePath();
                string assetPath = ResolveStreamingAssetPath(relativePath);
                if (!File.Exists(assetPath))
                {
                    Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                        $"Knowledge file missing for {profile.GetDisplayName()}: {assetPath}",
                        source: nameof(NPCRAGImporter), npcSlug: profile.GetNpcSlug(),
                        data: new Dictionary<string, object> { ["assetPath"] = assetPath });
                    continue;
                }

                string knowledgeText = File.ReadAllText(assetPath).Trim();
                if (string.IsNullOrWhiteSpace(knowledgeText))
                {
                    Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                        $"Knowledge file is empty for {profile.GetDisplayName()}: {assetPath}",
                        source: nameof(NPCRAGImporter), npcSlug: profile.GetNpcSlug(),
                        data: new Dictionary<string, object> { ["assetPath"] = assetPath });
                    continue;
                }

                try
                {
                    int profileChunkCount = await ImportProfileKnowledgeAsync(rag, profile, knowledgeText);
                    if (profileChunkCount > 0)
                    {
                        importedProfileCount++;
                        importedChunkCount += profileChunkCount;
                        SetSourceChunkCount(metadata, profile.GetNpcSlug(), profileChunkCount);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                        $"Failed to import knowledge for {profile.GetDisplayName()}: {e.Message}",
                        source: nameof(NPCRAGImporter), npcSlug: profile.GetNpcSlug(),
                        data: new Dictionary<string, object>
                        {
                            ["exceptionType"] = e.GetType().Name,
                            ["exceptionMessage"] = e.Message
                        });
                }
            }

            if (importedChunkCount == 0)
            {
                Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                    "No NPC knowledge documents were imported.",
                    source: nameof(NPCRAGImporter));
                return false;
            }

            EnsureSaveDirectoryExists(ragEmbeddingPath);
            metadata.sourceCount = importedProfileCount;
            metadata.chunkCount = importedChunkCount;
            metadata.builtAtUtc = DateTime.UtcNow.ToString("o");
            rag.Save(ragEmbeddingPath);
            NPCRAGMetadataStore.Save(ragEmbeddingPath, metadata);

            Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                $"Imported {importedChunkCount} knowledge chunk(s) for {importedProfileCount} NPC profile(s) into {ragEmbeddingPath}",
                source: nameof(NPCRAGImporter),
                data: new Dictionary<string, object>
                {
                    ["chunkCount"] = importedChunkCount,
                    ["profileCount"] = importedProfileCount,
                    ["ragEmbeddingPath"] = ragEmbeddingPath
                });
            return true;
        }

        static async Task<int> ImportProfileKnowledgeAsync(RAG rag, NPCProfile profile, string knowledgeText)
        {
            int importedChunkCount = 0;
            string ragCategory = profile.GetRagCategory();

            foreach (string chunk in SplitKnowledge(knowledgeText))
            {
                string entry = $"{profile.GetDisplayName()} knowledge:\n{chunk}";
                if (!await HasValidEmbeddingAsync(rag, entry, profile.GetDisplayName())) continue;

                await rag.Add(entry, ragCategory);
                importedChunkCount++;
            }

            if (importedChunkCount == 0)
            {
                Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    $"No valid embedding chunks were produced for {profile.GetDisplayName()}.",
                    source: nameof(NPCRAGImporter), npcSlug: profile.GetNpcSlug());
            }

            return importedChunkCount;
        }

        static async Task<bool> HasValidEmbeddingAsync(RAG rag, string text, string displayName)
        {
            if (rag == null || rag.search == null || rag.search.llmEmbedder == null) return true;

            List<float> embedding = await rag.search.llmEmbedder.Embeddings(text);
            int expectedLength = rag.search.llmEmbedder.llm != null ? rag.search.llmEmbedder.llm.embeddingLength : 0;
            if (embedding != null && embedding.Count > 0 && (expectedLength <= 0 || embedding.Count == expectedLength)) return true;

            string size = embedding == null ? "null" : embedding.Count.ToString();
            Logger.Log(NPCFlowStage.LocalRagReady, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                $"Skipping {displayName} knowledge chunk because the embedding vector length is invalid ({size}, expected {expectedLength}).",
                source: nameof(NPCRAGImporter),
                data: new Dictionary<string, object>
                {
                    ["embeddingSize"] = size,
                    ["expectedLength"] = expectedLength
                });
            return false;
        }

        static IEnumerable<string> SplitKnowledge(string knowledgeText)
        {
            if (string.IsNullOrWhiteSpace(knowledgeText)) yield break;

            string[] lines = knowledgeText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder current = new StringBuilder(MaxChunkCharacters);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                if (line.Length > MaxChunkCharacters)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString().Trim();
                        current.Clear();
                    }

                    for (int start = 0; start < line.Length; start += MaxChunkCharacters)
                    {
                        int length = Math.Min(MaxChunkCharacters, line.Length - start);
                        yield return line.Substring(start, length).Trim();
                    }
                    continue;
                }

                if (current.Length + line.Length + 1 > MaxChunkCharacters)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }

                if (current.Length > 0) current.AppendLine();
                current.Append(line);
            }

            if (current.Length > 0)
            {
                yield return current.ToString().Trim();
            }
        }

        public static string ResolveStreamingAssetPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;
            string normalized = relativePath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized)) return normalized.Replace('\\', '/');
            return LLMUnitySetup.GetAssetPath(normalized);
        }

        static void EnsureSaveDirectoryExists(string ragEmbeddingPath)
        {
            if (string.IsNullOrWhiteSpace(ragEmbeddingPath)) return;

            string resolvedPath = LLMUnitySetup.GetAssetPath(ragEmbeddingPath.Trim().Replace('\\', '/'));
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        static LLM GetEmbeddingLLM(RAG rag)
        {
            return rag != null && rag.search != null && rag.search.llmEmbedder != null
                ? rag.search.llmEmbedder.llm
                : null;
        }

        static void SetSourceChunkCount(NPCRAGMetadata metadata, string npcSlug, int chunkCount)
        {
            if (metadata == null || metadata.sources == null) return;

            foreach (NPCRAGSourceMetadata source in metadata.sources)
            {
                if (source != null && string.Equals(source.npcSlug, npcSlug, StringComparison.OrdinalIgnoreCase))
                {
                    source.chunkCount = chunkCount;
                    return;
                }
            }
        }
    }
}
