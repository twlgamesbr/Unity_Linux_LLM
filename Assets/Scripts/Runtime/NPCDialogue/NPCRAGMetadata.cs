using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NPCSystem
{
    [Serializable]
    public class NPCRAGMetadata
    {
        public string importerVersion;
        public string ragPath;
        public string embeddingModel;
        public int embeddingLength;
        public int chunkCharacters;
        public int sourceCount;
        public int chunkCount;
        public string builtAtUtc;
        public List<NPCRAGSourceMetadata> sources = new List<NPCRAGSourceMetadata>();
    }

    [Serializable]
    public class NPCRAGSourceMetadata
    {
        public string npcSlug;
        public string displayName;
        public string sourcePath;
        public string sha256;
        public long byteLength;
        public int chunkCount;
    }

    public static class NPCRAGMetadataStore
    {
        public const string ImporterVersion = "npc-rag-importer-v2";

        public static string GetMetadataPath(string ragEmbeddingPath)
        {
            if (string.IsNullOrWhiteSpace(ragEmbeddingPath))
                return string.Empty;
            return NPCSearchable.ResolveAssetPath(
                ragEmbeddingPath.Trim().Replace('\\', '/') + ".json"
            );
        }

        public static NPCRAGMetadata CreateExpected(
            string ragEmbeddingPath,
            IEnumerable<NPCProfile> profiles,
            int chunkCharacters,
            int chunkCount = 0
        )
        {
            NPCRAGMetadata metadata = new NPCRAGMetadata
            {
                importerVersion = ImporterVersion,
                ragPath = NormalizePath(ragEmbeddingPath),
                embeddingModel = "localai-embedding",
                embeddingLength = 0,
                chunkCharacters = chunkCharacters,
                chunkCount = chunkCount,
                builtAtUtc = DateTime.UtcNow.ToString("o"),
            };

            if (profiles != null)
            {
                foreach (NPCProfile profile in profiles)
                {
                    if (profile == null)
                        continue;

                    string sourcePath = NPCRAGImporter.ResolveStreamingAssetPath(
                        profile.GetKnowledgeSourcePath()
                    );
                    NPCRAGSourceMetadata source = new NPCRAGSourceMetadata
                    {
                        npcSlug = profile.GetNpcSlug(),
                        displayName = profile.GetDisplayName(),
                        sourcePath = NormalizePath(profile.GetKnowledgeSourcePath()),
                        sha256 = File.Exists(sourcePath) ? ComputeSha256(sourcePath) : string.Empty,
                        byteLength = File.Exists(sourcePath) ? new FileInfo(sourcePath).Length : 0,
                    };
                    metadata.sources.Add(source);
                }
            }

            metadata.sourceCount = metadata.sources.Count;
            return metadata;
        }

        public static bool TryLoad(string ragEmbeddingPath, out NPCRAGMetadata metadata)
        {
            metadata = null;
            string metadataPath = GetMetadataPath(ragEmbeddingPath);
            if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
                return false;

            try
            {
                metadata = JsonUtility.FromJson<NPCRAGMetadata>(File.ReadAllText(metadataPath));
                return metadata != null;
            }
            catch (Exception e)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.LocalRagReady,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Warning,
                        $"Failed to read metadata '{metadataPath}'.",
                        source: nameof(NPCRAGMetadataStore),
                        data: new Dictionary<string, object>
                        {
                            ["metadataPath"] = metadataPath,
                            ["exceptionType"] = e.GetType().Name,
                            ["exceptionMessage"] = e.Message,
                        }
                    );
                return false;
            }
        }

        public static void Save(string ragEmbeddingPath, NPCRAGMetadata metadata)
        {
            if (metadata == null)
                return;

            string metadataPath = GetMetadataPath(ragEmbeddingPath);
            string directory = Path.GetDirectoryName(metadataPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
        }

        public static bool IsCurrent(
            NPCRAGMetadata actual,
            NPCRAGMetadata expected,
            out string reason
        )
        {
            reason = string.Empty;
            if (actual == null)
            {
                reason = "metadata is missing";
                return false;
            }

            if (
                !string.Equals(
                    actual.importerVersion,
                    expected.importerVersion,
                    StringComparison.Ordinal
                )
            )
            {
                reason =
                    $"importer version changed ({actual.importerVersion} != {expected.importerVersion})";
                return false;
            }

            if (
                !string.Equals(
                    actual.embeddingModel,
                    expected.embeddingModel,
                    StringComparison.Ordinal
                )
            )
            {
                reason =
                    $"embedding model changed ({actual.embeddingModel} != {expected.embeddingModel})";
                return false;
            }

            if (actual.embeddingLength != expected.embeddingLength)
            {
                reason =
                    $"embedding length changed ({actual.embeddingLength} != {expected.embeddingLength})";
                return false;
            }

            if (actual.chunkCharacters != expected.chunkCharacters)
            {
                reason =
                    $"chunk size changed ({actual.chunkCharacters} != {expected.chunkCharacters})";
                return false;
            }

            if (actual.sources == null || actual.sources.Count != expected.sources.Count)
            {
                reason = "source file list changed";
                return false;
            }

            Dictionary<string, NPCRAGSourceMetadata> actualByPath = new Dictionary<
                string,
                NPCRAGSourceMetadata
            >(StringComparer.Ordinal);
            foreach (NPCRAGSourceMetadata source in actual.sources)
            {
                if (source != null)
                    actualByPath[source.sourcePath] = source;
            }

            foreach (NPCRAGSourceMetadata expectedSource in expected.sources)
            {
                if (expectedSource == null)
                    continue;
                if (
                    !actualByPath.TryGetValue(
                        expectedSource.sourcePath,
                        out NPCRAGSourceMetadata actualSource
                    )
                )
                {
                    reason = $"source file missing from metadata: {expectedSource.sourcePath}";
                    return false;
                }

                if (
                    !string.Equals(
                        actualSource.sha256,
                        expectedSource.sha256,
                        StringComparison.Ordinal
                    )
                )
                {
                    reason = $"source file changed: {expectedSource.sourcePath}";
                    return false;
                }
            }

            return true;
        }

        static string ComputeSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(File.ReadAllBytes(path));
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }

        static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
        }
    }
}
