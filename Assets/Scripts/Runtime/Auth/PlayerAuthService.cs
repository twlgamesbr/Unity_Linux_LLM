using System;
using System.Threading.Tasks;
using EditorAttributes;
using NPCSystem.Auth;
using NPCSystem.Character.NPC;
using NPCSystem.Character.Player;
using NPCSystem.Dialogue.Core;
using NPCSystem.Dialogue.Persistence;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Initialization;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Monitoring;
using NPCSystem.Network.Core;
using Supabase;
using Supabase.Gotrue;
using UnityEngine;

namespace NPCSystem.Auth
{
    // ── Auth response DTOs ────────────────────────────────

    [Serializable]
    public class PlayerAuthRegisterResponse
    {
        public string playerId;
        public string email;
        public string username;
        public string createdAtUtc;
    }

    /// <summary>
    /// Session response DTO — backed by Supabase Gotrue JWT tokens.
    /// </summary>
    [Serializable]
    public class PlayerAuthSessionResponse
    {
        public string sessionId;
        public string sessionToken; // Supabase access_token
        public string refreshToken;
        public string expiresAtUtc;
        public string lastSeenAtUtc;

        // ── Fields populated after login / register ──
        public string playerId;
        public string username;
        public string createdAtUtc;
    }

    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    public class PlayerAuthService : MonoBehaviour
    {
        [Title("Player Auth Service \u2014 Supabase Gotrue (SDK)")]
        [FoldoutGroup("Supabase Auth", true, nameof(supabaseUrl), nameof(supabaseAnonKey), nameof(restApiUrl))]
        [HelpBox(
            "supabaseUrl points to Gotrue. restApiUrl points to PostgREST. For development defaults: Gotrue on :8091, PostgREST on :8092.",
            MessageMode.Log
        )]
        [SerializeField]
        EditorAttributes.Void supabaseAuthGroup;

        [SerializeField, HideProperty]
        string supabaseUrl = "http://localhost:8091";

        [SerializeField, HideProperty]
        string supabaseAnonKey = "dev-local-anon-key";

        [SerializeField, HideProperty]
        string restApiUrl = "http://localhost:8092";

        [FoldoutGroup(
            "Behaviour",
            true,
            nameof(requestTimeoutSeconds),
            nameof(validateStoredSessionOnStart),
            nameof(restoreStoredSessionOnWebGLStart)
        )]
        [SerializeField]
        EditorAttributes.Void behaviourGroup;

        [SerializeField, HideProperty, Suffix("s")]
        float requestTimeoutSeconds = 15f;

        [SerializeField, HideProperty]
        bool validateStoredSessionOnStart = true;

        [SerializeField, HideProperty]
        bool restoreStoredSessionOnWebGLStart;

        [FoldoutGroup("Debug", true, nameof(lastAuthStatus), nameof(lastAuthRoute), nameof(lastAuthDurationMs))]
        [SerializeField]
        EditorAttributes.Void debugGroup;

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthStatus = "Idle";

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthRoute = string.Empty;

        [SerializeField, HideProperty, ReadOnly]
        long lastAuthDurationMs;

        Supabase.Client _supabaseClient;
        UnitySessionStore _sessionStore;
        bool _initialized;
        SupabaseRealtimeService _realtimeService;

        public PlayerAuthSessionResponse CurrentSession { get; private set; }
        public bool IsAuthenticated =>
            CurrentSession != null
            && !string.IsNullOrWhiteSpace(CurrentSession.sessionToken)
            && !UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc);
        public string SupabaseUrl => supabaseUrl?.Trim() ?? string.Empty;

        public Supabase.Client SupabaseClient => _supabaseClient;

        [ShowInInspector]
        string SupabaseUrlPreview => supabaseUrl?.Trim() ?? string.Empty;

        [ShowInInspector]
        string AuthSessionPreview =>
            CurrentSession == null ? "<none>" : $"{CurrentSession.username} ({CurrentSession.playerId})";

        [ShowInInspector]
        string BackendHealthPreview =>
            $"{supabaseUrl} | timeout={Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds))}s";

        [Button("Validate Auth Service Settings")]
        void ValidateAuthServiceSettings()
        {
            bool validUrl =
                !string.IsNullOrWhiteSpace(supabaseUrl)
                && (supabaseUrl.StartsWith("http://") || supabaseUrl.StartsWith("https://"));
            bool validKey = !string.IsNullOrWhiteSpace(supabaseAnonKey);
            bool validTimeout = requestTimeoutSeconds > 0f;
            lastAuthStatus =
                validUrl && validKey && validTimeout
                    ? $"Supabase auth settings look valid: {BackendHealthPreview}"
                    : "Supabase auth settings are incomplete. Check supabaseUrl, supabaseAnonKey, and requestTimeoutSeconds.";

            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.ConfigurationValidation,
                    validUrl && validKey && validTimeout ? NPCFlowStatus.Success : NPCFlowStatus.Warning,
                    validUrl && validKey && validTimeout ? NPCFlowLogLevel.Info : NPCFlowLogLevel.Warning,
                    lastAuthStatus,
                    source: nameof(PlayerAuthService),
                    data: new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["supabaseUrl"] = supabaseUrl ?? string.Empty,
                        ["requestTimeoutSeconds"] = requestTimeoutSeconds,
                        ["validateStoredSessionOnStart"] = validateStoredSessionOnStart,
                    }
                );
        }

        public async Task<PlayerAuthSessionResponse> InitializeAsync()
        {
            if (_initialized)
                return IsAuthenticated ? CurrentSession : null;

            bool shouldValidateStoredSession = ShouldValidateStoredSessionOnStart(
                validateStoredSessionOnStart,
                restoreStoredSessionOnWebGLStart,
                Application.platform
            );

#if UNITY_WEBGL && !UNITY_EDITOR
            SupabaseAuthClient.ResolveWebGLUrls(
                supabaseUrl,
                restApiUrl,
                out string resolvedSupabase,
                out string resolvedRest
            );
            supabaseUrl = resolvedSupabase;
            restApiUrl = resolvedRest;

            _sessionStore = new UnitySessionStore();
            _initialized = true;

            if (!shouldValidateStoredSession)
            {
                lastAuthStatus = "Stored session restore deferred on WebGL startup.";
                return null;
            }

            CurrentSession = UnitySessionStore.Load();

            if (CurrentSession != null && !UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc))
            {
                lastAuthStatus = $"Session restored for {CurrentSession.username}.";
                return CurrentSession;
            }

            if (CurrentSession != null && UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc))
            {
                PlayerAuthSessionResponse refreshed = await SupabaseAuthClient.RefreshSessionWebGLAsync(
                    supabaseUrl,
                    supabaseAnonKey,
                    CurrentSession,
                    requestTimeoutSeconds
                );
                if (refreshed != null)
                {
                    UnitySessionStore.Save(refreshed);
                    lastAuthStatus = $"Session refreshed for {refreshed.username}.";
                    return refreshed;
                }
            }

            ClearLocalSession();
            lastAuthStatus = "No active session.";
            return null;
#else

            using var scope = NPCFlowScope.Start(
                NPCFlowLogger.FindOrCreate(),
                NPCFlowStage.AuthSession,
                nameof(PlayerAuthService),
                data: new System.Collections.Generic.Dictionary<string, object>
                {
                    ["validateStoredSessionOnStart"] = shouldValidateStoredSession,
                    ["supabaseUrl"] = supabaseUrl ?? string.Empty,
                }
            );

            try
            {
                _sessionStore = new UnitySessionStore();

                var options = new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = false,
                    SessionHandler = _sessionStore,
                    AuthUrlFormat = "{0}",
                    RestUrlFormat = restApiUrl,
                };

                _supabaseClient = new Supabase.Client(supabaseUrl.TrimEnd('/'), supabaseAnonKey, options);

                _supabaseClient.Auth.AddDebugListener(
                    (msg, ex) =>
                    {
                        if (ex != null)
                            Debug.Log($"[Supabase Auth] {msg}: {ex.Message}");
                    }
                );

                _supabaseClient.Auth.AddStateChangedListener(
                    (Supabase.Gotrue.Interfaces.IGotrueClient<User, Session> sender, Constants.AuthState state) =>
                    {
                        OnAuthStateChanged(state);
                    }
                );

                _supabaseClient.Auth.Options.AllowUnconfirmedUserSessions = true;

                _supabaseClient.Auth.LoadSession();

                await _supabaseClient.InitializeAsync();

                _initialized = true;

                if (_supabaseClient.Auth.CurrentSession != null)
                {
                    CurrentSession = UnitySessionStore.ToAuthSession(_supabaseClient.Auth.CurrentSession);
                    lastAuthStatus = $"SDK initialized. Session active for {CurrentSession.username}.";
                }
                else
                {
                    lastAuthStatus = "SDK initialized. No active session.";
                }

                if (!shouldValidateStoredSession)
                {
                    lastAuthStatus = "Stored session restore deferred on startup.";
                    scope.Success(lastAuthStatus);
                    return null;
                }

                if (CurrentSession == null)
                {
                    scope.Skipped("No stored auth session found.");
                    return null;
                }

                PlayerAuthSessionResponse restored = await TryRestoreStoredSessionAsync();
                if (restored == null)
                    scope.Warning("Stored auth session could not be restored.");
                else
                    scope.Success($"Stored auth session restored for {restored.username}.", BuildSessionData(restored));

                return restored;
            }
            catch (Exception ex)
            {
                lastAuthStatus = $"SDK init failed: {ex.Message}";
                scope.Error(ex, lastAuthStatus);
                return null;
            }
#endif
        }

        public async Task<PlayerAuthRegisterResponse> RegisterAsync(string username, string password)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var registerResult = await SupabaseAuthClient.RegisterWebGLAsync(
                supabaseUrl,
                supabaseAnonKey,
                username,
                password,
                requestTimeoutSeconds
            );

            string sessionToken = registerResult.sessionToken ?? string.Empty;
            CurrentSession = new PlayerAuthSessionResponse
            {
                playerId = registerResult.playerId,
                username = registerResult.username,
                sessionToken = sessionToken,
                expiresAtUtc = DateTime.UtcNow.AddSeconds(3600).ToString("O"),
                createdAtUtc = registerResult.createdAtUtc,
            };
            UnitySessionStore.Save(CurrentSession);
            lastAuthStatus = $"Registered and signed in as '{username?.Trim()}'.";
            return registerResult;
#else
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);

            Session session = await _supabaseClient.Auth.SignUp(email, password ?? string.Empty, null);

            if (session?.User == null || string.IsNullOrWhiteSpace(session.User.Id))
                throw new InvalidOperationException("Supabase signup returned no user.");

            CurrentSession = UnitySessionStore.ToAuthSession(session);

            await TryCreatePlayerProfileAsync(username?.Trim() ?? string.Empty);

            return new PlayerAuthRegisterResponse
            {
                playerId = session.User.Id,
                email = session.User.Email ?? email,
                username = username?.Trim() ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
#endif
        }

        async Task TryCreatePlayerProfileAsync(string displayName)
        {
            if (_supabaseClient == null)
                return;

            try
            {
                await _supabaseClient.Rpc(
                    "create_or_update_player_profile",
                    new { p_display_name = displayName?.Trim() ?? string.Empty }
                );

                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Debug,
                        $"Player profile created/updated for '{displayName}'.",
                        source: nameof(PlayerAuthService)
                    );
            }
            catch (Exception ex)
            {
                NPCFlowLogger
                    .FindOrCreate()
                    ?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Player profile creation failed: {ex.Message}",
                        source: nameof(PlayerAuthService)
                    );
            }
        }

        public async Task<PlayerAuthSessionResponse> LoginAsync(string username, string password, bool rememberMe)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var session = await SupabaseAuthClient.LoginWebGLAsync(
                supabaseUrl,
                supabaseAnonKey,
                username,
                password,
                rememberMe,
                requestTimeoutSeconds
            );

            CurrentSession = session;
            if (rememberMe)
                UnitySessionStore.Save(session);
            else
                UnitySessionStore.Clear();

            await SupabaseAuthClient.CreatePlayerProfileWebGLAsync(
                supabaseUrl,
                restApiUrl,
                supabaseAnonKey,
                username?.Trim(),
                session.sessionToken,
                requestTimeoutSeconds
            );

            lastAuthStatus = $"Login successful for '{username?.Trim()}'. Session expires at {session.expiresAtUtc}.";
            return session;
#else
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);

            Session sdkSession = await _supabaseClient.Auth.SignIn(email, password ?? string.Empty);

            if (sdkSession == null || string.IsNullOrWhiteSpace(sdkSession.AccessToken) || sdkSession.User == null)
                throw new InvalidOperationException("Supabase login returned an invalid session.");

            CurrentSession = UnitySessionStore.ToAuthSession(sdkSession);

            if (rememberMe)
                UnitySessionStore.Save(CurrentSession);
            else
                UnitySessionStore.Clear();

            await TryCreatePlayerProfileAsync(username?.Trim() ?? string.Empty);
            await TryConnectRealtimeAsync();

            lastAuthStatus =
                $"Login successful for '{username?.Trim()}'. Session expires at {CurrentSession.expiresAtUtc}.";
            return CurrentSession;
#endif
        }

        public async Task<PlayerAuthSessionResponse> TryRestoreStoredSessionAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CurrentSession = UnitySessionStore.Load();
            if (CurrentSession == null)
                return null;

            if (UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc))
            {
                var refreshed = await SupabaseAuthClient.RefreshSessionWebGLAsync(
                    supabaseUrl,
                    supabaseAnonKey,
                    CurrentSession,
                    requestTimeoutSeconds
                );
                if (refreshed == null)
                {
                    ClearLocalSession();
                    return null;
                }
                CurrentSession = refreshed;
                UnitySessionStore.Save(CurrentSession);
            }

            AuthNetworkBridge.ActivePlayerName = CurrentSession.username;
            return CurrentSession;
#else
            if (_supabaseClient?.Auth?.CurrentSession == null)
            {
                ClearLocalSession();
                return null;
            }

            try
            {
                if (_supabaseClient.Auth.CurrentSession.Expired())
                {
                    await _supabaseClient.Auth.RefreshToken();
                }

                if (_supabaseClient.Auth.CurrentSession == null)
                {
                    ClearLocalSession();
                    return null;
                }

                CurrentSession = UnitySessionStore.ToAuthSession(_supabaseClient.Auth.CurrentSession);

                await TryCreatePlayerProfileAsync(CurrentSession.username);
                AuthNetworkBridge.ActivePlayerName = CurrentSession.username;
                await TryConnectRealtimeAsync();
                return CurrentSession;
            }
            catch
            {
                ClearLocalSession();
                return null;
            }
#endif
        }

        public async Task LogoutAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            await SupabaseAuthClient.LogoutWebGLAsync(
                supabaseUrl,
                supabaseAnonKey,
                CurrentSession,
                requestTimeoutSeconds
            );
            ClearLocalSession();
#else
            using var scope = NPCFlowScope.Start(
                NPCFlowLogger.FindOrCreate(),
                NPCFlowStage.AuthSession,
                nameof(PlayerAuthService),
                data: BuildSessionData(CurrentSession)
            );
            try
            {
                if (_supabaseClient?.Auth != null)
                    await _supabaseClient.Auth.SignOut();

                lastAuthStatus = "Logged out auth session.";
                scope.Success(lastAuthStatus);
            }
            catch (Exception ex)
            {
                scope.Error(ex, $"Logout request failed: {ex.Message}");
            }
            finally
            {
                ClearLocalSession();
                DisconnectRealtime();
            }
#endif
        }

        void OnAuthStateChanged(Constants.AuthState state)
        {
            if (_supabaseClient?.Auth?.CurrentSession != null)
            {
                CurrentSession = UnitySessionStore.ToAuthSession(_supabaseClient.Auth.CurrentSession);
                lastAuthStatus = $"Auth state changed: {state} for {CurrentSession.username}";
            }
            else
            {
                CurrentSession = null;
                lastAuthStatus = $"Auth state changed: {state} (no session)";
            }
        }

        void ClearLocalSession()
        {
            CurrentSession = null;
            UnitySessionStore.Clear();
        }

        async Task TryConnectRealtimeAsync()
        {
            if (_realtimeService == null)
                _realtimeService = FindAnyObjectByType<SupabaseRealtimeService>(FindObjectsInactive.Include);

            if (_realtimeService != null)
                await _realtimeService.ConnectAsync();
        }

        void DisconnectRealtime()
        {
            if (_realtimeService == null)
                _realtimeService = FindAnyObjectByType<SupabaseRealtimeService>(FindObjectsInactive.Include);

            if (_realtimeService != null)
                _realtimeService.Disconnect();
        }

        static string EmailFromUsername(string username)
        {
            return $"{username}@npc-game.local";
        }

        static string UsernameFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "unknown";
            int atIndex = email.IndexOf('@');
            return atIndex > 0 ? email.Substring(0, atIndex) : email;
        }

        static bool ShouldValidateStoredSessionOnStart(
            bool configuredValue,
            bool restoreOnWebGLStart,
            RuntimePlatform platform
        )
        {
            if (!configuredValue)
                return false;
            return platform != RuntimePlatform.WebGLPlayer || restoreOnWebGLStart;
        }

        static System.Collections.Generic.Dictionary<string, object> BuildSessionData(PlayerAuthSessionResponse session)
        {
            return new System.Collections.Generic.Dictionary<string, object>
            {
                ["playerId"] = session?.playerId ?? string.Empty,
                ["username"] = session?.username ?? string.Empty,
                ["expiresAtUtc"] = session?.expiresAtUtc ?? string.Empty,
                ["hasRefreshToken"] = !string.IsNullOrWhiteSpace(session?.refreshToken) ? "yes" : "no",
            };
        }
    }
}
