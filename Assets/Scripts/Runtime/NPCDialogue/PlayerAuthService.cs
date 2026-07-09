using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using EditorAttributes;
using Void = EditorAttributes.Void;
using UnityEngine;
using UnityEngine.Networking;

namespace NPCSystem
{
    // Existing register response — kept for downstream compatibility
    [Serializable]
    public class PlayerAuthRegisterResponse
    {
        public string playerId;
        public string email;
        public string username;
        public string createdAtUtc;
    }

    // Existing session response — kept for downstream compatibility
    // Internally now backed by Supabase Gotrue JWT tokens
    [Serializable]
    public class PlayerAuthSessionResponse
    {
        public string sessionId;
        public string playerId;
        public string username;
        public string sessionToken; // Supabase access_token
        public string refreshToken;
        public string createdAtUtc;
        public string expiresAtUtc;
        public string lastSeenAtUtc;
    }

    [Serializable]
    class PlayerAuthEmptyResponse { }

    // ── Gotrue (Supabase Auth) internal DTOs ──────────────────────

    [Serializable]
    class GotrueSignupRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    class GotruePasswordGrantRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    class GotrueRefreshGrantRequest
    {
        public string refresh_token;
    }

    [Serializable]
    class GotrueSessionResponse
    {
        public string access_token;
        public string token_type;
        public int expires_in;
        public string refresh_token;
        public GotrueUser user;
    }

    [Serializable]
    class GotrueUserResponse
    {
        public string id;
        public string email;
        public string aud;
        public string role;
    }

    [Serializable]
    class GotrueUser
    {
        public string id;
        public string email;
    }

    [Serializable]
    class GotrueErrorResponse
    {
        public string msg;
        public string error_description;
    }

    // ── Session persistence ──────────────────────────────────────

    static class PlayerSessionStore
    {
        const string RelativePath = "NPCDialogue/player-auth-session.json";

        public static PlayerAuthSessionResponse Load()
        {
            string fullPath = GetFullPath();
            if (!File.Exists(fullPath))
                return null;

            try
            {
                string json = File.ReadAllText(fullPath);
                PlayerAuthSessionResponse session = JsonUtility.FromJson<PlayerAuthSessionResponse>(
                    json
                );
                if (
                    session == null
                    || string.IsNullOrWhiteSpace(session.sessionToken)
                    || IsExpired(session.expiresAtUtc)
                )
                {
                    Clear();
                    return null;
                }

                return session;
            }
            catch
            {
                Clear();
                return null;
            }
        }

        public static void Save(PlayerAuthSessionResponse session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.sessionToken))
            {
                Clear();
                return;
            }

            string fullPath = GetFullPath();
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonUtility.ToJson(session, true));
        }

        public static void Clear()
        {
            string fullPath = GetFullPath();
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public static bool IsExpired(string expiresAtUtc)
        {
            if (string.IsNullOrWhiteSpace(expiresAtUtc))
                return false;

            return DateTime.TryParse(
                    expiresAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime expiresAt
                )
                && expiresAt <= DateTime.UtcNow;
        }

        static string GetFullPath()
        {
            return Path.Combine(Application.persistentDataPath, RelativePath).Replace('\\', '/');
        }
    }

    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    public class PlayerAuthService : MonoBehaviour
    {
        [Title("Player Auth Service — Supabase Gotrue")]
        [FoldoutGroup("Supabase Auth", true, nameof(supabaseUrl), nameof(supabaseAnonKey), nameof(restApiUrl))]
        [HelpBox(
            "Anon key is required for PostgREST access. The URL should point to your self-hosted Gotrue instance. For development, the default localhost:8091 is used. restApiUrl points to PostgREST for player profile creation.",
            MessageMode.Log
        )]
        [SerializeField]
        Void supabaseAuthGroup;

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
                if (host == "localhost" || host == "127.0.0.1")
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

        [FoldoutGroup("Behaviour", true, nameof(requestTimeoutSeconds), nameof(validateStoredSessionOnStart))]
        [SerializeField]
        Void behaviourGroup;

        [SerializeField, HideProperty, Suffix("s")]
        float requestTimeoutSeconds = 15f;

        [SerializeField, HideProperty]
        bool validateStoredSessionOnStart = true;

        [FoldoutGroup("Debug", true, nameof(lastAuthStatus), nameof(lastAuthRoute), nameof(lastAuthDurationMs))]
        [SerializeField]
        Void debugGroup;

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthStatus = "Idle";

        [SerializeField, HideProperty, ReadOnly]
        string lastAuthRoute = string.Empty;

        [SerializeField, HideProperty, ReadOnly]
        long lastAuthDurationMs;

        bool _initialized;

        public PlayerAuthSessionResponse CurrentSession { get; private set; }
        public bool IsAuthenticated =>
            CurrentSession != null
            && !string.IsNullOrWhiteSpace(CurrentSession.sessionToken)
            && !PlayerSessionStore.IsExpired(CurrentSession.expiresAtUtc);
        public string SupabaseUrl => supabaseUrl?.Trim() ?? string.Empty;

        [ShowInInspector]
        string SupabaseUrlPreview => supabaseUrl?.Trim() ?? string.Empty;

        [ShowInInspector]
        string AuthSessionPreview =>
            CurrentSession == null
                ? "<none>"
                : $"{CurrentSession.username} ({CurrentSession.playerId})";

        [ShowInInspector]
        string BackendHealthPreview =>
            $"{BuildGotrueUrl("health")} | timeout={Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds))}s";

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

#if UNITY_WEBGL && !UNITY_EDITOR
            ResolveWebGLUrls();
#endif

            using var scope = NPCFlowScope.Start(
                NPCFlowLogger.FindOrCreate(),
                NPCFlowStage.AuthSession,
                nameof(PlayerAuthService),
                data: new System.Collections.Generic.Dictionary<string, object>
                {
                    ["validateStoredSessionOnStart"] = validateStoredSessionOnStart,
                    ["supabaseUrl"] = supabaseUrl ?? string.Empty,
                }
            );

            CurrentSession = PlayerSessionStore.Load();
            _initialized = true;

            if (CurrentSession == null)
            {
                lastAuthStatus = "No stored auth session found.";
                scope.Skipped(lastAuthStatus);
                return null;
            }

            if (!validateStoredSessionOnStart)
            {
                lastAuthStatus = IsAuthenticated
                    ? $"Stored auth session loaded for {CurrentSession.username}."
                    : "Stored auth session is not valid.";
                scope.Success(lastAuthStatus, BuildSessionData(CurrentSession));
                return IsAuthenticated ? CurrentSession : null;
            }

            PlayerAuthSessionResponse restored = await TryRestoreStoredSessionAsync();
            if (restored == null)
            {
                scope.Warning("Stored auth session could not be restored.");
            }
            else
            {
                scope.Success(
                    $"Stored auth session restored for {restored.username}.",
                    BuildSessionData(restored)
                );
            }

            return restored;
        }

        public async Task<PlayerAuthRegisterResponse> RegisterAsync(
            string username,
            string password
        )
        {
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);
            string rawPassword = password ?? string.Empty;

            // Step 1: signup via Gotrue
            var signupBody = new GotrueSignupRequest { email = email, password = rawPassword };

            GotrueSessionResponse signupResponse = await SendGotrueJsonAsync<GotrueSessionResponse>(
                "signup",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(signupBody)
            );
            if (signupResponse?.user == null || string.IsNullOrWhiteSpace(signupResponse.user.id))
                throw new InvalidOperationException("Supabase signup returned no user.");

            // Create player profile immediately using the signup session token,
            // so a player_profiles row exists from the moment the user registers
            if (!string.IsNullOrWhiteSpace(signupResponse.access_token))
            {
                string previousSessionToken = CurrentSession?.sessionToken;
                CurrentSession = new PlayerAuthSessionResponse
                {
                    playerId = signupResponse.user.id,
                    sessionToken = signupResponse.access_token,
                    username = username?.Trim() ?? string.Empty,
                };
                await TryCreatePlayerProfileAsync(username?.Trim() ?? string.Empty);
                CurrentSession = previousSessionToken != null
                    ? new PlayerAuthSessionResponse { sessionToken = previousSessionToken }
                    : null;
            }

            return new PlayerAuthRegisterResponse
            {
                playerId = signupResponse.user.id,
                email = signupResponse.user.email ?? email,
                username = username?.Trim() ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }

        async Task TryCreatePlayerProfileAsync(string displayName)
        {
            if (string.IsNullOrWhiteSpace(restApiUrl) || string.IsNullOrWhiteSpace(supabaseAnonKey))
                return;

            string url = $"{restApiUrl.TrimEnd('/')}/rpc/create_or_update_player_profile";
            var body = new System.Collections.Generic.Dictionary<string, object>
            {
                ["p_display_name"] = displayName?.Trim() ?? string.Empty,
            };
            string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body);

            try
            {
                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("apikey", supabaseAnonKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrWhiteSpace(CurrentSession?.sessionToken))
                    request.SetRequestHeader("Authorization", $"Bearer {CurrentSession.sessionToken}");
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.responseCode >= 200 && request.responseCode < 300)
                {
                    NPCFlowLogger.FindOrCreate()?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Success,
                        NPCFlowLogLevel.Debug,
                        $"Player profile created/updated for '{displayName}'.",
                        source: nameof(PlayerAuthService)
                    );
                }
                else
                {
                    NPCFlowLogger.FindOrCreate()?.Log(
                        NPCFlowStage.ConfigurationValidation,
                        NPCFlowStatus.Warning,
                        NPCFlowLogLevel.Warning,
                        $"Player profile RPC returned HTTP {request.responseCode}: {request.downloadHandler?.text ?? request.error}",
                        source: nameof(PlayerAuthService)
                    );
                }
            }
            catch (System.Exception ex)
            {
                NPCFlowLogger.FindOrCreate()?.Log(
                    NPCFlowStage.ConfigurationValidation,
                    NPCFlowStatus.Warning,
                    NPCFlowLogLevel.Warning,
                    $"Player profile creation failed: {ex.Message}",
                    source: nameof(PlayerAuthService)
                );
            }
        }

        public async Task<PlayerAuthSessionResponse> LoginAsync(
            string username,
            string password,
            bool rememberMe
        )
        {
            string email = EmailFromUsername(username?.Trim() ?? string.Empty);
            PlayerAuthSessionResponse session = await LoginInternalAsync(
                email,
                password ?? string.Empty,
                rememberMe
            );
            return session;
        }

        public async Task<PlayerAuthSessionResponse> TryRestoreStoredSessionAsync()
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.sessionToken))
                return null;

            if (PlayerSessionStore.IsExpired(CurrentSession.expiresAtUtc))
            {
                // Try refresh before giving up
                bool refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                {
                    ClearLocalSession();
                    return null;
                }
                return CurrentSession;
            }

            // Validate the stored token
            try
            {
                GotrueUserResponse user = await SendGotrueJsonAsync<GotrueUserResponse>(
                    "user",
                    UnityWebRequest.kHttpVerbGET,
                    null,
                    CurrentSession.sessionToken
                );
                if (user == null || string.IsNullOrWhiteSpace(user.id))
                {
                    ClearLocalSession();
                    return null;
                }

                // Session is valid — update playerId if needed
                CurrentSession.playerId = user.id;
                PlayerSessionStore.Save(CurrentSession);

                // Update profile on session restore (last_login_at, is_online)
                await TryCreatePlayerProfileAsync(CurrentSession.username);
                return CurrentSession;
            }
            catch
            {
                // Token might be expired — try refresh
                bool refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                {
                    ClearLocalSession();
                    return null;
                }

                // Update profile on token refresh
                await TryCreatePlayerProfileAsync(CurrentSession.username);
                return CurrentSession;
            }
        }

        public async Task LogoutAsync()
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.sessionToken))
            {
                ClearLocalSession();
                return;
            }

            using var scope = NPCFlowScope.Start(
                NPCFlowLogger.FindOrCreate(),
                NPCFlowStage.AuthSession,
                nameof(PlayerAuthService),
                data: BuildSessionData(CurrentSession)
            );
            try
            {
                await SendGotrueJsonAsync<PlayerAuthEmptyResponse>(
                    "logout",
                    UnityWebRequest.kHttpVerbPOST,
                    "{}",
                    CurrentSession.sessionToken
                );
                lastAuthStatus = $"Logged out auth session for {CurrentSession.username}.";
                scope.Success(lastAuthStatus);
            }
            catch (Exception ex)
            {
                scope.Error(ex, $"Logout request failed: {ex.Message}");
            }
            finally
            {
                ClearLocalSession();
            }
        }

        // ── Internal auth helpers ─────────────────────────────────

        async Task<PlayerAuthSessionResponse> LoginInternalAsync(
            string email,
            string password,
            bool rememberMe
        )
        {
            var body = new GotruePasswordGrantRequest { email = email, password = password };

            GotrueSessionResponse gotrueSession = await SendGotrueJsonAsync<GotrueSessionResponse>(
                "token?grant_type=password",
                UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(body)
            );
            if (
                gotrueSession == null
                || string.IsNullOrWhiteSpace(gotrueSession.access_token)
                || gotrueSession.user == null
            )
                throw new InvalidOperationException("Supabase login returned an invalid session.");

            int expiresIn = Math.Max(gotrueSession.expires_in, 3600);
            string username = UsernameFromEmail(gotrueSession.user.email);

            var session = new PlayerAuthSessionResponse
            {
                sessionId = gotrueSession.user.id,
                playerId = gotrueSession.user.id,
                username = username,
                sessionToken = gotrueSession.access_token,
                refreshToken = gotrueSession.refresh_token ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"),
                lastSeenAtUtc = DateTime.UtcNow.ToString("O"),
            };

            CurrentSession = session;
            if (rememberMe)
                PlayerSessionStore.Save(session);
            else
                PlayerSessionStore.Clear();

            // Create/update player profile via PostgREST RPC
            await TryCreatePlayerProfileAsync(username);

            lastAuthStatus = $"Login successful for '{username}'. Session expires at {session.expiresAtUtc}.";
            return session;
        }

        async Task<bool> TryRefreshTokenAsync()
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.refreshToken))
                return false;

            try
            {
                var body = new GotrueRefreshGrantRequest
                {
                    refresh_token = CurrentSession.refreshToken,
                };

                GotrueSessionResponse refreshed = await SendGotrueJsonAsync<GotrueSessionResponse>(
                    "token?grant_type=refresh_token",
                    UnityWebRequest.kHttpVerbPOST,
                    JsonUtility.ToJson(body)
                );
                if (
                    refreshed == null
                    || string.IsNullOrWhiteSpace(refreshed.access_token)
                    || refreshed.user == null
                )
                    return false;

                int expiresIn = Math.Max(refreshed.expires_in, 3600);
                string username = UsernameFromEmail(refreshed.user.email);

                CurrentSession.playerId = refreshed.user.id;
                CurrentSession.username = username;
                CurrentSession.sessionToken = refreshed.access_token;
                CurrentSession.refreshToken =
                    refreshed.refresh_token ?? CurrentSession.refreshToken;
                CurrentSession.expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O");
                CurrentSession.lastSeenAtUtc = DateTime.UtcNow.ToString("O");
                PlayerSessionStore.Save(CurrentSession);

                lastAuthStatus = $"Session refreshed for {username}.";
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Gotrue HTTP layer ─────────────────────────────────────

        async Task<TResponse> SendGotrueJsonAsync<TResponse>(
            string route,
            string method,
            string jsonBody = null,
            string bearerToken = null
        )
        {
            string url = BuildGotrueUrl(route);
            NPCFlowLogger logger = NPCFlowLogger.FindOrCreate();
            int timeoutSeconds = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));
            lastAuthRoute = route?.Trim() ?? string.Empty;
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["route"] = route ?? string.Empty,
                ["method"] = method ?? string.Empty,
                ["url"] = url,
                ["timeoutSeconds"] = timeoutSeconds,
                ["hasJsonBody"] = !string.IsNullOrWhiteSpace(jsonBody),
                ["hasBearerToken"] = !string.IsNullOrWhiteSpace(bearerToken),
            };
            using var scope = NPCFlowScope.Start(
                logger,
                NPCFlowStage.AuthRequest,
                nameof(PlayerAuthService),
                data: data
            );
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("apikey", supabaseAnonKey);

            if (!string.IsNullOrWhiteSpace(jsonBody))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            }

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (
                request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
            )
            {
                string error = ParseGotrueError(
                    request.downloadHandler.text,
                    request.error,
                    request.responseCode
                );
                lastAuthDurationMs = scope.ElapsedMilliseconds;
                lastAuthStatus =
                    $"Auth request failed: {route} -> HTTP {request.responseCode} {error}";
                scope.Error(
                    new InvalidOperationException(error),
                    lastAuthStatus,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["responseCode"] = request.responseCode,
                        ["responseText"] = request.downloadHandler.text ?? string.Empty,
                    }
                );
                throw new InvalidOperationException(error);
            }

            // 204 No Content (e.g. logout) — return default
            if (
                request.responseCode == 204
                || string.IsNullOrWhiteSpace(request.downloadHandler.text)
            )
            {
                lastAuthDurationMs = scope.ElapsedMilliseconds;
                lastAuthStatus = $"Auth request succeeded (HTTP {request.responseCode}): {route}";
                scope.Success(lastAuthStatus);
                return default;
            }

            string responseText = request.downloadHandler.text;
            TResponse response = JsonUtility.FromJson<TResponse>(responseText);
            if (response == null)
            {
                string error = "Auth server returned an unreadable response.";
                lastAuthDurationMs = scope.ElapsedMilliseconds;
                lastAuthStatus = $"{route} failed: {error}";
                scope.Error(new InvalidOperationException(error), lastAuthStatus);
                throw new InvalidOperationException(error);
            }

            lastAuthDurationMs = scope.ElapsedMilliseconds;
            lastAuthStatus = $"Auth request succeeded: {route} (HTTP {request.responseCode})";
            scope.Success(lastAuthStatus);

            return response;
        }

        void ClearLocalSession()
        {
            CurrentSession = null;
            PlayerSessionStore.Clear();
        }

        string BuildGotrueUrl(string route)
        {
            return $"{supabaseUrl.TrimEnd('/')}/{route.TrimStart('/')}";
        }

        static string ParseGotrueError(string responseText, string fallback, long statusCode)
        {
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                GotrueErrorResponse err = JsonUtility.FromJson<GotrueErrorResponse>(responseText);
                if (err != null)
                {
                    if (!string.IsNullOrWhiteSpace(err.error_description))
                        return err.error_description;
                    if (!string.IsNullOrWhiteSpace(err.msg))
                        return err.msg;
                }
            }

            if (statusCode == 401)
                return "Invalid or expired session token.";
            if (statusCode == 422)
                return "Invalid email or password format.";
            if (statusCode == 429)
                return "Rate limited. Please wait and try again.";

            return string.IsNullOrWhiteSpace(fallback) ? "Auth request failed." : fallback;
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
