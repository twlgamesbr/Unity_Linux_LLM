using System;
using Unity.Netcode;

namespace NPCSystem
{
    [Serializable]
    public struct NPCDialogueSelectionMessage : INetworkSerializable
    {
        public string npcSlug;

        public void SanitizeInPlace()
        {
            npcSlug = string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
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
            requestId = string.IsNullOrWhiteSpace(requestId)
                ? Guid.NewGuid().ToString("N")
                : requestId.Trim();
            npcSlug = string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
            playerMessage = string.IsNullOrWhiteSpace(playerMessage)
                ? string.Empty
                : playerMessage.Trim();
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
            npcSlug = string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();
            content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim();
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

    [Serializable]
    public struct NPCNotebookStateMessage : INetworkSerializable
    {
        public string npcSlug;
        public string notesPageLeft;
        public string notesPageRight;

        public void SanitizeInPlace()
        {
            npcSlug = string.IsNullOrWhiteSpace(npcSlug)
                ? string.Empty
                : npcSlug.Trim().ToLowerInvariant();
            notesPageLeft = string.IsNullOrWhiteSpace(notesPageLeft)
                ? string.Empty
                : notesPageLeft.Trim();
            notesPageRight = string.IsNullOrWhiteSpace(notesPageRight)
                ? string.Empty
                : notesPageRight.Trim();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref npcSlug);
            serializer.SerializeValue(ref notesPageLeft);
            serializer.SerializeValue(ref notesPageRight);
        }
    }
}
