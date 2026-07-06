using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NPCSystem
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NPCServerCharacter : NetworkBehaviour
    {
        static NPCServerCharacter()
        {
            UserNetworkVariableSerialization<string>.WriteValue = (
                FastBufferWriter writer,
                in string value
            ) =>
            {
                writer.WriteValueSafe(value);
            };
            UserNetworkVariableSerialization<string>.ReadValue = (
                FastBufferReader reader,
                out string value
            ) =>
            {
                reader.ReadValueSafe(out value);
            };
            UserNetworkVariableSerialization<string>.DuplicateValue = (
                in string value,
                ref string duplicatedValue
            ) =>
            {
                duplicatedValue = value;
            };
        }

        public readonly NetworkVariable<string> npcSlug = new NetworkVariable<string>(
            string.Empty,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public readonly NetworkVariable<string> npcDisplayName = new NetworkVariable<string>(
            string.Empty,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public string DisplayName =>
            string.IsNullOrWhiteSpace(npcDisplayName.Value) ? "NPC" : npcDisplayName.Value;
        public string Slug => string.IsNullOrWhiteSpace(npcSlug.Value) ? "npc" : npcSlug.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            npcSlug.OnValueChanged += HandleIdentityChanged;
            npcDisplayName.OnValueChanged += HandleIdentityChanged;
            RefreshHierarchyName();

            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.NpcSpawn,
                    NPCFlowStatus.Success,
                    NPCFlowLogLevel.Info,
                    $"Server NPC '{DisplayName}' spawned.",
                    source: nameof(NPCServerCharacter),
                    npcSlug: Slug,
                    data: new Dictionary<string, object>
                    {
                        ["networkObjectId"] = NetworkObjectId,
                        ["isServer"] = IsServer,
                    }
                );
        }

        public override void OnNetworkDespawn()
        {
            npcSlug.OnValueChanged -= HandleIdentityChanged;
            npcDisplayName.OnValueChanged -= HandleIdentityChanged;
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.NpcSpawn,
                    NPCFlowStatus.Warning,
                    NPCFlowLogLevel.Info,
                    $"Server NPC '{DisplayName}' despawned.",
                    source: nameof(NPCServerCharacter),
                    npcSlug: Slug
                );
            base.OnNetworkDespawn();
        }

        public void InitializeIdentity(NPCProfile profile)
        {
            if (!IsServer || profile == null)
            {
                return;
            }

            npcSlug.Value = profile.GetNpcSlug();
            npcDisplayName.Value = profile.GetDisplayName();
            RefreshHierarchyName();
        }

        void HandleIdentityChanged(string _, string __)
        {
            RefreshHierarchyName();
        }

        void RefreshHierarchyName()
        {
            gameObject.name = $"NPCServerCharacter_{Slug}_{DisplayName}";
        }
    }
}
