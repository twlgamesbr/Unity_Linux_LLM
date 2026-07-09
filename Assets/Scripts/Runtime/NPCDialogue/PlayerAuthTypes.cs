using System;

namespace NPCSystem
{
    // ── Auth response DTOs ────────────────────────────────────────

    /// <summary>
    /// Response from a player registration request.
    /// </summary>
    [Serializable]
    public class PlayerAuthRegisterResponse
    {
        public string playerId;
        public string email;
        public string username;
        public string createdAtUtc;
    }

    /// <summary>
    /// Full auth session state, backed by Supabase Gotrue JWT tokens.
    /// </summary>
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
}
