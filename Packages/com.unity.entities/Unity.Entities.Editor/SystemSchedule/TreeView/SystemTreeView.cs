using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemTreeView : VisualElement, System.IDisposable
    {
        static readonly string k_NoSystemsFoundTitle = L10n.Tr("No system matches your search");

        // internal for test.
        internal MultiColumnTreeView MultiColumnTreeViewElement { get; }
        internal IList<TreeViewItemData<SystemTreeViewItemData>> TreeViewRootItems { get; } = new List<TreeViewItemData<SystemTreeViewItemData>>();
        // Column Labels
        static readonly ObjectPool<VisualElement> k_CellLabelPool = new (() => new VisualElement());

        internal readonly List<TreeViewItemData<SystemTreeViewItemData>> m_ListViewFilteredItems = new ();

        internal System.Action<SystemProxy> systemSelectionChanged;

        int m_LastSelectedItemId;
        WorldProxy m_WorldProxy;
        readonly CenteredMessageElement m_SearchEmptyMessage;
        int m_ScrollToItemId = -1;

        bool m_IsSearching = false;
        IList<SearchItem> m_SearchResults;

        readonly List<SystemDescriptor> m_AllSystemsForSearch = new();
        readonly Dictionary<string, string[]> m_SystemDependencyMap = new();
        readonly List<SystemDescriptor> m_SearchResultsFlatSystemList = new();

        internal SystemGraph LocalSystemGraph;
        public static SystemProxy SelectedSystem;

        public bool ShowWorldColumn { get; set; }
        public bool ShowNamespaceColumn { get; set; }
        public bool ShowEntityCountColumn { get; set; }
        public bool ShowMorePrecisionForRunningTime { get; set; }
        public bool Show0sInEntityCountAndTimeColumn { get; set; }
        public bool ShowTimeColumn { get; set; }

        Column m_SystemColumn;
        Column m_WorldColumn;
        Column m_NamespaceColumn;
        Column m_EntityCountColumn;
        Column m_RunningTimeColumn;

        /// <summary>
        /// Constructor of the tree view.
        /// </summary>
        public SystemTreeView()
        {
            MultiColumnTreeViewElement = new MultiColumnTreeView()
            {
                name = "SystemTreeView",
                fixedItemHeight = Constants.ListView.ItemHeight,
                autoExpand = true,
                viewDataKey = "full-view",
                selectionType = SelectionType.Single,
                style =
                {
                    flexGrow = 1
                }
            };

            CreateColumns();

            MultiColumnTreeViewElement.SetRootItems(TreeViewRootItems);

            MultiColumnTreeViewElement.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (m_ScrollToItemId == -1)
                    return;

                var tempId = m_ScrollToItemId;
                m_ScrollToItemId = -1;
                if (MultiColumnTreeViewElement.GetItemDataForId<SystemTreeViewItemData>(tempId) != null)
                    MultiColumnTreeViewElement.ScrollToItemById(tempId);
            });

            MultiColumnTreeViewElement.selectionChanged += OnSelectionChanged;
            Add(MultiColumnTreeViewElement);

            m_SearchEmptyMessage = new CenteredMessageElement { Title = k_NoSystemsFoundTitle };
            m_SearchEmptyMessage.Hide();
            Add(m_SearchEmptyMessage);

            MultiColumnTreeViewElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == MultiColumnTreeViewElement.Q(className: ScrollView.contentAndVerticalScrollUssClassName))
                    Selection.activeObject = null;
            });
        }

        internal void RebuildColumns()
        {
            MultiColumnTreeViewElement.columns.Clear();
            MultiColumnTreeViewElement.columns.Add(m_SystemColumn);
            if (ShowWorldColumn)
                MultiColumnTreeViewElement.columns.Add(m_WorldColumn);
            if (ShowNamespaceColumn)
                    MultiColumnTreeViewElement.columns.Add(m_NamespaceColumn);
            if (ShowEntityCountColumn)
                    MultiColumnTreeViewElement.columns.Add(m_EntityCountColumn);
            if (ShowTimeColumn)
                    MultiColumnTreeViewElement.columns.Add(m_RunningTimeColumn);
            Resources.Templates.SystemScheduleItem.AddStyles(MultiColumnTreeViewElement);
        }

        void CreateColumns()
        {
            const string headerStr = "Header";

            m_SystemColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.System,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.System;
                    label.tooltip = SystemScheduleWindow.Contents.SystemTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.System);
                },
                makeCell = MakeTreeViewItem,
                bindCell = BindSystemItem,
                resizable = true,
                optional = false,
                destroyCell = ReleaseTreeViewItem,
                minWidth = 100,
                width = 300
            };

            m_WorldColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.World,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.World;
                    label.tooltip = SystemScheduleWindow.Contents.WorldTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.World);
                },
                makeCell = MakeCellLabel,
                bindCell = BindWorldCell,
                resizable = true,
                width = 100
            };

            m_NamespaceColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.Namespace,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.Namespace;
                    label.tooltip = SystemScheduleWindow.Contents.NamespaceTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.Namespace);
                },
                makeCell = MakeCellLabel,
                bindCell = BindNamespaceCell,
                resizable = true,
                width = 100
            };

            m_EntityCountColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.EntityCount,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>(headerStr);
                    label.text = SystemScheduleWindow.Contents.EntityCount;
                    label.tooltip = SystemScheduleWindow.Contents.EntityCountTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.EntityCount);
                },
                makeCell = MakeCellLabel,
                bindCell = BindEntityCountCell,
                resizable = true,
                width = 100
            };

           m_RunningTimeColumn = new Column()
            {
                name = SystemScheduleWindow.Contents.Time,
                makeHeader = MakeHeaderLabel,
                bindHeader = e =>
                {
                    var label = e.Q<Label>("Header");
                    label.text = SystemScheduleWindow.Contents.Time;
                    label.tooltip = SystemScheduleWindow.Contents.TimeTooltip;
                    label.AddToClassList(UssClasses.SystemScheduleWindow.TreeViewHeader.Time);
                },
                makeCell = MakeCellLabel,
                bindCell = BindRunningTimeCell,
                resizable = true,
                width = 100
            };
        }
        void OnSelectionChanged(IEnumerable<object> selection)
        {
            SystemTreeViewItemData selectedItem = null;
            foreach (var obj in selection)
            {
                selectedItem = obj as SystemTreeViewItemData;
                if (selectedItem != null)
                    break;
            }
            if (selectedItem != null)
                OnSelectionChanged(selectedItem);
        }

        void OnSelectionChanged(SystemTreeViewItemData selectedItem)
        {
            // By selecting a system within Systems window, we need to clear up SelectedSystem which is set only from the outside.
            SelectedSystem = default;

            if (selectedItem == null || !selectedItem.SystemProxy.Valid)
                return;

            m_LastSelectedItemId = selectedItem.id;
            m_ScrollToItemId = selectedItem.id;

            systemSelectionChanged?.Invoke(selectedItem.SystemProxy);
        }

        VisualElement MakeHeaderLabel()
        {
            var element = new VisualElement();
            Resources.Templates.SystemScheduleTreeViewHeader.AddStyles(element);
            var label = new Label
            {
                name = "Header",
            };
            element.Add(label);
            return element;
        }

        VisualElement MakeCellLabel()
        {
            var element = k_CellLabelPool.Get();
            Resources.Templates.SystemScheduleItem.AddStyles(element);
            var label = new Label
            {
                name = "Cell"
            };
            element.Add(label);
            return element;
        }

        VisualElement MakeTreeViewItem() => SystemInformationVisualElement.Acquire(this);

        static void ReleaseTreeViewItem(VisualElement ve)
        {
            if(ve  != null)
                ((SystemInformationVisualElement)ve).Release();
        }

        public void StopSearch()
        {
            m_IsSearching = false;
            Refresh();
        }

        public void SetResults(IList<SearchItem> results)
        {
            m_IsSearching = true;
            m_SearchResults = results;
            Refresh();
        }

        public void Refresh(WorldProxy worldProxy)
        {
            m_WorldProxy = worldProxy;

            m_AllSystemsForSearch.Clear();
            m_SystemDependencyMap.Clear();

            RecreateTreeViewRootItems();
            FillSystemDependencyCache(m_AllSystemsForSearch, m_SystemDependencyMap);
            Refresh();
        }

        void RecreateTreeViewRootItems()
        {
            ReleaseAllPooledItems();

            if (World.All.Count > 0)
            {
                var graph = LocalSystemGraph;

                foreach (var node in graph.Roots)
                {
                    if (!node.ShowForWorldProxy(m_WorldProxy))
                        continue;

                    var item = SystemTreeViewItemData.Acquire((PlayerLoopSystemGraph)graph, node, m_WorldProxy);
                    PopulateAllChildren(item);

                    var children = GetAllChildren(item);
                    TreeViewRootItems.Add(new TreeViewItemData<SystemTreeViewItemData>(item.id, item, children));
                }

                MultiColumnTreeViewElement.SetRootItems(TreeViewRootItems);
                MultiColumnTreeViewElement.Rebuild();
            }
        }

        void PopulateAllChildren(SystemTreeViewItemData item)
        {
            if (item.SystemProxy.Valid)
            {
                var systemForSearch = new SystemDescriptor(item.SystemProxy)
                {
                    Node = item.Node,
                };
                m_AllSystemsForSearch.Add(systemForSearch);
                SystemProxy.BuildSystemDependencyMap(item.SystemProxy, m_SystemDependencyMap);
            }

            if (!item.HasChildren)
                return;

            item.PopulateChildren();

            foreach (var child in item.children)
                PopulateAllChildren(child.data);
        }

        static List<TreeViewItemData<SystemTreeViewItemData>> GetAllChildren(SystemTreeViewItemData item)
        {
            var result = new List<TreeViewItemData<SystemTreeViewItemData>>();
            foreach (var child in item.children)
            {
                var children = GetAllChildren(child.data);
                result.Add(new TreeViewItemData<SystemTreeViewItemData>(child.id, child.data, children));
            }
            return result;
        }

        static void FillSystemDependencyCache(List<SystemDescriptor> descriptors, Dictionary<string, string[]> dependencyMap)
        {
            foreach (var desc in descriptors)
            {
                var dependenciesList = new List<string>();
                foreach (var (system, dependencies) in dependencyMap)
                {
                    if (dependencies != null && System.Array.IndexOf(dependencies, desc.Name) >= 0)
                        dependenciesList.Add(system);
                }
                var dependenciesArr = dependenciesList.ToArray();
                desc.UpdateDependencies(dependenciesArr);
            }
        }

        void BuildFilterResults()
        {
            m_SearchResultsFlatSystemList.Clear();

            if (!m_IsSearching)
                m_SearchResultsFlatSystemList.AddRange(m_AllSystemsForSearch);
            else
            {
                foreach (var result in m_SearchResults)
                {
                    // TODO: Make use of ComponentSystemBase directly
                    var system = (SystemDescriptor)result.data;
                    var index = m_AllSystemsForSearch.FindIndex(x => x.Proxy == system.Proxy);
                    if (index > -1)
                        m_SearchResultsFlatSystemList.Add(m_AllSystemsForSearch[index]);
                }
            }
        }

        void PopulateListViewWithSearchResults()
        {
            BuildFilterResults();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.data.Release();
            }
            m_ListViewFilteredItems.Clear();
            foreach (var system in m_SearchResultsFlatSystemList)
            {
                var listViewItems = SystemTreeViewItemData.Acquire(LocalSystemGraph, system.Node, m_WorldProxy);
                m_ListViewFilteredItems.Add(new TreeViewItemData<SystemTreeViewItemData>(listViewItems.id, listViewItems));
            }
        }

        /// <summary>
        /// Refresh tree view to update with latest information.
        /// </summary>
        void Refresh()
        {
            // Check if there is search result
            if (m_IsSearching)
            {
                PopulateListViewWithSearchResults();
                var hasSearchResult = m_ListViewFilteredItems.Count > 0;

                MultiColumnTreeViewElement.SetVisibility(hasSearchResult);
                m_SearchEmptyMessage.SetVisibility(!hasSearchResult);
                m_SearchEmptyMessage.Title = k_NoSystemsFoundTitle;
                m_SearchEmptyMessage.Message = string.Empty;
            }
            else
            {
                MultiColumnTreeViewElement.Show();
                m_SearchEmptyMessage.Hide();
            }
            SetSelection();
        }

        public void SetSelection()
        {
            // Update last selected item ID if we have a valid selected system
            if (SelectedSystem.Valid && (m_WorldProxy == null || SelectedSystem.WorldProxy.Equals(m_WorldProxy)) && m_AllSystemsForSearch.Count > 0)
            {
                SystemDescriptor selectedSystem = null;
                foreach (var s in m_AllSystemsForSearch)
                {
                    if (s.Proxy.Equals(SelectedSystem))
                    {
                        selectedSystem = s;
                        break;
                    }
                }
                if (selectedSystem != null)
                    m_LastSelectedItemId = selectedSystem.Proxy.SystemIndex;
            }

            // Set up tree view with appropriate root items and rebuild
            MultiColumnTreeViewElement.ClearSelection();
            MultiColumnTreeViewElement.SetRootItems(m_IsSearching ? m_ListViewFilteredItems : TreeViewRootItems);
            MultiColumnTreeViewElement.Rebuild();

            // Restore selection if we have a valid last selected item
            if (MultiColumnTreeViewElement.GetItemDataForId<SystemTreeViewItemData>(m_LastSelectedItemId) == null)
                return;

            MultiColumnTreeViewElement.SetSelectionByIdWithoutNotify(new []{ m_LastSelectedItemId });
            MultiColumnTreeViewElement.RefreshItems();
            MultiColumnTreeViewElement.ScrollToItemById(m_LastSelectedItemId);
        }

        void BindSystemItem(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            var systemInformationElement = element as SystemInformationVisualElement;
            if (null == systemInformationElement)
                return;

            systemInformationElement.IndexInTreeView = index;
            systemInformationElement.Target = progressItem;
        }

        void BindWorldCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                label.text = progressItem.GetWorldName();
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.WorldNameColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.WorldName);
                if (progressItem.SystemProxy != null && progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
            }
        }

        void BindNamespaceCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                label.text = progressItem.GetNamespace();
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.NamespaceColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.Namespace);
                if (progressItem.SystemProxy != null && progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
            }
        }

        void BindEntityCountCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                var entityCount = progressItem.GetEntityMatches();
                label.text = entityCount;
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.EntityCountColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.EntityCount);
                if (progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
                if (!Show0sInEntityCountAndTimeColumn && entityCount.Equals("0"))
                {
                    label.Hide();
                }
                else
                {
                    label.Show();
                }
            }
        }

        void BindRunningTimeCell(VisualElement element, int index)
        {
            var progressItem = MultiColumnTreeViewElement.GetItemDataForIndex<SystemTreeViewItemData>(index);
            if (progressItem != null)
            {
                Label label = element.Q<Label>("Cell");
                var runningTime = progressItem.GetRunningTime(ShowMorePrecisionForRunningTime);
                label.text = runningTime;
                element.AddToClassList(UssClasses.SystemScheduleWindow.Items.TimeColumn);
                label.AddToClassList(UssClasses.SystemScheduleWindow.Items.Time);
                if (progressItem.SystemProxy != null && progressItem.SystemProxy.Valid)
                {
                    var groupState = progressItem.SystemProxy.Enabled && progressItem.GetParentState();
                    label.SetEnabled(groupState);
                }
                if (!Show0sInEntityCountAndTimeColumn &&
                    (runningTime.Equals("0.00") || runningTime.Equals("0.0000")))
                {
                    label.Hide();
                }
                else
                {
                    label.Show();
                }
            }
        }

        public void Dispose() => ReleaseAllPooledItems();

        void ReleaseAllPooledItems()
        {
            foreach (var rootItem in TreeViewRootItems)
            {
                rootItem.data.Release();
            }
            TreeViewRootItems.Clear();

            foreach (var filteredItem in m_ListViewFilteredItems)
            {
                filteredItem.data.Release();
            }
            m_ListViewFilteredItems.Clear();
            k_CellLabelPool.Clear();
        }
    }
}
