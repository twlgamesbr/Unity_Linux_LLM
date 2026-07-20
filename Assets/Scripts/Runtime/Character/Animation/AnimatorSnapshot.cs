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

namespace NPCSystem.Character.Animation
{
    /// <summary>
    /// Network-serializable snapshot of animation parameters.
    /// Captures the minimal state needed to drive a locomotion Animator Controller.
    /// </summary>
    public struct AnimatorSnapshot : INetworkSerializable
    {
        // ── Blend shape inputs ──
        public float MoveX;
        public float MoveY;
        public float Speed;
        public float MotionSpeed;

        // ── State booleans ──
        public bool Grounded;
        public bool Sprinting;

        // ── Triggers (single-frame, consumed on apply) ──
        public bool JumpTriggered;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveY);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref MotionSpeed);
            serializer.SerializeValue(ref Grounded);
            serializer.SerializeValue(ref Sprinting);
            serializer.SerializeValue(ref JumpTriggered);
        }
    }
}
