using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace NPCSystem.Tests
{
    public class NPCNetworkNpcPrefabTests
    {
        const string NpcPrefabPath = "Assets/Prefabs/Networking/NPCServerCharacter.prefab";

        [Test]
        public void NpcPrefabHasRequiredServerCharacterComponents()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NPCServerCharacter>(), Is.Not.Null);
        }
    }
}
