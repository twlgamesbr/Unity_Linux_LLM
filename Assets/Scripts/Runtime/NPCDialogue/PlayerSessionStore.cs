using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace NPCSystem
{
    /// <summary>
    /// Persists <see cref="PlayerAuthSessionResponse"/> to disk for session restore across game restarts.
    /// </summary>
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
}
