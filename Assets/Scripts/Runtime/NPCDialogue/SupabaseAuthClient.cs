using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NPCSystem
{
    /// <summary>
    /// Static WebGL HTTP transport for Supabase Gotrue auth endpoints.
    /// Used by PlayerAuthService on WebGL platforms where the SDK client cannot be used directly.
    /// </summary>
    public static class SupabaseAuthClient
    {
        public static async Task<PlayerAuthRegisterResponse> RegisterWebGLAsync(
            string supabaseUrl,
            string supabaseAnonKey,
            string username,
            string password,
            float timeoutSeconds
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

            await SendWebRequestAsync(req, timeoutSeconds);

            string json = req.downloadHandler.text;
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

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

            return new PlayerAuthRegisterResponse
            {
                playerId = userId,
                email = email,
                username = username?.Trim() ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }

        public static async Task<PlayerAuthSessionResponse> LoginWebGLAsync(
            string supabaseUrl,
            string supabaseAnonKey,
            string username,
            string password,
            bool rememberMe,
            float timeoutSeconds
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

            await SendWebRequestAsync(req, timeoutSeconds);

            string json = req.downloadHandler.text;
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            string accessToken = result?.GetValueOrDefault("access_token")?.ToString()
                ?? throw new InvalidOperationException("Login returned no access token.");
            string refreshToken = result?.GetValueOrDefault("refresh_token")?.ToString() ?? string.Empty;
            long expiresIn = 3600L;
            if (result?.TryGetValue("expires_in", out var expVal) == true)
                long.TryParse(expVal?.ToString(), out expiresIn);

            string userId = string.Empty;
            if (result?.TryGetValue("user", out var userObj) == true && userObj is JObject userJObj)
            {
                userId = userJObj.Value<string>("id") ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("Login returned no user ID.");

            return new PlayerAuthSessionResponse
            {
                playerId = userId,
                username = username?.Trim() ?? string.Empty,
                sessionToken = accessToken,
                refreshToken = refreshToken,
                expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O"),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }

        public static async Task CreatePlayerProfileWebGLAsync(
            string supabaseUrl,
            string restApiUrl,
            string supabaseAnonKey,
            string displayName,
            string sessionToken,
            float timeoutSeconds
        )
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return;

            try
            {
                string url = ResolveWebGLProxyUrl(restApiUrl, "/rpc/create_or_update_player_profile");
                using var req = new UnityWebRequest(url, "POST");
                byte[] body = System.Text.Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { p_display_name = displayName.Trim() })
                );
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", supabaseAnonKey);
                if (!string.IsNullOrWhiteSpace(sessionToken))
                    req.SetRequestHeader("Authorization", $"Bearer {sessionToken}");

                await SendWebRequestAsync(req, timeoutSeconds);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SupabaseAuthClient] WebGL profile creation: {ex.Message}");
            }
        }

        public static async Task<PlayerAuthSessionResponse> RefreshSessionWebGLAsync(
            string supabaseUrl,
            string supabaseAnonKey,
            PlayerAuthSessionResponse currentSession,
            float timeoutSeconds
        )
        {
            if (currentSession == null || string.IsNullOrWhiteSpace(currentSession.refreshToken))
                return null;

            try
            {
                string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/token?grant_type=refresh_token");
                using var req = new UnityWebRequest(url, "POST");
                byte[] body = System.Text.Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new { refresh_token = currentSession.refreshToken })
                );
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", supabaseAnonKey);

                await SendWebRequestAsync(req, timeoutSeconds);

                string json = req.downloadHandler.text;
                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                string accessToken = result?.GetValueOrDefault("access_token")?.ToString();
                string newRefreshToken = result?.GetValueOrDefault("refresh_token")?.ToString();
                long expiresIn = 3600L;
                if (result?.TryGetValue("expires_in", out var expVal) == true)
                    long.TryParse(expVal?.ToString(), out expiresIn);

                if (string.IsNullOrWhiteSpace(accessToken))
                    return null;

                currentSession.sessionToken = accessToken;
                currentSession.refreshToken = newRefreshToken ?? currentSession.refreshToken;
                currentSession.expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn).ToString("O");
                return currentSession;
            }
            catch
            {
                return null;
            }
        }

        public static async Task LogoutWebGLAsync(
            string supabaseUrl,
            string supabaseAnonKey,
            PlayerAuthSessionResponse currentSession,
            float timeoutSeconds
        )
        {
            try
            {
                if (currentSession != null && !string.IsNullOrWhiteSpace(currentSession.sessionToken))
                {
                    string url = ResolveWebGLProxyUrl(supabaseUrl, "/auth/logout");
                    using var req = new UnityWebRequest(url, "POST");
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Authorization", $"Bearer {currentSession.sessionToken}");
                    req.SetRequestHeader("apikey", supabaseAnonKey);
                    await SendWebRequestAsync(req, timeoutSeconds);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SupabaseAuthClient] WebGL logout: {ex.Message}");
            }
        }

        public static void ResolveWebGLUrls(
            string supabaseUrl,
            string restApiUrl,
            out string newSupabaseUrl,
            out string newRestApiUrl
        )
        {
            newSupabaseUrl = supabaseUrl;
            newRestApiUrl = restApiUrl;

            try
            {
                Uri pageUri = new Uri(Application.absoluteURL);
                string host = pageUri.Host;
                if (NPCNetworkUtils.IsLocalHost(host))
                    return;

                newSupabaseUrl = ReplaceHost(supabaseUrl, host);
                newRestApiUrl = ReplaceHost(restApiUrl, host);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SupabaseAuthClient] Failed to dynamically resolve WebGL URLs: {ex.Message}"
                );
            }
        }

        public static async Task SendWebRequestAsync(UnityWebRequest req, float timeoutSeconds)
        {
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            var tcs = new TaskCompletionSource<bool>();

            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                if (req.result == UnityWebRequest.Result.ConnectionError
                    || req.result == UnityWebRequest.Result.ProtocolError)
                {
                    string errorBody = req.downloadHandler?.text ?? string.Empty;
                    string msg = $"[{req.method}] {req.url} \u2192 {req.responseCode}: {req.error}";
                    if (!string.IsNullOrWhiteSpace(errorBody))
                        msg += "\n" + errorBody;
                    tcs.TrySetException(new InvalidOperationException(msg));
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            };

            await tcs.Task;
        }

        public static string ResolveWebGLProxyUrl(string originalUrl, string path)
        {
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
                catch { }
#endif
                return $"http://{host}:{port}{path}";
            }
            catch
            {
                return $"http://localhost:8085{path}";
            }
        }

        public static string ReplaceHost(string url, string newHost)
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

        static string EmailFromUsername(string username)
        {
            return $"{username}@npc-game.local";
        }
    }
}
