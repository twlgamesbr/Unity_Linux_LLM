using System.Threading.Tasks;
using EditorAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DefaultExecutionOrder(-1000)]
    public class NPCDialogueBootstrapper : MonoBehaviour
    {
        [FoldoutGroup("References", true, nameof(DialogueManager))]
        [SerializeField]
        EditorAttributes.Void referencesGroup;

        [SerializeField, HideProperty, Required]
        [FormerlySerializedAs("DialogueManager")]
        public NPCDialogueManager DialogueManager;

        [FoldoutGroup(
            "Startup Behaviour",
            true,
            nameof(autoSelectDefaultNPC),
            nameof(defaultNpcSlug),
            nameof(initializeOnStart)
        )]
        [SerializeField]
        EditorAttributes.Void startupGroup;

        [SerializeField, HideProperty]
        public bool autoSelectDefaultNPC = true;

        [SerializeField, HideProperty]
        public string defaultNpcSlug = "";

        [Tooltip(
            "If true, dialogue systems initialize on Start. If false, they can be initialized on-demand (e.g., after player login success)."
        )]
        [SerializeField, HideProperty]
        public bool initializeOnStart = false;

        [Title("Runtime Status")]
        [ShowInInspector, ReadOnly]
        string ResolvedDefaultNpcSlug
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(defaultNpcSlug))
                    return defaultNpcSlug.Trim();
                return DialogueManager != null
                    ? DialogueManager.GetDefaultProfileSlug()
                    : "<no dialogue manager>";
            }
        }

        void Awake()
        {
            // Initialize Datadog metrics early in the startup sequence.
            // The Datadog Agent must be running on localhost:8125 (sidecar in Docker).
            DatadogMetricsService.Initialize();

            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            if (DialogueManager == null)
            {
                DialogueManager = GetComponent<NPCDialogueManager>();
                if (DialogueManager == null)
                {
                    DialogueManager = FindAnyObjectByType<NPCDialogueManager>(
                        FindObjectsInactive.Include
                    );
                }
            }

            logger.Log(
                NPCFlowStage.SceneBootstrap,
                DialogueManager != null ? NPCFlowStatus.Success : NPCFlowStatus.Warning,
                DialogueManager != null ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Warning,
                DialogueManager != null
                    ? "Bootstrapper resolved dialogue manager."
                    : "Bootstrapper could not resolve dialogue manager.",
                source: nameof(NPCDialogueBootstrapper)
            );
        }

        async void Start()
        {
            if (initializeOnStart)
            {
                await InitializeOnDemandAsync();
            }
        }

        private Task _onDemandInitTask;

        [Button("Run Bootstrap Now")]
        public Task InitializeOnDemandAsync()
        {
            _onDemandInitTask ??= InitializeOnDemandInternalAsync();
            return _onDemandInitTask;
        }

        async Task InitializeOnDemandInternalAsync()
        {
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            if (DialogueManager == null)
            {
                logger.Log(
                    NPCFlowStage.SceneBootstrap,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Warning,
                    "Bootstrapper start skipped because dialogue manager is missing.",
                    source: nameof(NPCDialogueBootstrapper)
                );
                return;
            }

            // Perform post-login LocalAI readiness probe
            var backendReadiness = FindAnyObjectByType<NPCBackendReadinessService>(
                FindObjectsInactive.Include
            );
            if (backendReadiness != null)
            {
                await backendReadiness.ProbeAsync(probeLocalAi: true);
            }

            await DialogueManager.InitializeAsync();
            logger.Log(
                NPCFlowStage.SceneBootstrap,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                "Dialogue manager initialized from bootstrapper on demand.",
                source: nameof(NPCDialogueBootstrapper)
            );

            // Initialize dialogue network bridge to sync baseline status post-login
            var bridge = FindAnyObjectByType<NPCDialogueNetworkBridge>(FindObjectsInactive.Include);
            if (bridge != null)
            {
                await bridge.InitializeAsync();
            }

            // Run smoke validator configuration check post-login once dialogue systems are active
            var validator = FindAnyObjectByType<NPCDialogueSmokeValidator>(
                FindObjectsInactive.Include
            );
            if (validator != null)
            {
                validator.ValidateConfiguration();
            }

            if (!autoSelectDefaultNPC || DialogueManager.currentProfile != null)
            {
                logger.Log(
                    NPCFlowStage.NPCSwitch,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Info,
                    "Default NPC auto-select skipped.",
                    source: nameof(NPCDialogueBootstrapper)
                );
                return;
            }

            string npcKey = string.IsNullOrWhiteSpace(defaultNpcSlug)
                ? DialogueManager.GetDefaultProfileSlug()
                : defaultNpcSlug.Trim();

            if (string.IsNullOrWhiteSpace(npcKey))
            {
                logger.Log(
                    NPCFlowStage.NPCSwitch,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Warning,
                    "Default NPC auto-select skipped because no profile slug was available.",
                    source: nameof(NPCDialogueBootstrapper)
                );
                return;
            }

            await DialogueManager.SwitchToNPCAsync(npcKey);
            logger.Log(
                NPCFlowStage.NPCSwitch,
                NPCFlowStatus.Success,
                NPCFlowLogLevel.Info,
                "Default NPC selected by bootstrapper.",
                source: nameof(NPCDialogueBootstrapper),
                npcSlug: npcKey
            );
        }

        void OnDestroy()
        {
            DatadogMetricsService.Shutdown();
        }
    }
}
