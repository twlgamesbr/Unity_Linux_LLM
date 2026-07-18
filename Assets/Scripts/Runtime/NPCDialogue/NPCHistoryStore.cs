using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NPCSystem
{
    [Serializable]
    public class DialogueEntry
    {
        public string Role;
        public string Content;
        public string TimestampUtc;

        public DialogueEntry() { }

        public DialogueEntry(string role, string content)
        {
            this.Role = role;
            this.Content = content;
            this.TimestampUtc = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    internal class DialogueHistoryFile
    {
        public List<DialogueEntry> Entries = new List<DialogueEntry>();
    }

    public static class NPCHistoryStore
    {
        public static List<DialogueEntry> Load(string relativePath)
        {
            string fullPath = GetFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryLoad,
                        NPCFlowStatus.Skipped,
                        NPCFlowLogLevel.Info,
                        "History file does not exist; starting empty history.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object> { ["path"] = fullPath }
                    );
                return new List<DialogueEntry>();
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                DialogueHistoryFile history = JsonUtility.FromJson<DialogueHistoryFile>(json);
                List<DialogueEntry> entries =
                    history != null && history.Entries != null
                        ? history.Entries
                        : new List<DialogueEntry>();

                List<DialogueEntry> normalized = NormalizeForChatTemplate(
                    entries,
                    out int droppedCount
                );
                if (droppedCount > 0)
                {
                    NPCFlowLogger
                        .FindOrCreate()
                        .Log(
                            NPCFlowStage.HistoryLoad,
                            NPCFlowStatus.Warning,
                            NPCFlowLogLevel.Warning,
                            $"Repaired history '{fullPath}' by dropping {droppedCount} malformed entr{(droppedCount == 1 ? "y" : "ies")}.",
                            source: nameof(NPCHistoryStore),
                            data: new Dictionary<string, object>
                            {
                                ["path"] = fullPath,
                                ["droppedCount"] = droppedCount,
                                ["entryCount"] = normalized.Count,
                            }
                        );
                    Save(relativePath, normalized);
                }

                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryLoad,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Info,
                        "History loaded.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object>
                        {
                            ["path"] = fullPath,
                            ["entryCount"] = normalized.Count,
                        }
                    );

                return normalized;
            }
            catch (Exception ex)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryLoad,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Warning,
                        $"Failed to load history '{fullPath}'.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object>
                        {
                            ["path"] = fullPath,
                            ["exceptionType"] = ex.GetType().Name,
                            ["exceptionMessage"] = ex.Message,
                        }
                    );
                return new List<DialogueEntry>();
            }
        }

        public static void Save(string relativePath, List<DialogueEntry> entries)
        {
            string fullPath = GetFullPath(relativePath);
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                DialogueHistoryFile history = new DialogueHistoryFile
                {
                    Entries = NormalizeForChatTemplate(entries, out _),
                };

                File.WriteAllText(fullPath, JsonUtility.ToJson(history, true));
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Info,
                        "History saved.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object>
                        {
                            ["path"] = fullPath,
                            ["entryCount"] = history.Entries.Count,
                        }
                    );
            }
            catch (Exception ex)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Warning,
                        $"Failed to save history '{fullPath}'.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object>
                        {
                            ["path"] = fullPath,
                            ["exceptionType"] = ex.GetType().Name,
                            ["exceptionMessage"] = ex.Message,
                        }
                    );
            }
        }

        public static void Delete(string relativePath)
        {
            string fullPath = GetFullPath(relativePath);
            if (!File.Exists(fullPath))
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Skipped,
                        NPCFlowLogLevel.Info,
                        "History delete skipped because file does not exist.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object> { ["path"] = fullPath }
                    );
                return;
            }

            try
            {
                File.Delete(fullPath);
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Info,
                        "History deleted.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object> { ["path"] = fullPath }
                    );
            }
            catch (Exception ex)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    .Log(
                        NPCFlowStage.HistoryPersist,
                        NPCFlowStatus.Error,
                        NPCFlowLogLevel.Warning,
                        $"Failed to delete history '{fullPath}'.",
                        source: nameof(NPCHistoryStore),
                        data: new Dictionary<string, object>
                        {
                            ["path"] = fullPath,
                            ["exceptionType"] = ex.GetType().Name,
                            ["exceptionMessage"] = ex.Message,
                        }
                    );
            }
        }

        public static string GetFullPath(string relativePath)
        {
            string safeRelativePath = string.IsNullOrWhiteSpace(relativePath)
                ? "NPCDialogue/default.json"
                : relativePath.Trim().Replace('\\', '/');
            return Path.Combine(Application.persistentDataPath, safeRelativePath)
                .Replace('\\', '/');
        }

        public static List<DialogueEntry> NormalizeForChatTemplate(
            List<DialogueEntry> entries,
            out int droppedCount
        )
        {
            List<DialogueEntry> normalized = new List<DialogueEntry>();
            droppedCount = 0;
            string expectedRole = "user";

            foreach (DialogueEntry entry in entries ?? new List<DialogueEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Content))
                {
                    droppedCount++;
                    continue;
                }

                string role = NormalizeRole(entry.Role);
                if (role == null || !string.Equals(role, expectedRole, StringComparison.Ordinal))
                {
                    droppedCount++;
                    continue;
                }

                entry.Role = role;
                entry.Content = entry.Content.Trim();
                normalized.Add(entry);
                expectedRole = string.Equals(expectedRole, "user", StringComparison.Ordinal)
                    ? "assistant"
                    : "user";
            }

            if (normalized.Count % 2 != 0)
            {
                normalized.RemoveAt(normalized.Count - 1);
                droppedCount++;
            }

            return normalized;
        }

        static string NormalizeRole(string role)
        {
            if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                return "assistant";
            if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                return "user";
            return null;
        }
    }
}
