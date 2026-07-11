using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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
            public string secret_knowledge;
            public bool can_give_puzzle_hints;
            public bool can_accuse_suspects;
            public bool can_reveal_secrets;
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
                string resolvedKnowledgePath = NPCSearchable.ResolveAssetPath(knowledgeSourcePath);
                string knowledgeText = string.Empty;
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
                        system_prompt = profile.systemPrompt,
                        personality_brief = profile.personalityBrief,
                        speaking_style = profile.speakingStyle,
                        boundaries = profile.boundaries,
                        secret_knowledge = profile.secretKnowledge,
                        can_give_puzzle_hints = profile.canGivePuzzleHints,
                        can_accuse_suspects = profile.canAccuseSuspects,
                        can_reveal_secrets = profile.canRevealSecrets,
                        preferred_action_functions =
                            profile.preferredActionFunctions ?? Array.Empty<string>(),
                        forbidden_action_functions =
                            profile.forbiddenActionFunctions ?? Array.Empty<string>(),
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
                assetRelativePath.Replace("Assets/", string.Empty)
            );
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string json = JsonUtility.ToJson(collection, true);
            File.WriteAllText(outputPath, json);
            AssetDatabase.Refresh();
            Debug.Log(
                $"[NPCProfileDatasetExporter] Exported {collection.profiles.Count} profiles to {outputPath}"
            );
        }
    }
}
