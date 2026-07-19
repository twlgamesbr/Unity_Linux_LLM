using UnityEngine;

namespace NPCSystem.Monitoring
{
    /// <summary>
    /// Lightweight on-screen diagnostics overlay showing FPS, memory, and network state.
    /// Works on all platforms including WebGL (no TMP/UI dependencies — uses OnGUI).
    ///
    /// Visible by default in Editor and Development builds.
    /// Toggle with the backtick/tilde (`) key or call <see cref="SetVisible"/>.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class DiagnosticsHUD : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField]
        bool _visibleByDefault;

        [Header("Position")]
        [SerializeField]
        int _anchorX = 10;
        [SerializeField]
        int _anchorY = 10;

        [Header("Style")]
        [SerializeField]
        int _fontSize = 12;
        [SerializeField]
        string _textColor = "#00ff00";

        WebGLDiagnosticsService _diagnostics;
        GUIStyle _style;
        bool _visible;
        float _toggleCooldown;

        void Start()
        {
            _diagnostics = FindAnyObjectByType<WebGLDiagnosticsService>(FindObjectsInactive.Include);
            _visible = _visibleByDefault
                || Debug.isDebugBuild
                || Application.isEditor;
        }

        void Update()
        {
            // Toggle with backtick/tilde key
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                if (Time.unscaledTime > _toggleCooldown)
                {
                    _visible = !_visible;
                    _toggleCooldown = Time.unscaledTime + 0.5f;
                }
            }
        }

        void OnGUI()
        {
            if (!_visible)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                };
                ColorUtility.TryParseHtmlString(_textColor, out Color c);
                _style.normal.textColor = c;
            }

            float fps = _diagnostics != null ? _diagnostics.CurrentFps : 1f / Time.unscaledDeltaTime;
            long memMb = _diagnostics != null ? _diagnostics.CurrentMemoryMb : SystemInfo.graphicsMemorySize;

            string platform = Application.platform == RuntimePlatform.WebGLPlayer
                ? "WebGL"
                : Application.platform.ToString().Replace("Player", "");

            int qualityLevel = QualitySettings.GetQualityLevel();
            string qualityName = QualitySettings.names[qualityLevel];
            string netState = Application.internetReachability switch
            {
                NetworkReachability.ReachableViaLocalAreaNetwork => "LAN",
                NetworkReachability.ReachableViaCarrierDataNetwork => "WWAN",
                _ => "Offline",
            };

            Rect box = new Rect(_anchorX, _anchorY, 260, 100);
            GUI.Box(box, GUIContent.none);

            float y = _anchorY + 4;
            float x = _anchorX + 6;
            float lineHeight = _fontSize + 4;

            GUI.Label(new Rect(x, y, 240, lineHeight), $"{platform} | {qualityName}", _style);
            y += lineHeight;

            Color original = GUI.color;
            GUI.color = fps < 20f ? Color.red : (fps < 30f ? Color.yellow : Color.green);
            GUI.Label(new Rect(x, y, 240, lineHeight), $"FPS: {fps:F1}", _style);
            GUI.color = original;
            y += lineHeight;

            GUI.color = memMb > 800f ? Color.yellow : Color.green;
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Mem: {memMb}MB", _style);
            GUI.color = original;
            y += lineHeight;

            GUI.Label(new Rect(x, y, 240, lineHeight), $"Net: {netState} | Quality: {qualityLevel}", _style);
            y += lineHeight;

            // Network info (via reflection to avoid hard dependency on Unity.Netcode)
            var networkManagerType = System.Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
            if (networkManagerType != null)
            {
                var singleton = networkManagerType.GetProperty("Singleton")?.GetValue(null, null);
                if (singleton != null)
                {
                    bool isListening = (bool)(networkManagerType.GetProperty("IsListening")?.GetValue(singleton, null) ?? false);
                    if (isListening)
                    {
                        bool isServer = (bool)(networkManagerType.GetProperty("IsServer")?.GetValue(singleton, null) ?? false);
                        string role = isServer ? "Server" : "Client";
                        GUI.Label(new Rect(x, y, 240, lineHeight), $"Netcode: {role}", _style);
                    }
                }
            }
        }
    }
}
