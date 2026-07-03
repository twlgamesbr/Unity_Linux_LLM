using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCNetworkTransferableItemPrefabTests
    {
        const string ItemPrefabPath = "Assets/Resources/Networking/NPCTransferableItem.prefab";

        [Test]
        public void TransferableItemPrefabHasRequiredOwnershipTransferComponents()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NPCTransferableItem>(), Is.Not.Null);
        }
    }
}
