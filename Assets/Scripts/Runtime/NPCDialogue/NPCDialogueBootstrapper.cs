using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(-1000)]
    public class NPCDialogueBootstrapper : MonoBehaviour
    {
        public NPCDialogueManager dialogueManager;
        public bool preloadConfiguredLoras = false;
        public bool autoSelectDefaultNPC = true;
        public string defaultNpcSlug = "";
        public bool warmupAfterSelection = false;

        void Awake()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            if (dialogueManager == null)
            {
                dialogueManager = GetComponent<NPCDialogueManager>();
                if (dialogueManager == null)
                {
                    dialogueManager = FindAnyObjectByType<NPCDialogueManager>(FindObjectsInactive.Include);
                }
            }

            logger.Log(NPCFlowStage.SceneBootstrap, dialogueManager != null ? NPCFlowStatus.Success : NPCFlowStatus.Warning,
                dialogueManager != null ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Warning,
                dialogueManager != null ? "Bootstrapper resolved dialogue manager." : "Bootstrapper could not resolve dialogue manager.",
                source: nameof(NPCDialogueBootstrapper));

            if (preloadConfiguredLoras)
            {
                PreloadConfiguredLoras();
            }
        }

        async void Start()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            if (dialogueManager == null)
            {
                logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    "Bootstrapper start skipped because dialogue manager is missing.", source: nameof(NPCDialogueBootstrapper));
                return;
            }

            await dialogueManager.InitializeAsync();
            logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                "Dialogue manager initialized from bootstrapper.", source: nameof(NPCDialogueBootstrapper));

            if (!autoSelectDefaultNPC || dialogueManager.currentProfile != null)
            {
                logger.Log(NPCFlowStage.NPCSwitch, NPCFlowStatus.Skipped, NPCFlowLogLevel.Info,
                    "Default NPC auto-select skipped.", source: nameof(NPCDialogueBootstrapper));
                return;
            }

            string npcKey = string.IsNullOrWhiteSpace(defaultNpcSlug)
                ? dialogueManager.GetDefaultProfileSlug()
                : defaultNpcSlug.Trim();

            if (string.IsNullOrWhiteSpace(npcKey))
            {
                logger.Log(NPCFlowStage.NPCSwitch, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    "Default NPC auto-select skipped because no profile slug was available.", source: nameof(NPCDialogueBootstrapper));
                return;
            }

            await dialogueManager.SwitchToNPCAsync(npcKey);
            logger.Log(NPCFlowStage.NPCSwitch, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                "Default NPC selected by bootstrapper.", source: nameof(NPCDialogueBootstrapper), npcSlug: npcKey);
            if (warmupAfterSelection)
            {
                await dialogueManager.WarmupCurrentNPCAsync();
                logger.Log(NPCFlowStage.LLMChat, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                    "Warmup completed after default selection.", source: nameof(NPCDialogueBootstrapper), npcSlug: npcKey);
            }
        }

        void PreloadConfiguredLoras()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            if (dialogueManager == null || dialogueManager.llm == null)
            {
                logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Skipped, NPCFlowLogLevel.Warning,
                    "LoRA preload skipped because dialogue manager or LLM is missing.", source: nameof(NPCDialogueBootstrapper));
                return;
            }
            if (dialogueManager.llm.started)
            {
                logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Warning, NPCFlowLogLevel.Warning,
                    "LoRA preload skipped because the LLM is already started.", source: nameof(NPCDialogueBootstrapper),
                    data: new Dictionary<string, object> { ["reason"] = "LLM already started" });
                return;
            }

            HashSet<string> loraPaths = new HashSet<string>(
                dialogueManager.Profiles
                    .Where(profile => profile != null)
                    .Select(profile => profile.GetLoraAdapterPath())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
            );

            foreach (string loraPath in loraPaths)
            {
                dialogueManager.llm.AddLora(loraPath, 0f);
            }

            dialogueManager.RegisterPreloadedLoras(loraPaths);
            logger.Log(NPCFlowStage.SceneBootstrap, NPCFlowStatus.Success, NPCFlowLogLevel.Info,
                "LoRA preload pass finished.", source: nameof(NPCDialogueBootstrapper), data: new Dictionary<string, object>
                {
                    ["loraCount"] = loraPaths.Count
                });
        }
    }
}
