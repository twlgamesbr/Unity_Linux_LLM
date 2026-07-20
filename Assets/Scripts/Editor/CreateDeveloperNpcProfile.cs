using UnityEditor;
using UnityEngine;
using NPCSystem.Dialogue.Core;

namespace NPCSystem.Editor
{
    /// <summary>
    /// Auto-creates the Developer NPC profile ScriptableObject asset on first load.
    /// </summary>
    public static class CreateDeveloperNpcProfile
    {
        private static bool _hasRun;

        [InitializeOnLoadMethod]
        static void AutoCreate()
        {
            if (_hasRun)
                return;
            _hasRun = true;
            Create();
        }

        [MenuItem("NPC Dialogue/Create Developer NPC Profile")]
        static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NPCProfile>("Assets/Resources/NPCProfiles/DeveloperNPC.asset");
            if (existing != null)
            {
                Debug.Log("[CreateDeveloperNpcProfile] Developer NPC profile already exists. Updating fields.");
                ConfigureProfile(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return;
            }

            var profile = ScriptableObject.CreateInstance<NPCProfile>();
            profile.name = "DeveloperNPC";
            AssetDatabase.CreateAsset(profile, "Assets/Resources/NPCProfiles/DeveloperNPC.asset");
            ConfigureProfile(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateDeveloperNpcProfile] Created Developer NPC profile at Assets/Resources/NPCProfiles/DeveloperNPC.asset");
        }

        static void ConfigureProfile(NPCProfile profile)
        {
            var serialized = new SerializedObject(profile);

            serialized.FindProperty("_npcSlug").stringValue = "game-developer";
            serialized.FindProperty("_displayName").stringValue = "Game Developer";

            serialized.FindProperty("_systemPrompt").stringValue =
                "You are a veteran game developer NPC embedded inside the very game you helped build. "
                + "Your role is to guide the player through the game's codebase, explain architecture decisions, "
                + "share optimization techniques, and trade code knowledge as items.\n\n"
                + "You have access to the full codebase knowledge collection. When the player asks questions about "
                + "how something works, search your knowledge base first. If the answer isn't there, give your best "
                + "professional opinion as a senior developer.\n\n"
                + "You can trade code snippets, documentation patterns, and architecture knowledge with the player. "
                + "Use {expertiseLabel} to gauge how much detail to share — Rookies get simpler explanations, "
                + "Lead developers get deep architectural insights.\n\n"
                + "Current player: {playerName} (expertise: {expertiseLabel} level {expertiseLevel}). "
                + "Location: {currentLocation}. Time: {timeOfDay}.";

            serialized.FindProperty("_personalityBrief").stringValue =
                "Passionate about clean code, efficient architecture, and well-documented systems. "
                + "Gets excited discussing design patterns, refactoring strategies, and performance optimizations. "
                + "A bit opinionated but always constructive.";

            serialized.FindProperty("_speakingStyle").stringValue =
                "Speaks with the confidence of a senior engineer who has seen it all. "
                + "Uses technical terms but explains them when talking to junior developers. "
                + "Occasionally makes programming jokes. References real game development patterns.";

            serialized.FindProperty("_boundaries").stringValue =
                "Will not write complete code solutions — explains concepts and lets the player implement. "
                + "Will not deploy or modify the game directly. "
                + "Will not share sensitive credentials or system access information. "
                + "Stays in character as a developer NPC in a game.";

            serialized.FindProperty("_helpfulness").floatValue = 0.8f;

            serialized.FindProperty("_knowledgeSource").enumValueIndex = 0; // Qdrant
            serialized.FindProperty("_ragCategory").stringValue = "unity_linux_llm_codebase_v2";
            serialized.FindProperty("_ragResults").intValue = 5;

            serialized.FindProperty("_temperature").floatValue = 0.7f;
            serialized.FindProperty("_topP").floatValue = 0.9f;
            serialized.FindProperty("_maxTokens").intValue = 300;

            serialized.FindProperty("_preferredActionFunctions").ClearArray();
            serialized.FindProperty("_preferredActionFunctions").arraySize = 5;
            serialized.FindProperty("_preferredActionFunctions").GetArrayElementAtIndex(0).stringValue = "trade_item";
            serialized.FindProperty("_preferredActionFunctions").GetArrayElementAtIndex(1).stringValue = "explain_code";
            serialized.FindProperty("_preferredActionFunctions").GetArrayElementAtIndex(2).stringValue = "search_knowledge";
            serialized.FindProperty("_preferredActionFunctions").GetArrayElementAtIndex(3).stringValue = "suggest_refactor";
            serialized.FindProperty("_preferredActionFunctions").GetArrayElementAtIndex(4).stringValue = "review_pattern";

            serialized.ApplyModifiedProperties();
        }
    }
}
