using System;
using System.Collections.Generic;
using System.IO;
using NPCSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NPCSystem.Editor
{
    public static class MysterySceneTemplateGenerator
{
    const string DefaultTemplatePath = "Assets/MysteryTemplates/welltodo-style-mystery-template.json";
    const string DefaultPrototypeScenePath = "Assets/Scenes/NPCDialoguePrototype.unity";

    [MenuItem("Tools/Mystery Scenes/Generate From Template JSON...")]
    public static void GenerateFromTemplatePicker()
    {
        string selectedPath = EditorUtility.OpenFilePanel(
            "Mystery Scene Template",
            Path.Combine(Application.dataPath, "MysteryTemplates"),
            "json"
        );

        if (string.IsNullOrWhiteSpace(selectedPath)) return;
        GenerateFromTemplate(ToAssetPath(selectedPath));
    }

    [MenuItem("Tools/Mystery Scenes/Generate Example Template")]
    public static void GenerateDefaultTemplate()
    {
        GenerateFromTemplate(DefaultTemplatePath);
    }

    public static void GenerateFromTemplate(string templateAssetPath)
    {
        NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Start, NPCFlowLogLevel.Info,
            "Mystery scene generation started.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
            {
                ["templateAssetPath"] = templateAssetPath
            });

        if (string.IsNullOrWhiteSpace(templateAssetPath) || !File.Exists(templateAssetPath))
        {
            Debug.LogError($"[MysterySceneTemplateGenerator] Template not found: {templateAssetPath}");
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                "Mystery scene generation failed: template not found.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
                {
                    ["templateAssetPath"] = templateAssetPath
                });
            return;
        }

        MysterySceneTemplate template = JsonUtility.FromJson<MysterySceneTemplate>(File.ReadAllText(templateAssetPath));
        if (!Validate(template)) return;

        string caseSlug = SanitizeSlug(template.caseSlug);
        string prototypeScenePath = string.IsNullOrWhiteSpace(template.prototypeScenePath)
            ? DefaultPrototypeScenePath
            : template.prototypeScenePath;
        string outputScenePath = string.IsNullOrWhiteSpace(template.outputScenePath)
            ? $"Assets/Scenes/GeneratedMysteries/{caseSlug}.unity"
            : template.outputScenePath;

        if (!File.Exists(prototypeScenePath))
        {
            Debug.LogError($"[MysterySceneTemplateGenerator] Prototype scene not found: {prototypeScenePath}");
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                "Mystery scene generation failed: prototype scene not found.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
                {
                    ["prototypeScenePath"] = prototypeScenePath
                });
            return;
        }

        EnsureAssetDirectory(Path.GetDirectoryName(outputScenePath));
        if (File.Exists(outputScenePath))
        {
            Debug.LogError($"[MysterySceneTemplateGenerator] Output scene already exists: {outputScenePath}");
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                "Mystery scene generation failed: output scene already exists.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
                {
                    ["outputScenePath"] = outputScenePath
                });
            return;
        }

        if (!AssetDatabase.CopyAsset(prototypeScenePath, outputScenePath))
        {
            Debug.LogError($"[MysterySceneTemplateGenerator] Failed to copy scene to: {outputScenePath}");
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                "Mystery scene generation failed: scene copy failed.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
                {
                    ["prototypeScenePath"] = prototypeScenePath,
                    ["outputScenePath"] = outputScenePath
                });
            return;
        }

        AssetDatabase.Refresh();

        NPCProfile[] profiles = CreateProfiles(template, caseSlug);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(outputScenePath);
        ConfigureScene(scene, template, caseSlug, profiles);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MysterySceneTemplateGenerator] Generated mystery scene '{template.displayName}' at {outputScenePath}");
        NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Success, NPCFlowLogLevel.Info,
            "Mystery scene generation completed.", nameof(MysterySceneTemplateGenerator), new Dictionary<string, object>
            {
                ["caseSlug"] = caseSlug,
                ["profileCount"] = profiles.Length,
                ["outputScenePath"] = outputScenePath
            });
    }

    static NPCProfile[] CreateProfiles(MysterySceneTemplate template, string caseSlug)
    {
        string profileRoot = $"Assets/Data/MysteryScenes/{caseSlug}/NPCProfiles";
        EnsureAssetDirectory(profileRoot);

        List<NPCProfile> profiles = new List<NPCProfile>();
        foreach (MysteryNpcTemplate npc in template.npcs)
        {
            string npcSlug = SanitizeSlug(npc.slug);
            string knowledgePath = WriteKnowledgeFile(caseSlug, npcSlug, npc.knowledgeMarkdown);
            string profilePath = $"{profileRoot}/{npcSlug}.asset";

            NPCProfile profile = ScriptableObject.CreateInstance<NPCProfile>();
            profile.npcSlug = npcSlug;
            profile.displayName = string.IsNullOrWhiteSpace(npc.displayName) ? npcSlug : npc.displayName;
            profile.portraitTexture = LoadPortrait(npc.portraitAssetPath);
            profile.systemPrompt = npc.systemPrompt;
            profile.temperature = npc.temperature <= 0f ? 0.75f : npc.temperature;
            profile.topP = npc.topP <= 0f ? 0.9f : npc.topP;
            profile.minP = npc.minP < 0f ? 0.05f : npc.minP;
            profile.topK = npc.topK <= 0 ? 40 : npc.topK;
            profile.repeatPenalty = npc.repeatPenalty <= 0f ? 1.1f : npc.repeatPenalty;
            profile.maxTokens = npc.maxTokens <= 0 ? 180 : npc.maxTokens;
            profile.ragCategory = $"{caseSlug}-{npcSlug}";
            profile.ragResults = npc.ragResults <= 0 ? 3 : npc.ragResults;
            profile.knowledgeSourcePath = knowledgePath;
            profile.loraAdapterPath = string.IsNullOrWhiteSpace(npc.loraAdapterPath)
                ? $"NPCs/{caseSlug}/{npcSlug}/adapter.gguf"
                : npc.loraAdapterPath;
            profile.loraWeight = npc.loraWeight <= 0f ? 0.8f : npc.loraWeight;
            profile.historySaveFile = $"NPCDialogue/{caseSlug}/{npcSlug}.json";

            AssetDatabase.CreateAsset(profile, profilePath);
            profiles.Add(profile);
        }

        return profiles.ToArray();
    }

    static void ConfigureScene(UnityEngine.SceneManagement.Scene scene, MysterySceneTemplate template, string caseSlug, NPCProfile[] profiles)
    {
        NPCDialogueManager dialogueManager = UnityEngine.Object.FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
        if (dialogueManager == null)
        {
            Debug.LogError("[MysterySceneTemplateGenerator] Scene has no NPCDialogueManager.");
            NPCFlowLogger.LogEditorWorkflow(NPCFlowStatus.Error, NPCFlowLogLevel.Error,
                "Mystery scene configuration failed: no NPCDialogueManager.", nameof(MysterySceneTemplateGenerator));
            return;
        }

        dialogueManager.profiles = profiles;
        dialogueManager.ragEmbeddingPath = string.IsNullOrWhiteSpace(template.ragEmbeddingPath)
            ? $"RAG/{caseSlug}/NPCDialogues-minilm-chunked.rag"
            : template.ragEmbeddingPath;

        NPCDialogueBootstrapper bootstrapper = UnityEngine.Object.FindAnyObjectByType<NPCDialogueBootstrapper>(FindObjectsInactive.Include);
        if (bootstrapper != null && profiles.Length > 0)
        {
            bootstrapper.dialogueManager = dialogueManager;
            bootstrapper.defaultNpcSlug = profiles[0].GetNpcSlug();
        }

        NPCDialogueUIController ui = UnityEngine.Object.FindAnyObjectByType<NPCDialogueUIController>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.dialogueManager = dialogueManager;
            EditorUtility.SetDirty(ui);
        }

        NotebookUIController notebook = UnityEngine.Object.FindAnyObjectByType<NotebookUIController>(FindObjectsInactive.Include);
        if (notebook != null)
        {
            notebook.correctAnswer1 = template.correctAnswers.culprit;
            notebook.correctAnswer2 = template.correctAnswers.location;
            notebook.correctAnswer3 = template.correctAnswers.evidence;
            SetDropdownOptions(notebook.answer1, template.choices.culprits);
            SetDropdownOptions(notebook.answer2, template.choices.locations);
            SetDropdownOptions(notebook.answer3, template.choices.evidence);
            EditorUtility.SetDirty(notebook);
        }

        EditorUtility.SetDirty(dialogueManager);
        if (bootstrapper != null) EditorUtility.SetDirty(bootstrapper);
    }

    static string WriteKnowledgeFile(string caseSlug, string npcSlug, string knowledgeMarkdown)
    {
        string relativePath = $"NPCs/{caseSlug}/{npcSlug}/knowledge.md";
        string assetPath = $"Assets/StreamingAssets/{relativePath}";
        EnsureAssetDirectory(Path.GetDirectoryName(assetPath));

        string body = string.IsNullOrWhiteSpace(knowledgeMarkdown)
            ? $"# {npcSlug} knowledge\n\nAdd clues, alibis, contradictions, and suggested answers here.\n"
            : knowledgeMarkdown.Replace("\\n", "\n");

        File.WriteAllText(assetPath, body);
        return relativePath;
    }

    static Texture2D LoadPortrait(string portraitAssetPath)
    {
        if (string.IsNullOrWhiteSpace(portraitAssetPath)) return null;
        return AssetDatabase.LoadAssetAtPath<Texture2D>(portraitAssetPath);
    }

    static void SetDropdownOptions(Dropdown dropdown, string[] options)
    {
        if (dropdown == null || options == null || options.Length == 0) return;

        dropdown.options.Clear();
        foreach (string option in options)
        {
            if (!string.IsNullOrWhiteSpace(option))
            {
                dropdown.options.Add(new Dropdown.OptionData(option.Trim()));
            }
        }

        dropdown.value = 0;
        dropdown.RefreshShownValue();
        EditorUtility.SetDirty(dropdown);
    }

    static bool Validate(MysterySceneTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.caseSlug))
        {
            Debug.LogError("[MysterySceneTemplateGenerator] Template must define caseSlug.");
            return false;
        }

        if (template.npcs == null || template.npcs.Length == 0)
        {
            Debug.LogError("[MysterySceneTemplateGenerator] Template must define at least one NPC.");
            return false;
        }

        if (template.correctAnswers == null
            || string.IsNullOrWhiteSpace(template.correctAnswers.culprit)
            || string.IsNullOrWhiteSpace(template.correctAnswers.location)
            || string.IsNullOrWhiteSpace(template.correctAnswers.evidence))
        {
            Debug.LogError("[MysterySceneTemplateGenerator] Template must define culprit, location, and evidence answers.");
            return false;
        }

        if (template.choices == null)
        {
            Debug.LogError("[MysterySceneTemplateGenerator] Template must define solve choices.");
            return false;
        }

        return true;
    }

    static void EnsureAssetDirectory(string assetDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory) || AssetDatabase.IsValidFolder(assetDirectory)) return;

        string[] parts = assetDirectory.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    static string SanitizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "mystery";

        string lower = value.Trim().ToLowerInvariant();
        char[] chars = lower.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            bool valid = chars[i] >= 'a' && chars[i] <= 'z' || chars[i] >= '0' && chars[i] <= '9';
            if (!valid) chars[i] = '-';
        }
        return new string(chars).Trim('-');
    }

    static string ToAssetPath(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string normalized = absolutePath.Replace('\\', '/');
        return normalized.StartsWith(dataPath, StringComparison.Ordinal)
            ? $"Assets{normalized.Substring(dataPath.Length)}"
            : normalized;
    }

    [Serializable]
    public class MysterySceneTemplate
    {
        public string caseSlug;
        public string displayName;
        public string prototypeScenePath;
        public string outputScenePath;
        public string ragEmbeddingPath;
        public MysterySolveAnswers correctAnswers;
        public MysterySolveChoices choices;
        public MysteryNpcTemplate[] npcs;
    }

    [Serializable]
    public class MysterySolveAnswers
    {
        public string culprit;
        public string location;
        public string evidence;
    }

    [Serializable]
    public class MysterySolveChoices
    {
        public string[] culprits;
        public string[] locations;
        public string[] evidence;
    }

    [Serializable]
    public class MysteryNpcTemplate
    {
        public string slug;
        public string displayName;
        public string portraitAssetPath;
        public string systemPrompt;
        public float temperature = 0.75f;
        public float topP = 0.9f;
        public float minP = 0.05f;
        public int topK = 40;
        public float repeatPenalty = 1.1f;
        public int maxTokens = 180;
        public int ragResults = 3;
        public string loraAdapterPath;
        public float loraWeight = 0.8f;
        [TextArea(8, 24)]
        public string knowledgeMarkdown;
    }
}
}
