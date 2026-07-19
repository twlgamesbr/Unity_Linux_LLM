using System;
using System.Threading.Tasks;
using EditorAttributes;
using Supabase;
using Supabase.Gotrue;
using UnityEngine;

namespace NPCSystem
{
    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    public class PlayerAuthService : MonoBehaviour
    {
        [Title("Player Auth Service — Supabase Gotrue (SDK)")]
        [FoldoutGroup(
            "Supabase Auth",
            true,
            nameof(supabaseUrl),
            nameof(supabaseAnonKey),
            nameof(restApiUrl)
        )]
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

#if UNITY_WEBGL && !UNITY_EDITOR
        void ResolveWebGLUrls()
        {
            try
            {
                Uri pageUri = new Uri(Application.absoluteURL);
                string host = pageUri.Host;
                if (NPCNetworkUtils.IsLocalHost(host))
                    return;

                supabaseUrl = ReplaceHost(supabaseUrl, host);
                restApiUrl = ReplaceHost(restApiUrl, host);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[PlayerAuthService] Failed to dynamically resolve WebGL URLs: {ex.Message}"
                );
            }
        }

        static string ReplaceHost(string url, string newHost)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{newHost}:{uri.Port}{uri.AbsolutePath}";
            }
            catch
            {
                return url;
            }
        }
#endif

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

        [FoldoutGroup(
            "Debug",
            true,
            nameof(lastAuthStatus),
            nameof(lastAuthRoute),
            nameof(lastAuthDurationMs)
        )]
        [SerializeField]
        EditorAttributes.Void debugGroup;

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthStatus = "Idle";

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthRoute = string.Empty;

        [SerializeField, HideProperty, ReadOnly]
        long lastAuthDurationMs;

        // ── Runtime state ────────────────────────────────────────────

        Supabase.Client _supabaseClient;
        UnitySessionStore _sessionStore;
        bool _initialized;

        // Auto-discovered on first use
        SupabaseRealtimeService _realtimeService;

        public PlayerAuthSessionResponse CurrentSession { get; private set; }
        public bool IsAuthenticated =>
            CurrentSession != null
            && !string.IsNullOrWhiteSpace(CurrentSession.sessionToken)
            && !UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc);
        public string SupabaseUrl => supabaseUrl?.Trim() ?? string.Empty;

        /// <summary>
        /// Exposes the underlying supabase-csharp client so that other components
        /// (e.g. <see cref="SupabaseDialogueRepository"/>) can use the SDK directly.
        /// </summary>
        public Supabase.Client SupabaseClient => _supabaseClient;

        [ShowInInspector]
        string SupabaseUrlPreview => supabaseUrl?.Trim() ?? string.Empty;

        [ShowInInspector]
        string AuthSessionPreview =>
            CurrentSession == null
                ? "<none>"
                : $"{CurrentSession.username} ({CurrentSession.playerId})";

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
                    validUrl && validKey && validTimeout
                        ? NPCFlowStatus.Success
                        : NPCFlowStatus.Warning,
                    validUrl && validKey && validTimeout
                        ? NPCFlowLogLevel.Info
                        : NPCFlowLogLevel.Warning,
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

        // ── Public API ────────────────────────────────────────────

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
            ResolveWebGLUrls();
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
                // Try refresh
                PlayerAuthSessionResponse refreshed = await TryRefreshSessionWebGLAsync();
                if (refreshed != null)
                {
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

                _supabaseClient = new Supabase.Client(
                    supabaseUrl.TrimEnd('/'),
                    supabaseAnonKey,
                    options
                );

                _supabaseClient.Auth.AddDebugListener((msg, ex) =>
                {
                    if (ex != null)
                        Debug.Log($"[Supabase Auth] {msg}: {ex.Message}");
                });

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
                    CurrentSession = UnitySessionStore.ToAuthSession(
                        _supabaseClient.Auth.CurrentSession
                    );
                    lastAuthStatus =
                        $"SDK initialized. Session active for {CurrentSession.username}.";
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
                    scope.Success(
                        $"Stored auth session restored for {restored.username}.",
                        BuildSessionData(restored)
                    );

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

        public async Task<PlayerAuthRegisterResponse> RegisterAsync(
            string username,
            string password
        )
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await RegisterWebGLAsync(username, password);
#else
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);

            Session session = await _supabaseClient.Auth.SignUp(
                email,
                password ?? string.Empty,
                null
            );

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

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task<PlayerAuthRegisterResponse> RegisterWebGLAsync(
            string username,
            string password
        )
        {
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);
            string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/signup");

            using var req = new UnityWebRequest(url, "POST");
            byte[] body = System.Text.Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(new { email, password })
            );
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", supabaseAnonKey);

            await SendWebRequestAsync(req);

            string json = req.downloadHandler.text;
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            // Extract user id from nested "user" object (Gotrue returns id inside user)
            string userId = null;
            string accessToken = result?.GetValueOrDefault("access_token")?.ToString() ?? string.Empty;
            if (result?.TryGetValue("user", out var userObj) == true && userObj is JObject userJObj)
            {
                userId = userJObj.Value<string>("id");
            }
            userId = userId ?? result?.GetValueOrDefault("id")?.ToString()
                ?? throw new InvalidOperationException("Signup returned no user ID.");

            string refreshToken = result?.GetValueOrDefault("refresh_token")?.ToString() ?? string.Empty;
            long expiresIn = 3600L;
            if (result?.TryGetValue("expires_in", out var expVal) == true)
                long.TryParse(expVal?.ToString(), out expiresIn);

            var session = new PlayerAuthSessionResponse
            {
                playerId = userId,
                username = username?.Trim() ?? string.Empty,
                sessionToken = accessToken,
                refreshToken = refreshToken,
                expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };

            CurrentSession = session;
            UnitySessionStore.Save(session);
            lastAuthStatus = $"Registered and signed in as '{username?.Trim()}'.";

            await TryCreatePlayerProfileWebGLAsync(username?.Trim() ?? string.Empty);
            return new PlayerAuthRegisterResponse
            {
                playerId = userId,
                email = email,
                username = username?.Trim() ?? string.Empty,
                createdAtUtc = session.createdAtUtc,
            };
        }
#endif

        async Task TryCreatePlayerProfileAsync(string displayName)
        {
            if (_supabaseClient == null)
                return;

            try
            {
                await _supabaseClient.Rpc("create_or_update_player_profile", new
                {
                    p_display_name = displayName?.Trim() ?? string.Empty,
                });

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

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task<PlayerAuthSessionResponse> LoginWebGLAsync(
            string username,
            string password,
            bool rememberMe
        )
        {
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);
            string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/token?grant_type=password");

            using var req = new UnityWebRequest(url, "POST");
            byte[] body = System.Text.Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(new { email, password })
            );
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", supabaseAnonKey);

            await SendWebRequestAsync(req);

            string json = req.downloadHandler.text;
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            string accessToken = result?.GetValueOrDefault("access_token")?.ToString()
                ?? throw new InvalidOperationException("Login returned no access token.");
            string refreshToken = result?.GetValueOrDefault("refresh_token")?.ToString() ?? string.Empty;
            long expiresIn = 3600L;
            if (result?.TryGetValue("expires_in", out var expVal) == true)
                long.TryParse(expVal?.ToString(), out expiresIn);

            // Extract user info from nested "user" object
            string userId = string.Empty;
            if (result?.TryGetValue("user", out var userObj) == true && userObj is JObject userJObj)
            {
                userId = userJObj.Value<string>("id") ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("Login returned no user ID.");

            var session = new PlayerAuthSessionResponse
            {
                playerId = userId,
                username = username?.Trim() ?? string.Empty,
                sessionToken = accessToken,
                refreshToken = refreshToken,
                expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };

            CurrentSession = session;
            if (rememberMe)
                UnitySessionStore.Save(session);
            else
                UnitySessionStore.Clear();

            await TryCreatePlayerProfileWebGLAsync(username?.Trim() ?? string.Empty);

            lastAuthStatus =
                $"Login successful for '{username?.Trim()}'. Session expires at {session.expiresAtUtc}.";
            return session;
        }

        async Task TryCreatePlayerProfileWebGLAsync(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            try
            {
                string url = ResolveWebGLProxyUrl(restApiUrl, "/rpc/create_or_update_player_profile");
                using var req = new UnityWebRequest(url, "POST");
                byte[] body = System.Text.Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { p_display_name = displayName?.Trim() ?? string.Empty })
                );
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", supabaseAnonKey);
                if (CurrentSession != null)
                    req.SetRequestHeader("Authorization", $"Bearer {CurrentSession.sessionToken}");

                await SendWebRequestAsync(req);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerAuthService] WebGL profile creation: {ex.Message}");
            }
        }

        async Task SendWebRequestAsync(UnityWebRequest req)
        {
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            var tcs = new TaskCompletionSource<bool>();

            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                if (req.result == UnityWebRequest.Result.ConnectionError
                    || req.result == UnityWebRequest.Result.ProtocolError)
                {
                    string errorBody = req.downloadHandler?.text ?? string.Empty;
                    string msg = $"[{req.method}] {req.url} → {req.responseCode}: {req.error}";
                    if (!string.IsNullOrWhiteSpace(errorBody))
                        msg += $"\n{errorBody}";
                    tcs.TrySetException(new InvalidOperationException(msg));
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            };

            await tcs.Task;
        }

        string ResolveWebGLProxyUrl(string originalUrl, string path)
        {
            // Re-map the backend URL to go through the nginx proxy
            try
            {
                var uri = new Uri(originalUrl);
                string host = "localhost";
                int port = 8085;

#if !UNITY_EDITOR
                try
                {
                    Uri pageUri = new Uri(Application.absoluteURL);
                    host = pageUri.Host;
                    if (!NPCNetworkUtils.IsLocalHost(host))
                        port = pageUri.Port;
                }
                catch { /* use fallback defaults */ }
#endif
                return $"http://{host}:{port}{path}";
            }
            catch
            {
                return $"http://localhost:8085{path}";
            }
        }
#endif

        public async Task<PlayerAuthSessionResponse> LoginAsync(
            string username,
            string password,
            bool rememberMe
        )
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await LoginWebGLAsync(username, password, rememberMe);
#else
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);

            Session session = await _supabaseClient.Auth.SignIn(email, password ?? string.Empty);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken) || session.User == null)
                throw new InvalidOperationException("Supabase login returned an invalid session.");

            CurrentSession = UnitySessionStore.ToAuthSession(session);

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
            PlayerAuthSessionResponse webglResult = await TryRestoreWebGLAsync();
            if (webglResult != null)
                AuthNetworkBridge.ActivePlayerName = webglResult.username;
            return webglResult;
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

                CurrentSession = UnitySessionStore.ToAuthSession(
                    _supabaseClient.Auth.CurrentSession
                );

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

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task<PlayerAuthSessionResponse> TryRestoreWebGLAsync()
        {
            CurrentSession = UnitySessionStore.Load();
            if (CurrentSession == null)
                return null;

            if (UnitySessionStore.IsExpired(CurrentSession.expiresAtUtc))
            {
                // Try refresh token
                PlayerAuthSessionResponse refreshed = await TryRefreshSessionWebGLAsync();
                if (refreshed == null)
                {
                    ClearLocalSession();
                    return null;
                }
                return refreshed;
            }

            return CurrentSession;
        }

        async Task<PlayerAuthSessionResponse> TryRefreshSessionWebGLAsync()
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.refreshToken))
                return null;

            try
            {
                string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/token?grant_type=refresh_token");
                using var req = new UnityWebRequest(url, "POST");
                byte[] body = System.Text.Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { refresh_token = CurrentSession.refreshToken })
                );
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", supabaseAnonKey);

                await SendWebRequestAsync(req);

                string json = req.downloadHandler.text;
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                string accessToken = result?.GetValueOrDefault("access_token")?.ToString();
                string newRefreshToken = result?.GetValueOrDefault("refresh_token")?.ToString();
                long expiresIn = 3600L;
                if (result?.TryGetValue("expires_in", out var expVal) == true)
                    long.TryParse(expVal?.ToString(), out expiresIn);

                if (string.IsNullOrWhiteSpace(accessToken))
                    return null;

                CurrentSession.sessionToken = accessToken;
                CurrentSession.refreshToken = newRefreshToken ?? CurrentSession.refreshToken;
                CurrentSession.expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O");
                UnitySessionStore.Save(CurrentSession);
                return CurrentSession;
            }
            catch
            {
                return null;
            }
        }
#endif

        public async Task LogoutAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            await LogoutWebGLAsync();
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

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task LogoutWebGLAsync()
        {
            try
            {
                if (CurrentSession != null && !string.IsNullOrWhiteSpace(CurrentSession.sessionToken))
                {
                    string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/logout");
                    using var req = new UnityWebRequest(url, "POST");
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Authorization", $"Bearer {CurrentSession.sessionToken}");
                    req.SetRequestHeader("apikey", supabaseAnonKey);
                    await SendWebRequestAsync(req);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerAuthService] WebGL logout: {ex.Message}");
            }
            finally
            {
                ClearLocalSession();
            }
        }
#endif

        // ── Internal ──────────────────────────────────────────────

        void OnAuthStateChanged(Constants.AuthState state)
        {
            if (_supabaseClient?.Auth?.CurrentSession != null)
            {
                CurrentSession = UnitySessionStore.ToAuthSession(
                    _supabaseClient.Auth.CurrentSession
                );
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
                _realtimeService = FindAnyObjectByType<SupabaseRealtimeService>(
                    FindObjectsInactive.Include
                );

            if (_realtimeService != null)
                await _realtimeService.ConnectAsync();
        }

        void DisconnectRealtime()
        {
            if (_realtimeService == null)
                _realtimeService = FindAnyObjectByType<SupabaseRealtimeService>(
                    FindObjectsInactive.Include
                );

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

        static System.Collections.Generic.Dictionary<string, object> BuildSessionData(
            PlayerAuthSessionResponse session
        )
        {
            return new System.Collections.Generic.Dictionary<string, object>
            {
                ["playerId"] = session?.playerId ?? string.Empty,
                ["username"] = session?.username ?? string.Empty,
                ["expiresAtUtc"] = session?.expiresAtUtc ?? string.Empty,
                ["hasRefreshToken"] = !string.IsNullOrWhiteSpace(session?.refreshToken)
                    ? "yes"
                    : "no",
            };
        }
    }
}
