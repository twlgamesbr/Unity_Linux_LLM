using System.Collections.Generic;
using Unity.Entities.UI;
using Unity.Profiling;
using Unity.Properties;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    internal class SystemScheduleWindow : DOTSEditorWindow, IHasCustomMenu
    {
        internal static class Contents
        {
            public static readonly string WindowName = L10n.Tr("Systems");
            public static readonly string ShowFullPlayerLoopString = L10n.Tr("Show Full Player Loop");
            public static readonly string System = L10n.Tr("System");
            public static readonly string SystemTooltip = L10n.Tr("System name.");
            public static readonly string World = L10n.Tr("World");
            public static readonly string WorldTooltip = L10n.Tr("The world in which this system operates.");
            public static readonly string Namespace = L10n.Tr("Namespace");
            public static readonly string NamespaceTooltip = L10n.Tr("Namespace to which this system belongs.");
            public static readonly string EntityCount = L10n.Tr("Entity Count");
            public static readonly string EntityCountTooltip = L10n.Tr(
                "The number of entities that match the queries at the end of the frame."
            );
            public static readonly string Time = L10n.Tr("Time (ms)");
            public static readonly string TimeTooltip = L10n.Tr("System running time");
            public static readonly string EntitiesPreferencesString = L10n.Tr("Entities Preferences");
            public static readonly string EntitiesPreferencesPath = "Preferences/Entities";
            public static readonly string ViewOption = L10n.Tr("View Options");
            public static readonly string ColumnOption = L10n.Tr("Column Options");
            public static readonly string Setting = L10n.Tr("Setting");
        }

        static readonly ProfilerMarker k_OnUpdateMarker = new($"{nameof(SystemScheduleWindow)}.{nameof(OnUpdate)}");

        VisualElement m_Root;
        CenteredMessageElement m_NoWorld;
        SystemTreeView m_SystemTreeView;
        PropertyElement m_SystemInspectorView;
        VisualElement m_WorldSelector;
        VisualElement m_EmptySelectorWhenShowingFullPlayerLoop;
        internal WorldProxyManager WorldProxyManager; // internal for tests.
        PlayerLoopSystemGraph m_LocalSystemGraph;
        int m_LastWorldVersion;
        bool m_ViewChange;
        bool m_GraphChange;

        public SystemSearchView SystemSearchView { get; private set; }
        SearchFieldElement m_SearchField;

        WorldProxy m_SelectedWorldProxy;

        /// <summary>
        /// The systems window configuration. This is data which is managed externally by settings, tests or users but drives internal behaviours.
        /// </summary>
        [GeneratePropertyBag]
        public class SystemsWindowConfiguration
        {
            [CreateProperty]
            public bool Show0sInEntityCountAndTimeColumn = false;

            [CreateProperty]
            public bool ShowMorePrecisionForRunningTime = false;
            public bool ShowWorldColumn = true;
            public bool ShowNamespaceColumn = true;
            public bool ShowEntityCountColumn = true;
            public bool ShowTimeColumn = true;
            public bool ShowFullPlayerLoop;
        }

        // Internal for tests.
        internal SystemsWindowConfiguration m_Configuration;

        [MenuItem(Constants.MenuItems.SystemScheduleWindow, false, Constants.MenuItems.SystemScheduleWindowPriority)]
        static void OpenWindow()
        {
            var window = GetWindow<SystemScheduleWindow>();
            window.Show();
        }

        public SystemScheduleWindow()
            : base(Analytics.Window.Systems) { }

        /// <summary>
        /// Build the GUI for the system window.
        /// </summary>
        protected override void OnCreate()
        {
            Resources.AddCommonVariables(rootVisualElement);
            UnityEditor.Search.SearchElement.AppendStyleSheets(rootVisualElement);

            titleContent = EditorGUIUtility.TrTextContent(Contents.WindowName, EditorIcons.System);
            minSize = Constants.MinWindowSize;

            m_Root = new VisualElement();
            m_Root.AddToClassList(UssClasses.SystemScheduleWindow.WindowRoot);
            rootVisualElement.Add(m_Root);

            m_NoWorld = new CenteredMessageElement() { Message = NoWorldMessageContent };
            rootVisualElement.Add(m_NoWorld);
            m_NoWorld.Hide();

            m_Configuration = UserSettings<SystemsWindowPreferenceSettings>
                .GetOrCreate(Constants.Settings.SystemsWindow)
                .Configuration;

            Resources.Templates.SystemSchedule.AddStyles(m_Root);
            Resources.Templates.DotsEditorCommon.AddStyles(m_Root);

            WorldProxyManager = new WorldProxyManager();
            m_LocalSystemGraph = new PlayerLoopSystemGraph { WorldProxyManager = WorldProxyManager };
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            CreateToolBar(m_Root);

            var bodyView = new TwoPaneSplitView()
            {
                name = "BodySplitView",
                orientation = TwoPaneSplitViewOrientation.Horizontal,
                fixedPaneInitialDimension = 1024f,
            };

            m_Root.Add(bodyView);
            bodyView.Add(CreateTreeView());
            bodyView.Add(CreateInspectorView());

            Selection.selectionChanged += OnGlobalSelectionChanged;
        }

        protected override void OnCleanup()
        {
            WorldProxyManager?.Dispose();
            m_SystemTreeView?.Dispose();

            Selection.selectionChanged -= OnGlobalSelectionChanged;
        }

        void CreateToolBar(VisualElement root)
        {
            var toolbar = new VisualElement();
            Resources.Templates.SystemScheduleToolbar.Clone(toolbar);
            var leftSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.LeftSide);
            var rightSide = toolbar.Q(className: UssClasses.SystemScheduleWindow.Toolbar.RightSide);

            m_WorldSelector = CreateWorldSelector();
            m_EmptySelectorWhenShowingFullPlayerLoop = new ToolbarMenu { text = Contents.ShowFullPlayerLoopString };
            leftSide.Add(m_WorldSelector);
            leftSide.Add(m_EmptySelectorWhenShowingFullPlayerLoop);

            var dropdownSettings = InspectorUtility.CreateDropdownSettings(UssClasses.DotsEditorCommon.SettingsIcon);
            AppendOptionMenu(dropdownSettings.menu);

            UpdateWorldSelectorDisplay();
            rightSide.Add(dropdownSettings);

            root.Add(toolbar);
            AddSearchField(toolbar);
        }

        void AppendOptionMenu(DropdownMenu menu)
        {
            // Full player loop
            menu.AppendAction(Contents.ViewOption, null, DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                Contents.ShowFullPlayerLoopString,
                a =>
                {
                    m_Configuration.ShowFullPlayerLoop = !m_Configuration.ShowFullPlayerLoop;
                    WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowFullPlayerLoop;

                    UpdateWorldSelectorDisplay();

                    if (World.All.Count > 0)
                        RebuildTreeView();
                },
                a =>
                    m_Configuration.ShowFullPlayerLoop
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            menu.AppendSeparator();

            // Column options
            menu.AppendAction(Contents.ColumnOption, null, DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                Contents.World,
                a =>
                {
                    m_Configuration.ShowWorldColumn = !m_Configuration.ShowWorldColumn;
                    UpdateConfigurations();
                    m_SystemTreeView.RebuildColumns();
                },
                a =>
                    m_Configuration.ShowWorldColumn
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            menu.AppendAction(
                Contents.Namespace,
                a =>
                {
                    m_Configuration.ShowNamespaceColumn = !m_Configuration.ShowNamespaceColumn;
                    UpdateConfigurations();
                    m_SystemTreeView.RebuildColumns();
                },
                a =>
                    m_Configuration.ShowNamespaceColumn
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            menu.AppendAction(
                Contents.EntityCount,
                a =>
                {
                    m_Configuration.ShowEntityCountColumn = !m_Configuration.ShowEntityCountColumn;
                    UpdateConfigurations();
                    m_SystemTreeView.RebuildColumns();
                },
                a =>
                    m_Configuration.ShowEntityCountColumn
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            menu.AppendAction(
                Contents.Time,
                a =>
                {
                    m_Configuration.ShowTimeColumn = !m_Configuration.ShowTimeColumn;
                    UpdateConfigurations();
                    m_SystemTreeView.RebuildColumns();
                },
                a =>
                    m_Configuration.ShowTimeColumn
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            menu.AppendSeparator();

            // Setting
            menu.AppendAction(Contents.Setting, null, DropdownMenuAction.AlwaysDisabled);
            menu.AppendAction(
                Contents.EntitiesPreferencesString,
                a =>
                {
                    SettingsService.OpenUserPreferences(Contents.EntitiesPreferencesPath);
                }
            );
        }

        void AddSearchField(VisualElement root)
        {
            SystemSearchView = new SystemSearchView(this);
            m_SearchField = new SearchFieldElement(
                "SystemSearch",
                SystemSearchView,
                SearchQueryBuilderViewFlags.Default
            );
            root.Add(m_SearchField);
        }

        void UpdateWorldSelectorDisplay()
        {
            m_WorldSelector.SetVisibility(!m_Configuration.ShowFullPlayerLoop);
            m_EmptySelectorWhenShowingFullPlayerLoop.SetVisibility(m_Configuration.ShowFullPlayerLoop);
        }

        VisualElement CreateTreeView()
        {
            m_SystemTreeView = new SystemTreeView
            {
                viewDataKey = nameof(SystemScheduleWindow),
                style = { flexGrow = 1 },
                LocalSystemGraph = m_LocalSystemGraph,
            };
            UpdateConfigurations();
            m_SystemTreeView.SetSelection();
            m_SystemTreeView.RebuildColumns();
            m_SystemTreeView.systemSelectionChanged += UpdateSelectedSystem;
            return m_SystemTreeView;
        }

        void UpdateSelectedSystem(SystemProxy systemProxy)
        {
            if (systemProxy.World.IsCreated)
            {
                var content = new SystemContent(systemProxy.World, systemProxy);
                m_SystemInspectorView.SetTarget(new SystemContentDisplay(content));
            }
            else
                m_SystemInspectorView.SetTarget(default(SystemContentDisplay));
            m_SystemInspectorView.ForceReload();
        }

        void UpdateConfigurations()
        {
            m_SystemTreeView.ShowWorldColumn = m_Configuration.ShowWorldColumn;
            m_SystemTreeView.ShowNamespaceColumn = m_Configuration.ShowNamespaceColumn;
            m_SystemTreeView.ShowEntityCountColumn = m_Configuration.ShowEntityCountColumn;
            m_SystemTreeView.ShowTimeColumn = m_Configuration.ShowTimeColumn;
            m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;
            m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
        }

        VisualElement CreateInspectorView()
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            m_SystemInspectorView = new PropertyElement();
            Resources.AddCommonVariables(m_SystemInspectorView);

            Resources.Templates.ContentProvider.System.AddStyles(m_SystemInspectorView);
            m_SystemInspectorView.AddToClassList(UssClasses.Content.SystemInspector.SystemContainer);

            scrollView.Add(m_SystemInspectorView);
            return scrollView;
        }

        void UpdatePreferences()
        {
            if (m_SystemTreeView.ShowMorePrecisionForRunningTime != m_Configuration.ShowMorePrecisionForRunningTime)
                m_SystemTreeView.ShowMorePrecisionForRunningTime = m_Configuration.ShowMorePrecisionForRunningTime;

            if (m_SystemTreeView.Show0sInEntityCountAndTimeColumn != m_Configuration.Show0sInEntityCountAndTimeColumn)
                m_SystemTreeView.Show0sInEntityCountAndTimeColumn = m_Configuration.Show0sInEntityCountAndTimeColumn;
        }

        public void StopSearch() => m_SystemTreeView.StopSearch();

        public void SetResults(IList<SearchItem> results) => m_SystemTreeView.SetResults(results);

        // internal for test.
        internal void RebuildTreeView()
        {
            m_SystemTreeView.Refresh(m_Configuration.ShowFullPlayerLoop ? null : m_SelectedWorldProxy);
        }

        internal void ForceUpdate()
        {
            if (m_SystemTreeView == null || WorldProxyManager == null)
                return;

            UpdatePreferences();

            // Force all active updaters to rebuild their proxies
            foreach (var updater in WorldProxyManager.GetAllWorldProxyUpdaters())
            {
                if (!updater.IsActive())
                    continue;

                updater.ResetWorldProxy();
            }

            // Rebuild graph and tree view
            m_LocalSystemGraph.BuildCurrentGraph();
            RebuildTreeView();

            m_GraphChange = false;
            m_ViewChange = false;
        }

        protected override void OnUpdate()
        {
            using (k_OnUpdateMarker.Auto())
            {
                if (m_SystemTreeView == null || WorldProxyManager == null)
                    return;

                UpdatePreferences();

                if (SystemSearchView != null)
                    SystemSearchView.position = position;

                foreach (var updater in WorldProxyManager.GetAllWorldProxyUpdaters())
                {
                    if (updater.IsActive() && updater.IsDirty())
                    {
                        m_GraphChange = true;
                        updater.SetClean();
                    }
                }

                if (m_GraphChange)
                    m_LocalSystemGraph.BuildCurrentGraph();

                if (m_GraphChange || m_ViewChange)
                    RebuildTreeView();

                m_GraphChange = false;
                m_ViewChange = false;
            }
        }

        protected override void OnWorldsChanged(bool containsAnyWorld)
        {
            m_Root.SetVisibility(containsAnyWorld);
            m_NoWorld.SetVisibility(!containsAnyWorld);

            if (m_SystemTreeView == null)
                return;

            WorldProxyManager.IsFullPlayerLoop = m_Configuration.ShowFullPlayerLoop;
            WorldProxyManager.CreateWorldProxiesForAllWorlds();

            if (SelectedWorld != null && SelectedWorld.IsCreated)
            {
                m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(SelectedWorld);
                WorldProxyManager.SetSelectedWorldProxy(m_SelectedWorldProxy);
            }

            if (m_Configuration.ShowFullPlayerLoop)
                m_GraphChange = true;
        }

        protected override void OnWorldSelected(World world)
        {
            if (world == null || !world.IsCreated)
                return;

            if (m_Configuration.ShowFullPlayerLoop)
                return;

            m_SelectedWorldProxy = WorldProxyManager.GetWorldProxyForGivenWorld(world);
            WorldProxyManager.SetSelectedWorldProxy(m_SelectedWorldProxy);
            SystemSearchView.SetWorld(world);

            m_ViewChange = true;
        }

        public static void HighlightSystem(SystemProxy systemProxy)
        {
            SystemTreeView.SelectedSystem = systemProxy;

            if (HasOpenInstances<SystemScheduleWindow>())
            {
                var systemWindow = GetWindow<SystemScheduleWindow>();
                systemWindow.m_SystemTreeView.SetSelection();
                systemWindow.UpdateSelectedSystem(SystemTreeView.SelectedSystem);
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (Unsupported.IsDeveloperMode())
            {
                menu.AddItem(
                    new GUIContent($"Debug..."),
                    false,
                    () => SelectionUtility.ShowInWindow(new SystemsWindowDebugContentProvider())
                );
            }
        }

        void OnGlobalSelectionChanged()
        {
            if (Selection.activeObject is InspectorContent content && content.Content.Name.Equals(Contents.System))
                return;

            SystemTreeView.SelectedSystem = default;
            m_SystemTreeView.MultiColumnTreeViewElement.ClearSelection();
        }
    }
}
