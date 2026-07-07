using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    public class HierarchyEntityHandlerTests
    {
        HierarchyEntityHandler m_Handler;
        HierarchyWorldHandler m_WorldHandler;
        Unity.Hierarchy.Hierarchy m_Hierarchy;
        World m_PreviousWorld;
        World m_World;
        BakingSystem m_BakingSystem;
        BakingSettings m_Settings;
        GameObject m_Prefab;

        [SetUp]
        public void SetUp()
        {
            // Load the prefab
            var path = $"Packages/com.unity.entities/Unity.Entities.Editor.Tests/Content/Prefab_Hierarchy.prefab";
            m_Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            m_World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            m_Hierarchy = new Unity.Hierarchy.Hierarchy();
            m_Handler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyEntityHandler>();
            m_WorldHandler = m_Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();

            m_BakingSystem = m_World.GetOrCreateSystemManaged<BakingSystem>();
            m_Settings = new BakingSettings
            {
                BakingFlags = BakingUtility.BakingFlags.AssignName | BakingUtility.BakingFlags.AddEntityGUID
            };

            m_BakingSystem.BakingSettings = m_Settings;

            UpdateHierarchy(m_Hierarchy);
        }

        [TearDown]
        public void TearDown()
        {
            m_Handler = null;
            m_WorldHandler = null;
            m_Hierarchy.Dispose();
            m_Hierarchy = null;
            m_World.Dispose();
            m_World = null;
            m_BakingSystem = null;
            m_Settings = null;
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
        }

        public static void UpdateHierarchy(Unity.Hierarchy.Hierarchy hierarchy)
        {
            int count = 100;
            while (hierarchy.UpdateNeeded && count-- > 0)
                hierarchy.Update();
            Assert.IsFalse(hierarchy.UpdateNeeded);
        }

        [Test]
        public void CreateEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);
            Assert.That(node1, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            Assert.That(node2, Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));

            Assert.IsTrue(m_Hierarchy.Exists(node1));
            Assert.IsTrue(m_Hierarchy.Exists(node2));
        }

        [Test]
        public void RemoveEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var entity1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var node1 = m_Handler.GetNode(entity1);
            var node2 = m_Handler.GetNode(entity2);

            Assert.IsTrue(m_Hierarchy.Exists(node1));
            Assert.IsTrue(m_Hierarchy.Exists(node2));

            m_World.EntityManager.DestroyEntity(entity1);
            m_World.EntityManager.DestroyEntity(entity2);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            Assert.IsFalse(m_Hierarchy.Exists(node1));
            Assert.IsFalse(m_Hierarchy.Exists(node2));
        }

        [Test]
        public void CreateEntityChildrenNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));

            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 1);

            var childrenNode = m_Hierarchy.GetChildren(parentNode);
            Assert.IsTrue(childrenNode[0] == childNode);
        }

        [Test]
        public void ReparentEntityNodes()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var parent2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children1 = m_World.EntityManager.AddBuffer<Child>(parent1);
            children1.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent1});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode1 = m_Handler.GetNode(parent1);
            var parentNode2 = m_Handler.GetNode(parent2);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode1));
            Assert.IsTrue(m_Hierarchy.Exists(parentNode2));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode1) == 1);

            // Reparent
            m_World.EntityManager.RemoveComponent<Child>(parent1);
            var children2 = m_World.EntityManager.AddBuffer<Child>(parent2);
            children2.Add(new Child(){Value = child});
            m_World.EntityManager.AddComponentData(child, new Parent {Value = parent2});

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

             parentNode2 = m_Handler.GetNode(parent2);
             childNode = m_Handler.GetNode(child);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(childNode) == 0);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode2) == 1);
        }

        [Test]
        public void RemoveParentComponent_ReparentsEntityToWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child });
            m_World.EntityManager.AddComponentData(child, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 1);
            Assert.AreEqual(parentNode, m_Hierarchy.GetParent(childNode));

            // Remove parent relationship entirely
            m_World.EntityManager.RemoveComponent<Parent>(child);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            childNode = m_Handler.GetNode(child);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.Exists(worldNode));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode));
        }

        [Test]
        public void ReparentEntityToNullParent_IsParentedUnderWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child });
            m_World.EntityManager.AddComponentData(child, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            var childNode = m_Handler.GetNode(child);

            Assert.IsTrue(m_Hierarchy.Exists(parentNode));
            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.AreEqual(parentNode, m_Hierarchy.GetParent(childNode));

            // Reparent to Entity.Null (parent still has Child buffer, but Parent.Value is null)
            m_World.EntityManager.SetComponentData(child, new Parent { Value = Entity.Null });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            childNode = m_Handler.GetNode(child);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.Exists(childNode));
            Assert.IsTrue(m_Hierarchy.Exists(worldNode));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode));
        }

        [Test]
        public void RemoveParentFromMultipleEntities_AllReparentedToWorldNode()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child3 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child1 });
            children.Add(new Child { Value = child2 });
            children.Add(new Child { Value = child3 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child3, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 3);

            // Remove parent from all children simultaneously
            m_World.EntityManager.RemoveComponent<Parent>(child1);
            m_World.EntityManager.RemoveComponent<Parent>(child2);
            m_World.EntityManager.RemoveComponent<Parent>(child3);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            parentNode = m_Handler.GetNode(parent);
            var childNode1 = m_Handler.GetNode(child1);
            var childNode2 = m_Handler.GetNode(child2);
            var childNode3 = m_Handler.GetNode(child3);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.IsTrue(m_Hierarchy.GetChildrenCount(parentNode) == 0);
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode1));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode2));
            Assert.AreEqual(worldNode, m_Hierarchy.GetParent(childNode3));
        }

        [Test]
        public void DestroyEntityWithParentChange_DoesNotCrash()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child3 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children = m_World.EntityManager.AddBuffer<Child>(parent);
            children.Add(new Child { Value = child1 });
            children.Add(new Child { Value = child2 });
            children.Add(new Child { Value = child3 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent });
            m_World.EntityManager.AddComponentData(child3, new Parent { Value = parent });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode = m_Handler.GetNode(parent);
            Assume.That(m_Hierarchy.GetChildrenCount(parentNode), Is.EqualTo(3));

            // Remove parent component from all children AND destroy child2 in the same frame
            // This tests CleanupRemovedEntities: child2 will be in both RemovedParentEntities and DestroyedEntities
            m_World.EntityManager.RemoveComponent<Parent>(child1);
            m_World.EntityManager.RemoveComponent<Parent>(child2);
            m_World.EntityManager.RemoveComponent<Parent>(child3);
            m_World.EntityManager.DestroyEntity(child2);
            m_World.EntityManager.RemoveComponent<Child>(parent);

            // This should not crash - CleanupRemovedEntities should handle child2 being destroyed
            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing);

            // Verify child1 and child3 were reparented to world node
            var childNode1 = m_Handler.GetNode(child1);
            var childNode3 = m_Handler.GetNode(child3);
            var worldNode = m_WorldHandler.GetWorldNode(m_World);

            Assert.That(m_Hierarchy.Exists(childNode1), Is.True);
            Assert.That(m_Hierarchy.Exists(childNode3), Is.True);
            Assert.That(m_Hierarchy.GetParent(childNode1), Is.EqualTo(worldNode));
            Assert.That(m_Hierarchy.GetParent(childNode3), Is.EqualTo(worldNode));

            // Verify child2 node no longer exists
            var childNode2 = m_Handler.GetNode(child2);
            Assert.That(m_Hierarchy.Exists(childNode2), Is.False);
        }

        [Test]
        public void DestroyEntityDuringReparenting_DoesNotCrash()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var parent1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var parent2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child1 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            var child2 = m_World.EntityManager.CreateEntity(typeof(EcsTestData));

            var children1 = m_World.EntityManager.AddBuffer<Child>(parent1);
            children1.Add(new Child { Value = child1 });
            children1.Add(new Child { Value = child2 });
            m_World.EntityManager.AddComponentData(child1, new Parent { Value = parent1 });
            m_World.EntityManager.AddComponentData(child2, new Parent { Value = parent1 });

            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            var parentNode1 = m_Handler.GetNode(parent1);
            Assume.That(m_Hierarchy.GetChildrenCount(parentNode1), Is.EqualTo(2));

            // Reparent both children to parent2, but destroy child1 in the same frame
            // This tests CleanupRemovedEntities: child1 will be in both AddedParentEntities and DestroyedEntities
            m_World.EntityManager.RemoveComponent<Child>(parent1);
            var children2 = m_World.EntityManager.AddBuffer<Child>(parent2);
            children2.Add(new Child { Value = child1 });
            children2.Add(new Child { Value = child2 });
            m_World.EntityManager.SetComponentData(child1, new Parent { Value = parent2 });
            m_World.EntityManager.SetComponentData(child2, new Parent { Value = parent2 });
            m_World.EntityManager.DestroyEntity(child1);

            // This should not crash - CleanupRemovedEntities should handle child1 being destroyed
            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing);

            // Verify child2 was reparented to parent2
            var parentNode2 = m_Handler.GetNode(parent2);
            var childNode2 = m_Handler.GetNode(child2);

            Assert.That(m_Hierarchy.Exists(childNode2), Is.True);
            Assert.That(m_Hierarchy.GetParent(childNode2), Is.EqualTo(parentNode2));
            Assert.That(m_Hierarchy.GetChildrenCount(parentNode2), Is.EqualTo(1));

            // Verify child1 node no longer exists
            var childNode1 = m_Handler.GetNode(child1);
            Assert.That(m_Hierarchy.Exists(childNode1), Is.False);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ShowHiddenEntitiesSetting_WorksAsExpected(bool showHiddenEntities)
        {
            var oldSetting = HierarchyEntitiesSettings.GetShowHiddenEntities();
            HierarchyEntitiesSettings.SetShowHiddenEntities(showHiddenEntities);

            try
            {
                var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
                var visibleEntity = m_World.EntityManager.CreateEntity();
                var hiddenEntity = m_World.EntityManager.CreateEntity(typeof(HideInHierarchy));

                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
                
                Assert.That(m_Handler.GetNode(visibleEntity), Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
                Assert.That(m_Handler.GetNode(hiddenEntity),
                    showHiddenEntities
                        ? Is.Not.EqualTo(Unity.Hierarchy.HierarchyNode.Null)
                        : Is.EqualTo(Unity.Hierarchy.HierarchyNode.Null));
            }
            finally
            {
                HierarchyEntitiesSettings.SetShowHiddenEntities(oldSetting);   
            }
        }       

        [Test]
        public void CreateEntityPrefabNodes()
        {
            var go = Object.Instantiate(m_Prefab);
            m_Settings.PrefabRoot = go;
            BakingUtility.BakeGameObjects(m_World, Array.Empty<GameObject>(), m_Settings);

            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();
            hierarchySystem.Update(m_World.Unmanaged);
            UpdateHierarchy(m_Hierarchy);

            EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<LinkedEntityGroup>().WithOptions(EntityQueryOptions.IncludePrefab).Build(m_World.EntityManager);
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            var entities = query.ToEntityArray(Allocator.Temp);
            var prefabNode = m_Handler.GetNode(entities[0]);

            // The prefab should have exactly one child
            Assert.IsTrue(m_Hierarchy.Exists(prefabNode));
            Assert.That(m_Hierarchy.GetChildrenCount(prefabNode), Is.EqualTo(1));

            entities.Dispose();
            query.Dispose();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CreatePrefabNodes_WithAdditionalEntitiesInLinkedEntityGroup()
        {
            var hierarchySystem = m_World.GetOrCreateSystem<UpdateHierarchySystem>();

            var prefabEntity = m_World.EntityManager.CreateEntity(typeof(Prefab));
            var linkedEntityGroup = m_World.EntityManager.AddBuffer<LinkedEntityGroup>(prefabEntity);
            linkedEntityGroup.Add(new LinkedEntityGroup { Value = prefabEntity });

            // Simulates CreateAdditionalEntity during baking - entities in LinkedEntityGroup without Parent component
            const int additionalEntityCount = 5;
            var additionalEntities = new Entity[additionalEntityCount];
            for (var i = 0; i < additionalEntityCount; i++)
            {
                additionalEntities[i] = m_World.EntityManager.CreateEntity();
                linkedEntityGroup.Add(new LinkedEntityGroup { Value = additionalEntities[i] });
            }

            Assert.That(() =>
            {
                hierarchySystem.Update(m_World.Unmanaged);
                UpdateHierarchy(m_Hierarchy);
            }, Throws.Nothing, "HashMap capacity should handle LinkedEntityGroup entities without Parent");

            var prefabNode = m_Handler.GetNode(prefabEntity);
            Assert.That(m_Hierarchy.Exists(prefabNode), Is.True);
            Assert.That(m_Hierarchy.GetChildrenCount(prefabNode), Is.EqualTo(additionalEntityCount));

            foreach (var entity in additionalEntities)
            {
                var childNode = m_Handler.GetNode(entity);
                Assert.That(m_Hierarchy.Exists(childNode), Is.True);
                Assert.That(m_Hierarchy.GetParent(childNode), Is.EqualTo(prefabNode));
            }
        }
    }
}
