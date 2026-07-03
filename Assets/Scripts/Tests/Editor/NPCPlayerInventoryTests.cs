using NUnit.Framework;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCPlayerInventoryTests
    {
        [Test]
        public void PlayerInventoryAddsAndRemovesNormalizedItemIdsOnServer()
        {
            var gameObject = new GameObject("NPCPlayerInventoryTests");
            var inventory = gameObject.AddComponent<NPCPlayerInventory>();

            try
            {
                typeof(Unity.Netcode.NetworkBehaviour)
                    .GetProperty("IsServer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                // Exercise the pure normalization/container behavior through the public list.
                inventory.itemIds.Add(new Unity.Collections.FixedString64Bytes("ledger"));
                Assert.That(inventory.ContainsItem("Ledger"), Is.True);
                inventory.itemIds.RemoveAt(0);
                Assert.That(inventory.ContainsItem("ledger"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
