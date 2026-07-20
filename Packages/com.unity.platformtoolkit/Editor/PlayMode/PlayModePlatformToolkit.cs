using System;
using System.Threading.Tasks;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModePlatformToolkit : IPlatformToolkit
    {
        public PlayModeAccountSystemManager AccountSystemManager;
        public PlatformToolkitMetrics Metrics => m_Metrics;

        private readonly IEnvironment m_Environment;
        private readonly IPlayModeUserManager m_UserManager;
        private readonly IPlayModeCapability m_PlayModeCapability;
        private readonly PlatformToolkitMetrics m_Metrics;

        public ICapabilities Capabilities { get; }
        public IAccountSystem Accounts { get; private set; }
        public IAccountPickerSystem AccountPickerSystem { get; }
        public ISavingSystem LocalSavingSystem { get; }

        public PlayModePlatformToolkit(
            IPlayModeCapability playModeCapability,
            IEnvironment environment,
            IPlayModeUserManager userManager,
            PlayModeSaveData localSaveData
        )
        {
            var accountsSupported =
                playModeCapability.PrimaryAccountBehaviour != PrimaryAccountBehaviour.NotSupported
                || playModeCapability.AdditionalAccountBehaviour != AdditionalAccountBehaviour.NotSupported;

            var primaryAccountSupported =
                playModeCapability.PrimaryAccountBehaviour != PrimaryAccountBehaviour.NotSupported;

            var capabilityBuilder = new CapabilityBuilder
            {
                AccountSupport = accountsSupported,
                PrimaryAccount = primaryAccountSupported,
                PrimaryAccountEstablishLimited =
                    primaryAccountSupported
                    && (
                        !playModeCapability.AllowMultipleSignInAttempts
                        || playModeCapability.AccountsCannotSignInOffline
                    ),
                AdditionalAccountSystem =
                    playModeCapability.AdditionalAccountBehaviour != AdditionalAccountBehaviour.NotSupported,
                AccountName = true,
                AccountPicture = true,
                AccountSavingSystem = accountsSupported,
                AccountAchievementSystem = playModeCapability.SupportsAchievements,
                AccountManualSignOut = playModeCapability.AccountsCanManuallySignOut,
                AccountInputPairingSystem = playModeCapability.SupportsAccountInputOwnership,
                LocalSavingSystem = playModeCapability.SupportsLocalSaving,
            };

            m_Metrics = new PlatformToolkitMetrics(environment.WarningSettings);

            Capabilities = capabilityBuilder.ToCapabilities();
            m_PlayModeCapability = playModeCapability;
            m_Environment = environment;
            m_UserManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            LocalSavingSystem = new PlayModeSavingSystem(m_Environment, -1, localSaveData, m_Metrics);
        }

        public async Task Initialize()
        {
            await m_Environment.WaitIfPaused();
            AccountSystemManager = new PlayModeAccountSystemManager(
                m_Environment,
                m_UserManager,
                Capabilities,
                m_PlayModeCapability,
                m_Metrics
            );
#if INPUT_SYSTEM_AVAILABLE
            AccountSystemManager.SetInputSystem(m_InputSystem);
#endif // INPUT_SYSTEM_AVAILABLE
            AccountSystemManager.Initialize();
            Accounts = AccountSystemManager.AccountSystem;
        }

        public void MetricsUpdate()
        {
            m_Metrics.Update();
        }

#if INPUT_SYSTEM_AVAILABLE
        private PlayModeInputSystem m_InputSystem;

        public void SetInputSystem(PlayModeInputSystem inputSystem)
        {
            m_InputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
        }
#endif // INPUT_SYSTEM_AVAILABLE
    }
}
