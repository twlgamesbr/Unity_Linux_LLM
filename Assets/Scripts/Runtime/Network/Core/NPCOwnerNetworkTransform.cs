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
using Unity.Netcode.Components;

namespace NPCSystem.Network.Core
{
    /// <summary>
    /// Owner-authoritative NetworkTransform for player avatars. The owning client moves its
    /// CharacterController locally and Netcode replicates the resulting transform to the server
    /// and other clients.
    /// </summary>
    public class NPCOwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
