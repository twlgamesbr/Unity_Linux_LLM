using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NPCSystem
{
    public static class NPCRAGImporter
    {
        public const int MaxChunkCharacters = 1200;

        static NPCFlowLogger Logger => NPCFlowLogger.FindOrCreate();

        public static async Task<bool> RebuildAsync(
            NPCLocalRAG localRag,
            IEnumerable<NPCProfile> profiles,
            string ragEmbeddingPath
        )
        {
            if (localRag == null)
            {
                Logger.Log(
                    NPCFlowStage.LocalRagReady,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Warning,
                    "RAG reference is missing; import skipped.",
                    source: nameof(NPCRAGImporter)
                );
                return false;
            }

            if (profiles == null)
            {
                Logger.Log(
                    NPCFlowStage.LocalRagReady,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Warning,
                    "No NPC profiles were provided; import skipped.",
                    source: nameof(NPCRAGImporter)
                );
                return false;
            }

            localRag.Clear();
            NPCProfile[] profileArray =
                profiles as NPCProfile[] ?? new List<NPCProfile>(profiles).ToArray();
            NPCRAGMetadata metadata = NPCRAGMetadataStore.CreateExpected(
                ragEmbeddingPath,
                profileArray,
                MaxChunkCharacters
            );
            int importedProfileCount = 0;
            int importedChunkCount = 0;

            foreach (NPCProfile profile in profileArray)
            {
                if (profile == null)
                    continue;

                string relativePath = profile.GetKnowledgeSourcePath();
                string assetPath = ResolveStreamingAssetPath(relativePath);
                if (!File.Exists(assetPath))
                {
                    Logger.Log(
                        NPCFlowStage.LocalRagReady,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Knowledge file missing for {profile.GetDisplayName()}: {assetPath}",
                        source: nameof(NPCRAGImporter),
                        npcSlug: profile.GetNpcSlug(),
                        data: new Dictionary<string, object> { ["assetPath"] = assetPath }
                    );
                    continue;
                }

                string knowledgeText = File.ReadAllText(assetPath).Trim();
                if (string.IsNullOrWhiteSpace(knowledgeText))
                {
                    Logger.Log(
                        NPCFlowStage.LocalRagReady,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Knowledge file for {profile.GetDisplayName()} is empty.",
                        source: nameof(NPCRAGImporter),
                        npcSlug: profile.GetNpcSlug()
                    );
                    continue;
                }

                if (knowledgeText.Length > MaxChunkCharacters)
                {
                    knowledgeText = knowledgeText.Substring(0, MaxChunkCharacters);
                }

                int chunkKey = await localRag.Add(knowledgeText, profile.GetRagCategory());
                SetSourceChunkCount(metadata, profile.GetNpcSlug(), 1);
                importedProfileCount++;
                importedChunkCount++;
            }

            if (importedProfileCount > 0)
            {
                EnsureSaveDirectoryExists(ragEmbeddingPath);
                localRag.SaveFile(ragEmbeddingPath);
                metadata.chunkCount = importedChunkCount;
                NPCRAGMetadataStore.Save(ragEmbeddingPath, metadata);

                Logger.Log(
                    NPCFlowStage.LocalRagReady,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    $"Import complete: {importedProfileCount} profile(s), {importedChunkCount} chunk(s).",
                    source: nameof(NPCRAGImporter),
                    data: new Dictionary<string, object>
                    {
                        ["importedProfiles"] = importedProfileCount,
                        ["importedChunks"] = importedChunkCount,
                        ["ragEmbeddingPath"] = ragEmbeddingPath,
                    }
                );
                return true;
            }

            Logger.Log(
                NPCFlowStage.LocalRagReady,
                NPCFlowStatus.Skipped,
                NPCFlowLogLevel.Debug,
                "Import skipped: no profiles had valid knowledge files.",
                source: nameof(NPCRAGImporter)
            );
            return false;
        }

        static async Task<int> ImportProfileKnowledgeAsync(
            NPCLocalRAG localRag,
            NPCProfile profile,
            string knowledgeText
        )
        {
            if (localRag == null || profile == null || string.IsNullOrWhiteSpace(knowledgeText))
                return 0;

            string chunk =
                knowledgeText.Length > MaxChunkCharacters
                    ? knowledgeText.Substring(0, MaxChunkCharacters)
                    : knowledgeText;

            await localRag.Add(chunk, profile.GetRagCategory());
            return 1;
        }

        public static IEnumerable<string> ChunkTextByMaxSize(
            string text,
            int maxChars = MaxChunkCharacters
        )
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            StringBuilder current = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.Length == 0)
                    continue;

                if (current.Length + trimmedLine.Length + 1 > maxChars && current.Length > 0)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }

                if (current.Length > 0)
                    current.Append(' ');
                current.Append(trimmedLine);
            }

            if (current.Length > 0)
            {
                yield return current.ToString().Trim();
            }
        }

        public static string ResolveStreamingAssetPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            string normalized = relativePath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
                return normalized.Replace('\\', '/');
            return NPCSearchable.ResolveAssetPath(normalized);
        }

        static void EnsureSaveDirectoryExists(string ragEmbeddingPath)
        {
            if (string.IsNullOrWhiteSpace(ragEmbeddingPath))
                return;

            string resolvedPath = NPCSearchable.ResolveAssetPath(
                ragEmbeddingPath.Trim().Replace('\\', '/')
            );
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        static void SetSourceChunkCount(NPCRAGMetadata metadata, string npcSlug, int chunkCount)
        {
            if (metadata == null || metadata.sources == null)
                return;

            foreach (NPCRAGSourceMetadata source in metadata.sources)
            {
                if (
                    source != null
                    && string.Equals(source.npcSlug, npcSlug, StringComparison.OrdinalIgnoreCase)
                )
                {
                    source.chunkCount = chunkCount;
                    return;
                }
            }
        }
    }
}
