using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NPCSystem.Dialogue.Core;

namespace NPCSystem.Editor
{
    public static class NPCProfileDatasetExporter
    {
        const string DefaultExportPath = "Assets/StreamingAssets/Exports/npc-profiles.json";

        [Serializable]
        class NPCProfileExportCollection
        {
            public List<NPCProfileExportItem> profiles = new List<NPCProfileExportItem>();
        }

        [Serializable]
        class NPCProfileExportItem
        {
            public string npc_slug;
            public string display_name;
            public string system_prompt;
            public string personality_brief;
            public string speaking_style;
            public string boundaries;
            public string[] preferred_action_functions;
            public string[] forbidden_action_functions;
            public string rag_category;
            public string knowledge_source_path;
            public string knowledge_text;
        }

        [MenuItem("NPC Dialogue/Export NPC Profile Dataset")]
        public static void ExportDefault()
        {
            ExportToPath(DefaultExportPath);
        }

        public static void ExportToPath(string assetRelativePath)
        {
            string[] guids = AssetDatabase.FindAssets("t:NPCProfile");
            var collection = new NPCProfileExportCollection();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                NPCProfile profile = AssetDatabase.LoadAssetAtPath<NPCProfile>(assetPath);
                if (profile == null)
                {
                    continue;
                }

                string knowledgeSourcePath = profile.GetKnowledgeSourcePath();
                string knowledgeText = string.Empty;
                string projectRootForExport =
                    Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string resolvedKnowledgePath = string.IsNullOrWhiteSpace(knowledgeSourcePath)
                    ? null
                    : Path.Combine(projectRootForExport, knowledgeSourcePath.TrimStart('/'));
                if (
                    !string.IsNullOrWhiteSpace(resolvedKnowledgePath)
                    && File.Exists(resolvedKnowledgePath)
                )
                {
                    knowledgeText = File.ReadAllText(resolvedKnowledgePath);
                }

                collection.profiles.Add(
                    new NPCProfileExportItem
                    {
                        npc_slug = profile.GetNpcSlug(),
                        display_name = profile.GetDisplayName(),
                        system_prompt = profile.SystemPrompt,
                        personality_brief = profile.PersonalityBrief,
                        speaking_style = profile.SpeakingStyle,
                        boundaries = profile.Boundaries,
                        preferred_action_functions =
                            profile.PreferredActionFunctions ?? Array.Empty<string>(),
                        forbidden_action_functions =
                            profile.ForbiddenActionFunctions ?? Array.Empty<string>(),
                        rag_category = profile.GetRagCategory(),
                        knowledge_source_path = knowledgeSourcePath,
                        knowledge_text = knowledgeText,
                    }
                );
            }

            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string outputPath = Path.Combine(
                projectRoot,
                assetRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(collection, prettyPrint: true);
            File.WriteAllText(outputPath, json);
            Debug.Log($"[NPCProfileDatasetExporter] Exported {collection.profiles.Count} profiles to {outputPath}");

            AssetDatabase.Refresh();
        }
    }
}
