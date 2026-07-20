using System;
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

namespace NPCSystem.Network.Core
{
    /// <summary>
    /// Shared networking utilities used across dialogue, auth, and bootstrap components.
    /// </summary>
    public static class NPCNetworkUtils
    {
        /// <summary>
        /// Returns true when <paramref name="host"/> is a loopback address
        /// (localhost or 127.0.0.1). Used by WebGL URL-resolution logic across
        /// multiple auth and dialogue components.
        /// </summary>
        public static bool IsLocalHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.Ordinal);
        }
    }
}
