using System;

namespace NPCSystem
{
    // ── Auth response DTOs (kept for backward compat) ──────────────

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
    /// Full auth session state, backed by Supabase Gotrue JWT tokens
    /// from the supabase-csharp SDK.
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
}
