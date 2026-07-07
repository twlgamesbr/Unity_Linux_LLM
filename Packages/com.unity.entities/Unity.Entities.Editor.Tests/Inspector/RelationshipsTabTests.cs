using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Hybrid.Tests;
using UnityEngine.LowLevel;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class RelationshipsTabTests
    {
        PlayerLoopSystem m_PrevPlayerLoop;
        TestWithCustomDefaultGameObjectInjectionWorld m_CustomInjectionWorld;
        World m_TestWorld;
        SystemBase m_SystemA;
        SystemBase m_SystemB;
        SystemBase m_SystemC;

        WorldProxyManager m_WorldProxyManager;
        WorldProxy m_WorldProxy;

        [SetUp]
        public void Setup()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_CustomInjectionWorld.Setup();
            DefaultWorldInitialization.Initialize("RelationshipTabTestWorld", false);
            m_TestWorld = World.DefaultGameObjectInjectionWorld;

            var group = m_TestWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            m_SystemA = m_TestWorld.GetOrCreateSystemManaged<SystemA>();
            m_SystemB = m_TestWorld.GetOrCreateSystemManaged<SystemB>();
            m_SystemC = m_TestWorld.GetOrCreateSystemManaged<SystemC>();
            group.AddSystemToUpdateList(m_SystemA);
            group.AddSystemToUpdateList(m_SystemB);
            group.AddSystemToUpdateList(m_SystemC);
            group.SortSystems();

            m_WorldProxyManager = new WorldProxyManager();
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_TestWorld);
        }

        [TearDown]
        public void TearDown()
        {
            m_CustomInjectionWorld.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }

        [Test]
        public void RelationshipsTab_MatchSystemsAreOrdered()
        {
            var e = m_TestWorld.EntityManager.CreateEntity(typeof(EntityGuid));

            Assert.That(GetSystemIndex<SystemA>(), Is.LessThan(GetSystemIndex<SystemB>()));

            var matchingQueries = new List<QueryViewData>();
            var matchingSystems = new List<SystemQueriesViewData>();
            RelationshipsTab.GetMatchSystems(e, m_TestWorld, matchingQueries, matchingSystems, m_WorldProxy);

            Assert.That(matchingSystems.Select(s => s.SystemProxy).SequenceEqual(new [] { new SystemProxy(m_SystemB, m_WorldProxy), new SystemProxy(m_SystemA, m_WorldProxy) }), Is.True);

            int GetSystemIndex<T>()
            {
                for (var i = 0; i < m_TestWorld.Systems.Count; i++)
                {
                    if (m_TestWorld.Systems[i].GetType() == typeof(T))
                        return i;
                }

                throw new InvalidOperationException($"Excepted system of type {typeof(T)} was not found");
            }
        }

        [Test]
        public unsafe void RelationshipsTab_MatchQuery()
        {
            using var w = new World("test world");
            var archetype = w.EntityManager.CreateArchetype(typeof(EntityGuid), typeof(EcsTestSharedComp));
            using var entities = w.EntityManager.CreateEntity(archetype, 2, w.UpdateAllocator.ToAllocator);
            for (var i = 0; i < entities.Length; i++)
            {
                w.EntityManager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp { value = i == 0 ? 123 : 345 });
            }

            using var query = w.EntityManager.CreateEntityQuery(typeof(EntityGuid), typeof(EcsTestSharedComp));
            query.SetSharedComponentFilterManaged(new EcsTestSharedComp { value = 123 });
            var queries = stackalloc EntityQuery[] { query };
            var queryList = new UnsafeList<EntityQuery>(queries, 1);

            Assert.That(query.Matches(entities[0]), Is.True);
            Assert.That(query.Matches(entities[1]), Is.False);

            var matchingQueries = new List<QueryViewData>();
            RelationshipsTab.GatherMatchingQueries(w.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore, entities[0], ref queryList, matchingQueries, w, new SystemProxy(m_SystemA, m_WorldProxy));
            Assert.That(matchingQueries, Is.Not.Empty);

            matchingQueries.Clear();
            RelationshipsTab.GatherMatchingQueries(w.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore, entities[1], ref queryList, matchingQueries, w, new SystemProxy(m_SystemB, m_WorldProxy));
            Assert.That(matchingQueries, Is.Empty);
        }

        [Test]
        public void RelationshipsTab_ShowLimitedNumberOfSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var ve = view.Build();
            var moreLabel = ve.Q<Label>(className: UssClasses.SystemListView.MoreLabel);

            Assert.That(ve.Query<SystemQueriesView>().ToList(), Is.Empty);
            Assert.That(moreLabel, UIToolkitTestHelper.Is.Display(DisplayStyle.None));

            var systems = Enumerable.Range(0, 100).Select(i => new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0])).ToList();
            view.systemQueriesListView.Update(systems);

            Assert.That(ve.Query<SystemQueriesView>().ToList().Select(e => e.Data), Is.EquivalentTo(systems.Take(Constants.Inspector.MaxVisibleSystemCount)));
            Assert.That(moreLabel, UIToolkitTestHelper.Is.Display(DisplayStyle.Flex));
        }

        [Test]
        public void RelationshipsTab_Filter()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var ve = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemC = new SystemQueriesViewData(new SystemProxy(m_SystemC, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB, systemC };
            view.systemQueriesListView.Update(systems);

            view.SearchTerms.Add("system B");
            view.SearchTerms.Add("system C");
            view.systemQueriesListView.Update(systems);

            Assert.That(ve.Query<SystemQueriesView>().ToList().Select(e => e.Data), Is.EquivalentTo(new[] { systems[1], systems[2] }));
        }

        [Test]
        public void RelationshipTab_DuplicatedSystemNameWithNamespace()
        {
            // Add duplicated name system into the world.
            var duplicatedSystemA = m_TestWorld.GetOrCreateSystemManaged<DuplicateSystemNameTest.RelationshipsTabTests.SystemA>();
            var group = m_TestWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(duplicatedSystemA);
            group.SortSystems();
            m_WorldProxyManager.RebuildWorldProxyForGivenWorld(m_TestWorld);
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_TestWorld);

            var e = m_TestWorld.EntityManager.CreateEntity(typeof(EntityGuid));
            var matchingQueries = new List<QueryViewData>();
            var matchingSystems = new List<SystemQueriesViewData>();

            RelationshipsTab.GetMatchSystems(e, m_TestWorld, matchingQueries, matchingSystems, m_WorldProxy);
            Assert.That(matchingSystems.Select(s => s.SystemName).SequenceEqual(
                    new [] {
                        "Relationships Tab Tests | System A (Unity.Entities.Editor.Tests.DuplicateSystemNameTest)",
                        "Relationships Tab Tests | System B",
                        "Relationships Tab Tests | System A (Unity.Entities.Editor.Tests)"}), Is.True);
        }

        [Test]
        public void RelationshipsTab_Search_NoFilter_ShowsAllSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemC = new SystemQueriesViewData(new SystemProxy(m_SystemC, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB, systemC };
            view.systemQueriesListView.Update(systems);

            view.ClearSearch();

            var visibleSystems = GetVisibleSystems(root);
            Assert.That(visibleSystems.Count, Is.EqualTo(3), "All systems should be visible with no search");
        }

        [Test]
        public void RelationshipsTab_Search_FilterBySystemName_ShowsMatchingSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemC = new SystemQueriesViewData(new SystemProxy(m_SystemC, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB, systemC };
            view.systemQueriesListView.Update(systems);

            view.ApplySearch("system b");

            var visibleSystems = GetVisibleSystems(root);
            Assert.That(visibleSystems.Count, Is.EqualTo(1), "Only System B should be visible");
            Assert.That(visibleSystems[0].Data.SystemName, Does.Contain("System B"));
        }

        [Test]
        public void RelationshipsTab_Search_FilterByPartialName_ShowsMatchingSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemC = new SystemQueriesViewData(new SystemProxy(m_SystemC, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB, systemC };
            view.systemQueriesListView.Update(systems);

            view.ApplySearch("system");

            var visibleSystems = GetVisibleSystems(root);
            Assert.That(visibleSystems.Count, Is.EqualTo(3), "All systems should match 'system'");
        }

        [Test]
        public void RelationshipsTab_Search_NoMatches_HidesAllSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB };
            view.systemQueriesListView.Update(systems);

            view.ApplySearch("NonExistentSystem");

            var visibleSystems = GetVisibleSystems(root);
            Assert.That(visibleSystems.Count, Is.EqualTo(0), "No systems should be visible");
        }

        [Test]
        public void RelationshipsTab_Search_ClearSearch_ShowsAllSystems()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemA = new SystemQueriesViewData(new SystemProxy(m_SystemA, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systemC = new SystemQueriesViewData(new SystemProxy(m_SystemC, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemA, systemB, systemC };
            view.systemQueriesListView.Update(systems);

            view.ApplySearch("system a");

            var visibleAfterSearch = GetVisibleSystems(root);
            Assert.That(visibleAfterSearch.Count, Is.EqualTo(1), "Only System A should be visible");

            view.ClearSearch();

            var visibleAfterClear = GetVisibleSystems(root);
            Assert.That(visibleAfterClear.Count, Is.EqualTo(3), "All systems should be visible after clearing search");
        }

        [Test]
        public void RelationshipsTab_Search_CaseInsensitive()
        {
            var view = new RelationshipsTab.RelationshipsTabView();
            var root = view.Build();

            var systemB = new SystemQueriesViewData(new SystemProxy(m_SystemB, m_WorldProxy), SystemQueriesViewData.SystemKind.Regular, new QueryViewData[0]);
            var systems = new List<SystemQueriesViewData> { systemB };
            view.systemQueriesListView.Update(systems);

            view.ApplySearch("SYSTEM");
            Assert.That(GetVisibleSystems(root).Count, Is.EqualTo(1), "Should match uppercase");

            view.ApplySearch("system");
            Assert.That(GetVisibleSystems(root).Count, Is.EqualTo(1), "Should match lowercase");

            view.ApplySearch("System");
            Assert.That(GetVisibleSystems(root).Count, Is.EqualTo(1), "Should match mixed case");
        }

        List<SystemQueriesView> GetVisibleSystems(VisualElement root)
        {
            var allSystems = new List<SystemQueriesView>();
            root.Query<SystemQueriesView>().ToList(allSystems);

            var visibleSystems = new List<SystemQueriesView>();
            for (var i = 0; i < allSystems.Count; i++)
            {
                var system = allSystems[i];
                // System is visible if: no explicit style (Null) or style is not None
                if (system.style.display.keyword == StyleKeyword.Null ||
                    system.style.display.value != DisplayStyle.None)
                {
                    visibleSystems.Add(system);
                }
            }

            return visibleSystems;
        }

        partial class SystemA : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var guid in SystemAPI.Query<RefRO<EntityGuid>>()) { }
            }
        }

        [UpdateBefore(typeof(SystemA))]
        partial class SystemB : SystemBase
        {
            protected override void OnUpdate()
            {
                foreach (var guid in SystemAPI.Query<RefRO<EntityGuid>>()) { }
            }
        }

        partial class SystemC : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }
    }

    namespace DuplicateSystemNameTest
    {
        public partial class RelationshipsTabTests
        {
            public partial class SystemA : SystemBase
            {
                protected override void OnUpdate()
                {
                    foreach (var guid in SystemAPI.Query<RefRO<EntityGuid>>()) { }
                }
            }
        }
    }
}
