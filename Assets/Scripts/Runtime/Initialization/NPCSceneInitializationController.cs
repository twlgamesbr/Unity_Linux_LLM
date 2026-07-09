using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    public enum NPCSceneInitializationPhase
    {
        Logger,
        SceneReferences,
        NetworkTransport,
        DialogueServices,
        BackendReadiness,
        NetworkBridge,
        Validation,
        Spawning,
    }

    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public sealed class NPCSceneInitializationController : MonoBehaviour
    {
        public static readonly NPCSceneInitializationPhase[] OrderedPhases =
        {
            NPCSceneInitializationPhase.Logger,
            NPCSceneInitializationPhase.SceneReferences,
            NPCSceneInitializationPhase.NetworkTransport,
            NPCSceneInitializationPhase.DialogueServices,
            NPCSceneInitializationPhase.BackendReadiness,
            NPCSceneInitializationPhase.NetworkBridge,
            NPCSceneInitializationPhase.Validation,
            NPCSceneInitializationPhase.Spawning,
        };

        [Header("References")]
        [FormerlySerializedAs("FlowLogger")]
        public NPCFlowLogger FlowLogger;
        [FormerlySerializedAs("NetworkBootstrap")]
        public NPCNetworkBootstrap NetworkBootstrap;
        [FormerlySerializedAs("DialogueManager")]
        public NPCDialogueManager DialogueManager;
        [FormerlySerializedAs("BackendReadiness")]
        public NPCBackendReadinessService BackendReadiness;
        [FormerlySerializedAs("NetworkBridge")]
        public NPCDialogueNetworkBridge NetworkBridge;
        [FormerlySerializedAs("SmokeValidator")]
        public NPCDialogueSmokeValidator SmokeValidator;

        [Header("Startup")]
        [FormerlySerializedAs("InitializeOnStart")]
        public bool InitializeOnStart = true;
        [FormerlySerializedAs("ConfigureNetworkTransport")]
        public bool ConfigureNetworkTransport = false;

        [Tooltip(
            "If true, initializes the dialogue manager immediately during scene start. Set to false to delay initialization until after player login (recommended for WebGL memory-smart start)."
        )]
        [FormerlySerializedAs("InitializeDialogueManager")]
        public bool InitializeDialogueManager = false;
        [FormerlySerializedAs("VerifyBackendsDuringInitialization")]
        public bool VerifyBackendsDuringInitialization = false;
        [FormerlySerializedAs("InitializeNetworkBridge")]
        public bool InitializeNetworkBridge = true;
        [FormerlySerializedAs("ValidateAfterInitialization")]
        public bool ValidateAfterInitialization = true;
        [FormerlySerializedAs("StartNetworkingAfterInitialization")]
        public bool StartNetworkingAfterInitialization = false;

        bool _started;
        Task _initializationTask;

        public bool IsInitialized =>
            _initializationTask != null && _initializationTask.IsCompletedSuccessfully;
        public Task InitializationTask => _initializationTask;

        void Reset()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResolveReferences();
            }
        }

        void Awake()
        {
            ResolveReferences();
        }

        async void Start()
        {
            if (!InitializeOnStart)
                return;
            if (ShouldDeferInitializationForWebGL())
            {
                FlowLogger = FlowLogger != null ? FlowLogger : NPCFlowLogger.FindOrCreate();
                FlowLogger.Log(
                    NPCFlowStage.SceneBootstrap,
                    NPCFlowStatus.Skipped,
                    NPCFlowLogLevel.Info,
                    "Deferred automatic scene initialization for WebGL startup to avoid browser bootstrap instability. Call InitializeSceneAsync after the page finishes loading and the player is ready.",
                    source: nameof(NPCSceneInitializationController)
                );
                return;
            }
            await InitializeSceneAsync();
        }

        [ContextMenu("Initialize Scene")]
        public async void InitializeSceneFromContextMenu()
        {
            await InitializeSceneAsync();
        }

        public Task InitializeSceneAsync()
        {
            _initializationTask ??= InitializeSceneInternalAsync();
            return _initializationTask;
        }

        bool ShouldDeferInitializationForWebGL()
        {
            return Application.platform == RuntimePlatform.WebGLPlayer;
        }

        async Task InitializeSceneInternalAsync()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            foreach (NPCSceneInitializationPhase phase in OrderedPhases)
            {
                await RunPhaseAsync(phase);
            }
        }

        async Task RunPhaseAsync(NPCSceneInitializationPhase phase)
        {
            NPCFlowLogger logger = FlowLogger != null ? FlowLogger : NPCFlowLogger.FindOrCreate();
            using var scope = NPCFlowScope.Start(
                logger,
                NPCFlowStage.SceneBootstrap,
                source: nameof(NPCSceneInitializationController),
                data: new Dictionary<string, object> { ["phase"] = phase.ToString() }
            );

            try
            {
                switch (phase)
                {
                    case NPCSceneInitializationPhase.Logger:
                        FlowLogger = logger;
                        break;
                    case NPCSceneInitializationPhase.SceneReferences:
                        ResolveReferences();
                        break;
                    case NPCSceneInitializationPhase.NetworkTransport:
                        if (ConfigureNetworkTransport && NetworkBootstrap != null)
                        {
                            NetworkBootstrap.ApplyTransportConfiguration();
                        }
                        break;
                    case NPCSceneInitializationPhase.DialogueServices:
                        if (InitializeDialogueManager && DialogueManager != null)
                        {
                            await DialogueManager.InitializeAsync();
                        }
                        break;
                    case NPCSceneInitializationPhase.BackendReadiness:
                        if (VerifyBackendsDuringInitialization && BackendReadiness != null)
                        {
                            bool probeLocalAi =
                                InitializeDialogueManager
                                && (DialogueManager != null && DialogueManager.InitializeOnStart);
                            await BackendReadiness.ProbeAsync(probeLocalAi);
                        }
                        break;
                    case NPCSceneInitializationPhase.NetworkBridge:
                        if (InitializeNetworkBridge && NetworkBridge != null)
                        {
                            await NetworkBridge.InitializeAsync();
                        }
                        break;
                    case NPCSceneInitializationPhase.Validation:
                        if (ValidateAfterInitialization && SmokeValidator != null)
                        {
                            if (DialogueManager != null && !DialogueManager.IsInitialized)
                            {
                                logger.Log(
                                    NPCFlowStage.SceneBootstrap,
                                    NPCFlowStatus.Skipped,
                                    NPCFlowLogLevel.Info,
                                    "Skipped scene initialization smoke validation because dialogue manager is not initialized yet (deferred loading active).",
                                    source: nameof(NPCSceneInitializationController),
                                    data: new Dictionary<string, object>
                                    {
                                        ["phase"] = phase.ToString(),
                                    }
                                );
                            }
                            else
                            {
                                SmokeValidator.ValidateConfiguration();
                            }
                        }
                        break;
                    case NPCSceneInitializationPhase.Spawning:
                        if (StartNetworkingAfterInitialization && NetworkBootstrap != null)
                        {
                            bool skipForBatchmodeBootstrap =
                                Application.isBatchMode
                                && NetworkBootstrap.TransportConfig.autoStartMode
                                    != NPCNetworkAutoStartMode.Manual;

                            if (skipForBatchmodeBootstrap)
                            {
                                logger.Log(
                                    NPCFlowStage.SceneBootstrap,
                                    NPCFlowStatus.Skipped,
                                    NPCFlowLogLevel.Info,
                                    "Skipped scene initialization network start because batchmode bootstrap auto-start is active.",
                                    source: nameof(NPCSceneInitializationController),
                                    data: new Dictionary<string, object>
                                    {
                                        ["phase"] = phase.ToString(),
                                        ["autoStartMode"] =
                                            NetworkBootstrap.TransportConfig.autoStartMode.ToString(),
                                    }
                                );
                                break;
                            }

                            bool started = NetworkBootstrap.StartConfiguredMode();
                            logger.Log(
                                NPCFlowStage.SceneBootstrap,
                                started ? NPCFlowStatus.Success : NPCFlowStatus.Skipped,
                                started ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Warning,
                                started
                                    ? "NetworkManager started from scene initialization controller."
                                    : "NetworkManager start skipped by scene initialization controller.",
                                source: nameof(NPCSceneInitializationController),
                                data: new Dictionary<string, object>
                                {
                                    ["phase"] = phase.ToString(),
                                }
                            );
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(phase),
                            phase,
                            "Unknown scene initialization phase."
                        );
                }

                scope.Success(
                    "Scene initialization phase completed.",
                    new Dictionary<string, object> { ["phase"] = phase.ToString() }
                );
            }
            catch (Exception ex)
            {
                logger.Log(
                    NPCFlowStage.SceneBootstrap,
                    NPCFlowStatus.Error,
                    NPCFlowLogLevel.Warning,
                    $"Scene initialization phase {phase} failed: {ex.Message}",
                    source: nameof(NPCSceneInitializationController),
                    data: new Dictionary<string, object>
                    {
                        ["phase"] = phase.ToString(),
                        ["exception"] = ex.ToString(),
                    }
                );
                scope.Warning(
                    $"Scene initialization phase {phase} failed: {ex.Message}",
                    new Dictionary<string, object> { ["phase"] = phase.ToString() }
                );
            }
        }

        public void ResolveReferences()
        {
            if (FlowLogger == null)
            {
                FlowLogger = NPCFlowLogger.FindOrCreate();
            }

            if (NetworkBootstrap == null)
            {
                NetworkBootstrap = FindAnyObjectByType<NPCNetworkBootstrap>(
                    FindObjectsInactive.Include
                );
            }

            if (DialogueManager == null)
            {
                DialogueManager = FindAnyObjectByType<NPCDialogueManager>(
                    FindObjectsInactive.Include
                );
            }

            if (NetworkBridge == null)
            {
                NetworkBridge = FindAnyObjectByType<NPCDialogueNetworkBridge>(
                    FindObjectsInactive.Include
                );
            }

            if (BackendReadiness == null)
            {
                BackendReadiness = FindAnyObjectByType<NPCBackendReadinessService>(
                    FindObjectsInactive.Include
                );
            }

            if (SmokeValidator == null)
            {
                SmokeValidator = FindAnyObjectByType<NPCDialogueSmokeValidator>(
                    FindObjectsInactive.Include
                );
            }
        }
    }
}
