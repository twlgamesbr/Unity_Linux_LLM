using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Attach to any persistent GameObject (e.g. the NPCFlowLogger) to
    /// drive Console Pro's live Watch panel with real-time performance metrics.
    ///
    /// Every frame, pushes FPS, memory, LLM durations, and other tracked
    /// counters into Console Pro's Watch view.
    ///
    /// Also installs the global log interceptor that routes all NPC-prefixed
    /// Debug.Log calls through Console Pro for proper category filtering.
    /// </summary>
    [AddComponentMenu("NPCSystem/Monitoring/Console Pro Watcher")]
    public class ConsoleProBehaviour : MonoBehaviour
    {
        [Header("Watch Panel")]
        [SerializeField, Tooltip("How often to push watch values (0 = every frame). Higher values reduce log spam.")]
        int _updateIntervalFrames = 3;

        [SerializeField, Tooltip("Enable real-time Watch panel updates")]
        bool _enableWatchPanel = true;

        [Header("Log Interceptor")]
        [SerializeField, Tooltip("Catch all NPC-prefixed Debug.Log calls and route through Console Pro filters")]
        bool _enableLogInterceptor = true;

        [Header("Startup")]
        [SerializeField, Tooltip("Open Console Pro window on scene start (Editor only)")]
        bool _autoOpenConsolePro = false;

        int _frameCounter;

        void Awake()
        {
            if (_enableLogInterceptor)
            {
                ConsoleProLogInterceptor.Initialize();
            }
        }

        void Start()
        {
#if UNITY_EDITOR
            if (_autoOpenConsolePro)
            {
                TryOpenConsoleProWindow();
            }
#endif
        }

        void Update()
        {
            if (!_enableWatchPanel)
                return;

            _frameCounter++;
            if (_frameCounter < _updateIntervalFrames)
                return;
            _frameCounter = 0;

            ConsoleProWatcher.UpdateWatches();
        }

        void OnDestroy()
        {
            if (_enableLogInterceptor)
            {
                ConsoleProLogInterceptor.Shutdown();
            }
        }

#if UNITY_EDITOR
        static void TryOpenConsoleProWindow()
        {
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    var type = asm.GetType("FlyingWormConsole3.ConsolePro3Window");
                    if (type == null)
                        continue;

                    var method = type.GetMethod("GetWindow",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    method?.Invoke(null, null);
                    Debug.Log("[ConsoleProBehaviour] Console Pro window opened automatically.");
                    return;
                }
            }
            catch
            {
                // Non-critical
            }
        }
#endif
    }
}
