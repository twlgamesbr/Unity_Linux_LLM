using System;
using System.Text;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Hierarchy;
using Unity.Profiling;
using Unity.Scenes;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class HierarchyEntityHandler : HierarchyNodeTypeHandler, IHierarchyEditorNodeTypeHandler
    {
        const string k_EntityUssClass = "hierarchy-item--entity-node";
        const string k_PrefabUssClass = "hierarchy-item--prefab";
        const string k_PrefabRootUssClass = "hierarchy-item--prefab-root";

        const string k_StyleSheetPath =
            "Packages/com.unity.entities/Editor Default Resources/uss/Hierarchy/hierarchy-entity-item.uss";

        StyleSheet m_StyleSheet;
        StyleSheet m_ThemeStyleSheet;

        NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode> m_EntityToNodeMap;
        NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity> m_NodeToEntityMap;
        NativeHashMap<Entity, HierarchyPrefabType> m_EntityToPrefabTypeMap;

        HierarchyWorldHandler m_WorldHandler;
        HierarchySubSceneRuntimeHandler m_SubSceneHandler;

        static readonly ProfilerMarker k_AllocatingHierarchyNodesMarker = new ("HierarchyEntityHandler.AllocatingNewHierarchyNodes");
        static readonly ProfilerMarker k_CommandAddNodesMarker = new ("HierarchyEntityHandler.CommandAddNodes");
        static readonly ProfilerMarker k_CommandSetParentMarker = new ("HierarchyEntityHandler.CommandSetParent");
        static readonly ProfilerMarker k_CommandSetNamesMarker = new ("HierarchyEntityHandler.CommandSetNames");

        public override string GetNodeTypeName() => nameof(Entity);

        protected override void Initialize()
        {
            base.Initialize();

            UpdateHierarchySystem.OnAddEntityNodes += AddEntityNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes += RemoveEntityNodes;
            UpdateHierarchySystem.OnSetParentNode += SetParentNode;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity += ResizeMappings;

            m_EntityToNodeMap = new NativeHashMap<Entity, Unity.Hierarchy.HierarchyNode>(1, Allocator.Persistent);
            m_NodeToEntityMap = new NativeHashMap<Unity.Hierarchy.HierarchyNode, Entity>(1, Allocator.Persistent);
            m_EntityToPrefabTypeMap = new NativeHashMap<Entity, HierarchyPrefabType>(1, Allocator.Persistent);

            m_WorldHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchyWorldHandler>();
            m_SubSceneHandler = Hierarchy.GetOrCreateNodeTypeHandler<HierarchySubSceneRuntimeHandler>();

            // Register all already initialized worlds
            m_WorldHandler.RegisterAllHierarchySystems();
        }

        protected override void Dispose(bool disposing)
        {
            m_EntityToNodeMap.Dispose();
            m_NodeToEntityMap.Dispose();
            m_EntityToPrefabTypeMap.Dispose();

            UpdateHierarchySystem.OnAddEntityNodes -= AddEntityNodes;
            UpdateHierarchySystem.OnRemoveEntityNodes -= RemoveEntityNodes;
            UpdateHierarchySystem.OnSetParentNode -= SetParentNode;
            UpdateHierarchySystem.OnResizeEntityHandlerMappingsCapacity -= ResizeMappings;

            m_WorldHandler = null;
            m_SubSceneHandler = null;
            base.Dispose(disposing);
        }

        StyleSheet StyleSheet
        {
            get
            {
                if (!m_StyleSheet)
                    m_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath);
                return m_StyleSheet;
            }
        }

        StyleSheet ThemeStyleSheet
        {
            get
            {
                if (!m_ThemeStyleSheet)
                {
                    var path = k_StyleSheetPath;
                    var index = path.LastIndexOf(".uss", StringComparison.OrdinalIgnoreCase);
                    if (EditorGUIUtility.isProSkin)
                        path = path.Insert(index, "_dark");
                    else
                        path = path.Insert(index, "_light");
                    m_ThemeStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }

                return m_ThemeStyleSheet;
            }
        }

        [InitializeOnLoadMethod, UsedImplicitly]
        internal static void RegisterHierarchyHandlers()
        {
            EditorApplication.delayCall +=
                Unity.Hierarchy.Editor.HierarchyWindow.RegisterNodeTypeHandler<HierarchyEntityHandler>;
        }

        [UsedImplicitly]
        internal static void UnregisterHierarchyHandlers()
        {
            Unity.Hierarchy.Editor.HierarchyWindow.UnregisterNodeTypeHandler<HierarchyEntityHandler>();
        }

        // For test purposes for now
        internal Unity.Hierarchy.HierarchyNode GetNode(Entity entity)
        {
            if (m_EntityToNodeMap.TryGetValue(entity, out var node))
                return node;
            return Unity.Hierarchy.HierarchyNode.Null;
        }

        void AddEntityNodes(World world, Entity parent, NativeArray<HierarchyEntityNodeData> nodes)
        {
            if (nodes.Length == 0)
                return;
            
            Unity.Hierarchy.HierarchyNode parentNode;
            if (parent == Entity.Null)
            {
                // If the parent entity doesn't exist create it under the world not
                parentNode = m_WorldHandler.GetOrCreateWorldNode(world);
            }
            else
            {
                if (world.EntityManager.HasComponent<SubScene>(parent) && m_SubSceneHandler != null)
                    parentNode = m_SubSceneHandler.GetOrCreateSubSceneNode(world, parent);
                else
                    m_EntityToNodeMap.TryGetValue(parent, out parentNode);
                
                if (parentNode == Unity.Hierarchy.HierarchyNode.Null)
                {
                    // If the parent node doesn't exist, create it under to world node
                    var worldNode = m_WorldHandler.GetOrCreateWorldNode(world);
                    CommandList.Add(worldNode, out parentNode);
                    CommandList.SetName(parentNode, parent.ToFixedString().Value);
                    m_EntityToNodeMap.Add(parent, parentNode);
                    m_NodeToEntityMap.Add(parentNode, parent);
                }
            }

            AddNodes(parentNode, nodes);
        }

        void AddNodes(Unity.Hierarchy.HierarchyNode parentNode, NativeArray<HierarchyEntityNodeData> nodes)
        {
            var addedNodes = new NativeList<HierarchyEntityNodeData>(nodes.Length, Allocator.Temp);
            var addedNodesCount = 0;
            var newHierarchyNodes = new NativeList<Unity.Hierarchy.HierarchyNode>(nodes.Length, Allocator.Temp);
            
            using (k_AllocatingHierarchyNodesMarker.Auto())
            {
                for (var i = 0; i < nodes.Length; i++)
                {
                    if (!m_EntityToNodeMap.ContainsKey(nodes[i].Entity))
                    {
                        // Allocate new HierarchyNodes to add
                        var hierarchyNode = new Unity.Hierarchy.HierarchyNode();
                        newHierarchyNodes.Add(hierarchyNode);
                        addedNodes.Add(nodes[i]);
                        addedNodesCount++;
                    }
                }
            }

            using (k_CommandAddNodesMarker.Auto())
                CommandList.Add(parentNode, newHierarchyNodes.AsSpan());

            for (var i = 0; i < addedNodesCount; i++)
            {
                // Update mappings after the HierarchyNodes have been created. They are created in the command
                m_EntityToNodeMap.Add(addedNodes[i].Entity, newHierarchyNodes[i]);
                m_NodeToEntityMap.Add(newHierarchyNodes[i], addedNodes[i].Entity);
                m_EntityToPrefabTypeMap.Add(addedNodes[i].Entity, addedNodes[i].PrefabType);
            }

            // Set names to all children nodes
            // TODO: A batch command will make it faster to update each node name,
            // It should also take a fixed string to we reduce allocation when passing the name to the command
            using (k_CommandSetNamesMarker.Auto())
            {
                for (var i = 0; i < addedNodesCount; i++)
                {
                    var nodeData = addedNodes[i];
                    CommandList.SetName(newHierarchyNodes[i], nodeData.EntityName.ToString());
                }
            }

            addedNodes.Dispose();
            newHierarchyNodes.Dispose();
        }

        void ResizeMappings(int count)
        {
            if(m_EntityToNodeMap.Capacity < m_EntityToNodeMap.Count + count)
                m_EntityToNodeMap.Capacity = m_EntityToNodeMap.Count + count;
            if(m_NodeToEntityMap.Capacity < m_NodeToEntityMap.Count + count)
                m_NodeToEntityMap.Capacity = m_NodeToEntityMap.Count + count;
            if(m_EntityToPrefabTypeMap.Capacity < m_EntityToPrefabTypeMap.Count + count)
                m_EntityToPrefabTypeMap.Capacity = m_EntityToPrefabTypeMap.Count + count;
        }

        void RemoveEntityNodes(NativeList<Entity> entitiesToRemove)
        {
            foreach (var entity in entitiesToRemove)
            {
                if (m_EntityToNodeMap.TryGetValue(entity, out var node))
                {
                    CommandList.Remove(node);
                    m_EntityToNodeMap.Remove(entity);
                    m_EntityToPrefabTypeMap.Remove(entity);
                    m_NodeToEntityMap.Remove(node);
                }
            }
        }

        void SetParentNode(World world, NativeList<Entity> entityChildren, NativeList<Entity> entityParents)
        {
            if (entityChildren.Length != entityParents.Length)
            {
                Debug.LogError($"The number of entities being reparented {entityChildren.Length} should be the same as the number of new entity parent {entityParents.Length}");
                return;
            }

            using (k_CommandSetParentMarker.Auto())
            {
                for (var i = 0; i < entityChildren.Length; i++)
                {
                    if (!m_EntityToNodeMap.TryGetValue(entityChildren[i], out var nodeChild))
                    {
                        Debug.LogError($"Failed to find Entity child node: {entityChildren[i]} in {nameof(HierarchyEntityHandler)} mapping");
                        continue;
                    }

                    var parent = entityParents[i];
                    Unity.Hierarchy.HierarchyNode parentNode;

                    // A default parent entity means that the child is now parented underneath its world.
                    if (parent == default)
                        parentNode = m_WorldHandler.GetOrCreateWorldNode(world);
                    else
                    {
                        if (!m_EntityToNodeMap.TryGetValue(parent, out parentNode))
                        {
                            if (!m_SubSceneHandler.TryGetSubSceneNode(parent, out parentNode))
                            {
                                Debug.LogError($"Failed to find Entity parent node: {parent} in {nameof(HierarchyEntityHandler)} mapping");
                                continue;
                            }
                        }
                    }

                    CommandList.SetParent(nodeChild, parentNode);
                }
            }
        }

        internal void ClearMappings(World world)
        {
            var entityManager = world.EntityManager;
            var allEntities = m_EntityToNodeMap.GetKeyArray(Allocator.Temp);
            foreach (var entity in allEntities)
            {
                if (entityManager.Exists(entity))
                {
                    var node = m_EntityToNodeMap[entity];
                    m_EntityToNodeMap.Remove(entity);
                    m_NodeToEntityMap.Remove(node);
                    m_EntityToPrefabTypeMap.Remove(entity);
                }
            }
        }

        protected override void OnBindView(HierarchyView view)
        {
            view.StyleContainer.styleSheets.Add(StyleSheet);
            view.StyleContainer.styleSheets.Add(ThemeStyleSheet);
        }

        protected override void OnUnbindView(HierarchyView view)
        {
            // The StyleSheet and ThemeStyleSheet can be null when the Entities package is being removed from a project
            if (StyleSheet != null)
                view.StyleContainer.styleSheets.Remove(StyleSheet);
            if (ThemeStyleSheet != null)
                view.StyleContainer.styleSheets.Remove(ThemeStyleSheet);
            base.OnUnbindView(view);
        }

        protected override void OnBindItem(HierarchyViewItem item)
        {
            if (!m_NodeToEntityMap.TryGetValue(item.Node, out var entity))
            {
                item.AddToClassList(k_PrefabUssClass);
                item.AddToClassList(k_PrefabRootUssClass);
                item.AddToClassList(k_EntityUssClass);
            }

            item.EnableInClassList(k_EntityUssClass, true);
            if (m_EntityToPrefabTypeMap.TryGetValue(entity, out var prefabType))
            {
                switch (prefabType)
                {
                    case HierarchyPrefabType.None:
                        item.EnableInClassList(k_PrefabUssClass, false);
                        item.EnableInClassList(k_PrefabRootUssClass, false);
                        break;
                    case HierarchyPrefabType.PrefabRoot:
                        item.EnableInClassList(k_PrefabUssClass, true);
                        item.EnableInClassList(k_PrefabRootUssClass, true);
                        break;
                    case HierarchyPrefabType.PrefabPart:
                        item.EnableInClassList(k_PrefabUssClass, true);
                        item.EnableInClassList(k_PrefabRootUssClass, false);
                        break;
                }
            }

            // TODO: Add support for more selection types like scrolling (ie: HierarchyGlobalSelectionHandler.SyncGlobalSelectionFromViewModel)
            item.RegisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode, item.Node);
        }

        protected override void OnUnbindItem(HierarchyViewItem item)
        {
            item.EnableInClassList(k_EntityUssClass, false);
            item.EnableInClassList(k_PrefabUssClass, false);
            item.EnableInClassList(k_PrefabRootUssClass, false);
            item.UnregisterCallback<ClickEvent, Unity.Hierarchy.HierarchyNode>(SelectEntityNode);
        }

        void SelectEntityNode(ClickEvent evt, Unity.Hierarchy.HierarchyNode node)
        {
            if (evt.clickCount >= 1)
            {
                if (m_NodeToEntityMap.TryGetValue(node, out var entity))
                {
                    foreach (var world in World.All)
                    {
                        var em = world.EntityManager;
                        if (em.Exists(entity))
                        {
                            EntitySelectionProxy.SelectEntity(world, entity);
                            break;
                        }
                    }
                }
            }
        }

        #region IHierarchyEditorNodeTypeHandler

        bool IHierarchyEditorNodeTypeHandler.CanSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.OnSetName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node, string name) => false;
        string IHierarchyEditorNodeTypeHandler.GetDisplayName(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => Hierarchy.GetName(in node);
        bool IHierarchyEditorNodeTypeHandler.CanDoubleClick(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDoubleClick(HierarchyView view, in Unity.Hierarchy.HierarchyNode node) => false;
        bool IHierarchyEditorNodeTypeHandler.CanCut(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnCut(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanCopy(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnCopy(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanPaste(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnPaste(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanPasteAsChild(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnPasteAsChild(HierarchyView view, bool keepWorldPos) => false;
        bool IHierarchyEditorNodeTypeHandler.CanDuplicate(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDuplicate(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.CanDelete(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnDelete(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.AcceptParent(HierarchyView view, in Unity.Hierarchy.HierarchyNode parent) => false;
        bool IHierarchyEditorNodeTypeHandler.AcceptChild(HierarchyView view, in Unity.Hierarchy.HierarchyNode child) => false;

        bool IHierarchyEditorNodeTypeHandler.CanStartDrag(HierarchyView view, ReadOnlySpan<Unity.Hierarchy.HierarchyNode> nodes)
        {
            var canStartDrag = false;
            foreach (var node in nodes)
            {
                if (!m_NodeToEntityMap.TryGetValue(node, out _))
                    canStartDrag = true;
            }

            return canStartDrag;
        }

        void IHierarchyEditorNodeTypeHandler.OnStartDrag(in HierarchyViewDragAndDropSetupData data) { }
        DragVisualMode IHierarchyEditorNodeTypeHandler.CanDrop(in HierarchyViewDragAndDropHandlingData data) => DragVisualMode.None;
        bool IHierarchyEditorNodeTypeHandler.CanFindReferences(HierarchyView view) => false;
        bool IHierarchyEditorNodeTypeHandler.OnFindReferences(HierarchyView view) => false;
        DragVisualMode IHierarchyEditorNodeTypeHandler.OnDrop(in HierarchyViewDragAndDropHandlingData data) => DragVisualMode.None;
        void IHierarchyEditorNodeTypeHandler.GetTooltip(HierarchyViewItem item, bool isFiltering, StringBuilder tooltip) { }
        void IHierarchyEditorNodeTypeHandler.PopulateContextMenu(HierarchyView view, HierarchyViewItem item, DropdownMenu menu) { }

        #endregion
    }
}
