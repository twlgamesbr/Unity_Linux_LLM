using System.Linq;
using NUnit.Framework;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;

namespace Unity.Entities.Editor.Tests
{
    partial class ComponentInspectorTests
    {
        PlayerLoopSystem m_PrevPlayerLoop;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        World m_World;
        ComponentInspectorTestSystem m_ComponentInspectorTestSystem;

        partial class ComponentInspectorTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var (data1, data2) in SystemAPI.Query<RefRW<SystemScheduleTestData1>, RefRO<SystemScheduleTestData2>>()) { }
                foreach (var data1 in SystemAPI.Query<RefRO<SystemScheduleTestData1>>().WithNone<SystemScheduleTestData2>()) { }
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();
            DefaultWorldInitialization.Initialize("ComponentInspectorTestWorld", false);
            m_World = World.DefaultGameObjectInjectionWorld;

            m_ComponentInspectorTestSystem = m_World.GetOrCreateSystemManaged<ComponentInspectorTestSystem>();
            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_ComponentInspectorTestSystem);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }

        [Test]
        public void ComponentInspector_RelationshipsTab_MatchingSystems()
        {
            var matchingSystems = new ComponentMatchingSystems(m_World, typeof(SystemScheduleTestData1));
            matchingSystems.Update();

            var results = matchingSystems.Systems.Where(s => s.SystemName == "Component Inspector Tests | Component Inspector Test System").ToList();
            Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(results[0].Kind, Is.EqualTo(SystemQueriesViewData.SystemKind.Regular));
            Assert.That(results[0].Queries.Count, Is.EqualTo(2));
        }

        [Test]
        public void ComponentInspector_RelationshipsTab_MatchingEntities()
        {
            var archetype = m_World.EntityManager.CreateArchetype(typeof(SystemScheduleTestData1), typeof(SystemScheduleTestData2));
            using var entities = m_World.EntityManager.CreateEntity(archetype, 6, m_World.UpdateAllocator.ToAllocator);
#if !DOTS_DISABLE_DEBUG_NAMES
            m_World.EntityManager.SetName(entities[0], "ComponentInspectorEntity0");
#endif

            var worldViewData = new ComponentRelationshipWorldViewData(m_World, typeof(SystemScheduleTestData1));
            worldViewData.QueryWithEntitiesViewData.Update();
            Assert.That(worldViewData.QueryWithEntitiesViewData.Entities.Count, Is.EqualTo(5));
            Assert.That(worldViewData.QueryWithEntitiesViewData.TotalEntityCount, Is.EqualTo(6));
#if !DOTS_DISABLE_DEBUG_NAMES
            Assert.That(worldViewData.QueryWithEntitiesViewData.Entities[0].EntityName, Is.EqualTo("ComponentInspectorEntity0"));
#endif
        }
    }
}
