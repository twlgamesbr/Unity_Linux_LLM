using System.Threading.Tasks;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(-1000)]
    public class NPCDialogueBootstrapper : MonoBehaviour
    {
        public NPCDialogueManager dialogueManager;
        public bool autoSelectDefaultNPC = true;
        public string defaultNpcSlug = "";

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
        }
    }
}
