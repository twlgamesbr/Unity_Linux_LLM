using Unity.Netcode;
using UnityEngine;

namespace NPCSystem
{
    [RequireComponent(typeof(NetworkObject))]
    public class NPCPlayerNetworkAvatar : NetworkBehaviour
    {
        [SerializeField] string playerDisplayName = string.Empty;

        public ulong PlayerId => OwnerClientId;

        public string PlayerDisplayName => string.IsNullOrWhiteSpace(playerDisplayName)
            ? $"Player {OwnerClientId}"
            : playerDisplayName.Trim();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer && string.IsNullOrWhiteSpace(playerDisplayName))
            {
                playerDisplayName = $"Player {OwnerClientId}";
            }
        }
    }
}
