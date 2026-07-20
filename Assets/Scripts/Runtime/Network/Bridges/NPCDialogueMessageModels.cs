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
using Unity.Netcode;

namespace NPCSystem.Network.Bridges
{
    [Serializable]
    public struct NPCDialogueSelectionMessage : INetworkSerializable
    {
        public string npcSlug;

        public void SanitizeInPlace()
        {
            npcSlug = string.IsNullOrWhiteSpace(npcSlug) ? string.Empty : npcSlug.Trim().ToLowerInvariant();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref npcSlug);
        }
    }

    [Serializable]
    public struct NPCDialogueRequestMessage : INetworkSerializable
    {
        public string requestId;
        public string npcSlug;
        public string playerMessage;

        public void SanitizeInPlace()
        {
            requestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId.Trim();
            npcSlug = string.IsNullOrWhiteSpace(npcSlug) ? string.Empty : npcSlug.Trim().ToLowerInvariant();
            playerMessage = string.IsNullOrWhiteSpace(playerMessage) ? string.Empty : playerMessage.Trim();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref requestId);
            serializer.SerializeValue(ref npcSlug);
            serializer.SerializeValue(ref playerMessage);
        }
    }

    [Serializable]
    public struct NPCDialogueResponseMessage : INetworkSerializable
    {
        public string requestId;
        public string npcSlug;
        public string displayName;
        public string content;

        public void SanitizeInPlace()
        {
            requestId = string.IsNullOrWhiteSpace(requestId) ? string.Empty : requestId.Trim();
            npcSlug = string.IsNullOrWhiteSpace(npcSlug) ? string.Empty : npcSlug.Trim().ToLowerInvariant();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : NPCFlowTextSanitizer.CleanDialogueText(displayName);
            content = string.IsNullOrWhiteSpace(content)
                ? string.Empty
                : NPCFlowTextSanitizer.CleanDialogueText(content);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref requestId);
            serializer.SerializeValue(ref npcSlug);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref content);
        }
    }
}
