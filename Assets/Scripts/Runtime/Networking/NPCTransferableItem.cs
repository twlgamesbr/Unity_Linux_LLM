using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace NPCSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public sealed class NPCTransferableItem : NetworkBehaviour
    {
        enum HolderType
        {
            World = 0,
            Npc = 1,
            Player = 2,
        }

        [Header("Identity")]
        [FormerlySerializedAs("itemId")]
        public string ItemId = "evidence-ledger";
        [FormerlySerializedAs("displayName")]
        public string DisplayName = "Evidence Ledger";
        [FormerlySerializedAs("initialNpcHolderSlug")]
        public string InitialNpcHolderSlug = "butler";

        [Header("Follow")]
        [FormerlySerializedAs("playerHoldOffset")]
        public Vector3 PlayerHoldOffset = new Vector3(0f, 1.1f, 0.8f);
        [FormerlySerializedAs("npcHoldOffset")]
        public Vector3 NpcHoldOffset = new Vector3(0.65f, 1.1f, 0f);
        [FormerlySerializedAs("followSharpness")]
        public float FollowSharpness = 20f;

        [HideInInspector]
        public readonly NetworkVariable<FixedString64Bytes> itemIdValue =
            new NetworkVariable<FixedString64Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        [HideInInspector]
        public readonly NetworkVariable<FixedString64Bytes> displayNameValue =
            new NetworkVariable<FixedString64Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        readonly NetworkVariable<int> _holderType = new NetworkVariable<int>(
            (int)HolderType.World,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        readonly NetworkVariable<FixedString64Bytes> _npcHolderSlug =
            new NetworkVariable<FixedString64Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public string ItemId =>
            (itemIdValue.Value.Length > 0) ? itemIdValue.Value.ToString() : ItemId;
        public string DisplayName =>
            (displayNameValue.Value.Length > 0) ? displayNameValue.Value.ToString() : DisplayName;
        public bool IsHeldByPlayer => _holderType.Value == (int)HolderType.Player;
        public bool IsHeldByNpc => _holderType.Value == (int)HolderType.Npc;

        void Awake()
        {
            ItemId = NormalizeId(ItemId, "evidence-ledger");
            DisplayName = string.IsNullOrWhiteSpace(DisplayName)
                ? "Evidence Ledger"
                : DisplayName.Trim();
            InitialNpcHolderSlug = NormalizeId(InitialNpcHolderSlug, "butler");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                itemIdValue.Value = new FixedString64Bytes(ItemId);
                displayNameValue.Value = new FixedString64Bytes(DisplayName);
            }

            RefreshHierarchyName();
            itemIdValue.OnValueChanged += HandleIdentityChanged;
            displayNameValue.OnValueChanged += HandleIdentityChanged;
            _npcHolderSlug.OnValueChanged += HandleIdentityChanged;
            _holderType.OnValueChanged += HandleHolderChanged;
        }

        public override void OnNetworkDespawn()
        {
            itemIdValue.OnValueChanged -= HandleIdentityChanged;
            displayNameValue.OnValueChanged -= HandleIdentityChanged;
            _npcHolderSlug.OnValueChanged -= HandleIdentityChanged;
            _holderType.OnValueChanged -= HandleHolderChanged;
            base.OnNetworkDespawn();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !IsServer || !IsSpawned)
            {
                return;
            }

            Transform target = ResolveFollowTarget();
            if (target == null)
            {
                return;
            }

            Vector3 offset = IsHeldByNpc ? NpcHoldOffset : PlayerHoldOffset;
            Vector3 targetPosition = target.position + target.rotation * offset;
            float t = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }

        public void AssignToPlayer(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            _holderType.Value = (int)HolderType.Player;
            _npcHolderSlug.Value = default;
            if (OwnerClientId != clientId)
            {
                NetworkObject.ChangeOwnership(clientId);
            }

            LogOwnershipEvent(
                NPCFlowStatus.Success,
                $"Assigned item '{DisplayName}' to player client {clientId}.",
                clientId,
                string.Empty
            );
        }

        public void AssignToNpc(string npcSlug)
        {
            if (!IsServer)
            {
                return;
            }

            string normalizedNpcSlug = NormalizeId(npcSlug, InitialNpcHolderSlug);
            _holderType.Value = (int)HolderType.Npc;
            _npcHolderSlug.Value = new FixedString64Bytes(normalizedNpcSlug);
            if (!NetworkObject.IsOwnedByServer)
            {
                NetworkObject.RemoveOwnership();
            }

            LogOwnershipEvent(
                NPCFlowStatus.Success,
                $"Assigned item '{DisplayName}' to NPC '{normalizedNpcSlug}'.",
                NetworkManager.ServerClientId,
                normalizedNpcSlug
            );
        }

        public void PlaceInWorld(Vector3 worldPosition)
        {
            if (!IsServer)
            {
                return;
            }

            _holderType.Value = (int)HolderType.World;
            _npcHolderSlug.Value = default;
            if (!NetworkObject.IsOwnedByServer)
            {
                NetworkObject.RemoveOwnership();
            }

            transform.position = worldPosition;
            LogOwnershipEvent(
                NPCFlowStatus.Success,
                $"Placed item '{DisplayName}' in the world.",
                NetworkManager.ServerClientId,
                string.Empty
            );
        }

        Transform ResolveFollowTarget()
        {
            if (IsHeldByNpc)
            {
                string npcSlug = _npcHolderSlug.Value.ToString();
                NPCServerCharacter[] npcCharacters = FindObjectsByType<NPCServerCharacter>(
                    FindObjectsInactive.Include
                );
                foreach (NPCServerCharacter npcCharacter in npcCharacters)
                {
                    if (npcCharacter != null && npcCharacter.Slug == npcSlug)
                    {
                        return npcCharacter.transform;
                    }
                }

                return null;
            }

            if (!IsHeldByPlayer)
            {
                return null;
            }

            NPCPlayerNetworkAvatar[] playerAvatars = FindObjectsByType<NPCPlayerNetworkAvatar>(
                FindObjectsInactive.Include
            );
            foreach (NPCPlayerNetworkAvatar avatar in playerAvatars)
            {
                if (avatar != null && avatar.OwnerClientId == OwnerClientId)
                {
                    return avatar.transform;
                }
            }

            return null;
        }

        void HandleIdentityChanged(FixedString64Bytes _, FixedString64Bytes __)
        {
            RefreshHierarchyName();
        }

        void HandleHolderChanged(int _, int __)
        {
            RefreshHierarchyName();
        }

        void RefreshHierarchyName()
        {
            string holder =
                IsHeldByNpc ? _npcHolderSlug.Value.ToString()
                : IsHeldByPlayer ? $"Client{OwnerClientId}"
                : "World";
            gameObject.name = $"NPCTransferableItem_{ItemId}_{holder}";
        }

        void LogOwnershipEvent(
            NPCFlowStatus status,
            string message,
            ulong ownerClientId,
            string npcSlug
        )
        {
            NPCFlowLogger
                .FindOrCreate()
                ?.Log(
                    NPCFlowStage.OwnershipAuthority,
                    status,
                    NPCFlowLogLevel.Info,
                    message,
                    source: nameof(NPCTransferableItem),
                    npcSlug: npcSlug,
                    data: new Dictionary<string, object>
                    {
                        ["ownerClientId"] = ownerClientId,
                        ["ItemId"] = ItemId,
                        ["holderType"] = ((HolderType)_holderType.Value).ToString(),
                    }
                );
        }

        static string NormalizeId(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        }
    }
}
