using Unity.PlatformToolkit.PlayMode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.PlatformToolkit.Editor
{
    static internal class PlatformToolkitEditorRunner
    {
        static PlayModeControlsSettings s_CachedSettings;
        static PlayModePlatformToolkit s_RegisteredUpdateInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            s_CachedSettings = null;

            if (PlayModeControlsEditorSettings.instance.CurrentSettings is { } settings)
            {
                // Always reset the play mode runtime on play to avoid state leaking.
                var newRuntime = settings.RecreateRuntime();
                s_CachedSettings = settings;
                EditorApplication.playModeStateChanged += PlayModeStateChanged;

                var toolkit = new PlayModePlatformToolkit(
                    newRuntime.Capability,
                    newRuntime.Environment,
                    newRuntime.UserManager,
                    settings.LocalSaveData);
#if INPUT_SYSTEM_AVAILABLE
                toolkit.SetInputSystem(newRuntime.PlayModeInputSystem);
#endif // INPUT_SYSTEM_AVAILABLE;
                PlatformToolkit.InjectImplementation(toolkit);

                // PlayModeStateChanged will unregister this.
                Assert.IsNull(s_RegisteredUpdateInstance, "An EditorApplication.update event was not unregistered correctly.");
                s_RegisteredUpdateInstance = toolkit;
                EditorApplication.update += Update;
            }
        }

        private static void PlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Update;
                s_RegisteredUpdateInstance = null;

                // Recreate the runtime for edit-mode
                EditorApplication.playModeStateChanged -= PlayModeStateChanged;

                // Check if the settings asset is still valid (it may have been deleted)
                if (s_CachedSettings != null)
                    s_CachedSettings.RecreateRuntime();
                s_CachedSettings = null;
            }
        }

        private static void Update()
        {
            // This event can be invoked on the frame it is unregistered
            if (s_RegisteredUpdateInstance != null)
                s_RegisteredUpdateInstance.MetricsUpdate();
        }
    }
}
