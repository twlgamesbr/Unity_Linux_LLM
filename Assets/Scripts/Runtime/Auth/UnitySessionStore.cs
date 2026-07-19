using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using UnityEngine;


using NPCSystem.Monitoring;
using NPCSystem.Dialogue.Core;
using NPCSystem.Network.Core;
using NPCSystem.Character.Player;
using NPCSystem.Auth;
using NPCSystem.Items;
using NPCSystem.LocalAI;
using NPCSystem.Initialization;
using NPCSystem.Character.NPC;
using NPCSystem.Dialogue.Session;
using NPCSystem.Dialogue.UI;
using NPCSystem.Dialogue.RAG;
using NPCSystem.Dialogue.Persistence;
namespace NPCSystem.Auth
{
    /// <summary>
    /// Persists <see cref="Supabase.Gotrue.Session"/> to disk for session restore,
    /// implementing <see cref="IGotrueSessionPersistence{Session}"/> for the supabase-csharp SDK.
    /// Also provides backward-compatible static helpers for <see cref="PlayerAuthSessionResponse"/>.
    /// </summary>
    public class UnitySessionStore : IGotrueSessionPersistence<Session>
    {
        const string RelativePath = "NPCDialogue/player-auth-session.json";

        // ── IGotrueSessionPersistence<Session> ─────────────────────

        public void SaveSession(Session session)
        {
            if (session == null)
            {
                DestroySession();
                return;
            }

            string fullPath = GetFullPath();
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(session, Formatting.Indented);
            File.WriteAllText(fullPath, json);
        }

        public void DestroySession()
        {
            string fullPath = GetFullPath();
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public Session LoadSession()
        {
            string fullPath = GetFullPath();
            if (!File.Exists(fullPath))
                return null;

            try
            {
                string json = File.ReadAllText(fullPath);
                var session = JsonConvert.DeserializeObject<Session>(json);

                if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
                {
                    DestroySession();
                    return null;
                }

                return session;
            }
            catch
            {
                DestroySession();
                return null;
            }
        }

        // ── Backward-compat static helpers ────────────────────────

        public static PlayerAuthSessionResponse ToAuthSession(Session session)
        {
            string username = "unknown";
            if (session.User != null && !string.IsNullOrWhiteSpace(session.User.Email))
            {
                int atIndex = session.User.Email.IndexOf('@');
                username = atIndex > 0
                    ? session.User.Email.Substring(0, atIndex)
                    : session.User.Email;
            }

            return new PlayerAuthSessionResponse
            {
                sessionId = session.User?.Id ?? string.Empty,
                playerId = session.User?.Id ?? string.Empty,
                username = username,
                sessionToken = session.AccessToken ?? string.Empty,
                refreshToken = session.RefreshToken ?? string.Empty,
                createdAtUtc = session.CreatedAt.ToString("O"),
                expiresAtUtc = session.ExpiresAt().ToString("O"),
                lastSeenAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }

        public static PlayerAuthSessionResponse Load()
        {
            var store = new UnitySessionStore();
            var session = store.LoadSession();
            return ToAuthSession(session);
        }

        public static void Save(PlayerAuthSessionResponse authSession)
        {
            if (authSession == null || string.IsNullOrWhiteSpace(authSession.sessionToken))
            {
                Clear();
                return;
            }

            var store = new UnitySessionStore();
            var session = new Session
            {
                AccessToken = authSession.sessionToken,
                RefreshToken = authSession.refreshToken ?? string.Empty,
                TokenType = "bearer",
                CreatedAt = DateTime.UtcNow,
                User = new User
                {
                    Id = authSession.playerId,
                    Email = $"{authSession.username}@npc-game.local",
                },
            };

            if (!string.IsNullOrWhiteSpace(authSession.expiresAtUtc)
                && DateTime.TryParse(
                    authSession.expiresAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime expiresAt
                ))
            {
                session.ExpiresIn = (long)(expiresAt - DateTime.UtcNow).TotalSeconds;
                if (session.ExpiresIn < 0)
                    session.ExpiresIn = 0;
            }
            else
            {
                session.ExpiresIn = 3600;
            }

            store.SaveSession(session);
        }

        public static void Clear()
        {
            var store = new UnitySessionStore();
            store.DestroySession();
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
}
