using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A UI window for selecting submodules.
    /// The window shows the hierarchy of submodules and allows to show
    /// the description of each submodule in a details view.
    /// </summary>
    class SubmoduleSelectionWindow : EditorWindow
    {
        // Path inside "Documentation~" folder to the documentation page containing the docs of this asset, no file extension.
        internal const string k_DocumentationPage = "submodule-reference";

        [ExcludeFromCodeCoverage] // used only by human-driven file dialog code
        static string LastProfilingDataPath
        {
            // default to project's root
            get => PackageSettings.GetUserSetting("SubmoduleSelectionWindow.LastProfilingDataPath", Utils.ProjectPath);
            set => PackageSettings.SetUserSetting("SubmoduleSelectionWindow.LastProfilingDataPath", value);
        }

        /// <summary>
        /// User data assigned to toggles in selection window
        /// </summary>
        private class ToggleUserData
        {
            public SubmoduleHierarchyNode Submodule = null;
            public int Index = -1;
        }

        [SerializeField]
        VisualTreeAsset m_VisualTreeAsset = default;

        /// <summary>
        /// The set of selected submodules. Changing this value will update the UI.
        /// </summary>
        public HashSet<string> SelectedSubmodules
        {
            get
            {
                if (m_Hierarchy != null)
                {
                    return m_Hierarchy.GetSelection();
                }

                return m_SelectedSubmodules;
            }

            set
            {
                m_SelectedSubmodules = value;

                if (m_Hierarchy != null)
                {
                    m_Hierarchy.SetSelection(value);
                    m_TreeView.RefreshItems();
                }
            }
        }

        public delegate void SubmodulesChangedCallback(HashSet<string> selectedSubmodules);
        /// <summary>
        /// Raised when the selection of submodules changes.
        /// </summary>
        public event SubmodulesChangedCallback SelectedSubmodulesChanged;

        private HashSet<string> m_SelectedSubmodules = new();
        private int ItemIdCounter = 0;

        private MultiColumnTreeView m_TreeView = null;
        private ScrollView m_DetailPane = null;
        private SubmoduleHierarchyNode m_Hierarchy = null;
        private List<TreeViewItemData<SubmoduleHierarchyNode>> m_TreeViewData = new();

        internal bool CloseWhenFocusLost { get; set; }

        /// <summary>
        /// Gets the window as utility popup window which closes itself automatically when focus is lost.
        /// </summary>
        /// <remarks>
        /// This is the preferred way of using Submodule Selection window.
        /// </remarks>
        /// <returns></returns>
        public static SubmoduleSelectionWindow GetAsUtilityPopup()
        {
            var wnd = GetWindow<SubmoduleSelectionWindow>(utility: true);
            wnd.CloseWhenFocusLost = true;
            return wnd;
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Select Submodules");
            minSize = new Vector2(600, 250);
        }

        void OnLostFocus()
        {
            if (CloseWhenFocusLost)
                Close();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            m_VisualTreeAsset.CloneTree(root);

            // Hook up buttons
            root.Q<Button>("SelectAllButton").clicked += () => OnBatchSelection(true);
            root.Q<Button>("SelectNoneButton").clicked += () => OnBatchSelection(false);
            root.Q<Button>("SetFromProfilingDataButton").clicked += () => SetSubmodulesFromProfilingDataUsingFileDialog();
            UIUtils.AddHelpButton(root.Q<Toolbar>(), k_DocumentationPage);

            // Load submodule definition and create hierarchy
            m_TreeView = root.Q<MultiColumnTreeView>();
            LoadSubmoduleDefinition();
            UpdateTreeView(m_Hierarchy);

            // Bind data
            m_TreeView.columns["index"].makeCell = () => new Label();
            m_TreeView.columns["active"].makeCell = () =>
            {
                var toggle = new Toggle();
                toggle.name = "unity-select-submodule-toggle";
                toggle.RegisterCallback<ChangeEvent<bool>>((evt) =>
                {
                    if (toggle.userData is ToggleUserData userData)
                    {
                        OnSubmoduleToggleChange(userData, evt);
                    }
                });

                return toggle;
            };

            // For each column, set Column.bindCell to bind an initialized node to a data item.
            m_TreeView.columns["index"].bindCell = (VisualElement element, int index) =>
                BindTextCell(element, index);
            m_TreeView.columns["active"].bindCell = (VisualElement element, int index) =>
                BindSelectionCell(element, index);

            m_TreeView.itemsChosen += ShowSubmoduleDetails;
            m_TreeView.selectionChanged += ShowSubmoduleDetails;

            m_DetailPane = root.Q<ScrollView>("detail-pane");
            ShowSubmoduleDetails(new List<object>());
        }

        private void BindTextCell(VisualElement element, int index)
        {
            var labelElement = element as Label;
            labelElement.text = m_TreeView.GetItemDataForIndex<SubmoduleHierarchyNode>(index).Name;
        }

        private void BindSelectionCell(VisualElement element, int index)
        {
            var submodule = m_TreeView.GetItemDataForIndex<SubmoduleHierarchyNode>(index);
            var toggle = element as Toggle;
            toggle.userData = new ToggleUserData()
            {
                Submodule = submodule,
                Index = index
            };
            switch (submodule.State)
            {
                case SubmoduleSelectionState.NotSelected:
                    toggle.SetValueWithoutNotify(submodule.Selected);
                    toggle.showMixedValue = false;
                    break;
                case SubmoduleSelectionState.PartiallySelected:
                    toggle.SetValueWithoutNotify(submodule.Selected);
                    toggle.showMixedValue = true;
                    break;
                case SubmoduleSelectionState.Selected:
                    toggle.SetValueWithoutNotify(submodule.Selected);
                    toggle.showMixedValue = false;
                    break;
            }
        }

        private void LoadSubmoduleDefinition()
        {
            // Load submodule definition
            var config = SubmoduleDefinitionLoader.Load(
                BuildToolsLocator.GetSubmoduleDefinitionFilePaths(false, false),
                Application.unityVersion
            );

            // Create submodule hierarchy for tree view
            var hierarchyGenerator = new SubmoduleHierarchyGenerator();
            m_Hierarchy = hierarchyGenerator.CreateSubmoduleHierarchy(config);
            m_Hierarchy.SetSelection(m_SelectedSubmodules);
        }

        private void UpdateTreeView(SubmoduleHierarchyNode hierarchy)
        {
            // Insert hierarchy into tree view
            m_TreeViewData = new List<TreeViewItemData<SubmoduleHierarchyNode>>();
            ItemIdCounter = 0;
            AddTreeViewItems(hierarchy, m_TreeViewData);
            m_TreeView.SetRootItems(m_TreeViewData);
        }

        private void AddTreeViewItems(SubmoduleHierarchyNode parent, List<TreeViewItemData<SubmoduleHierarchyNode>> treeViewItems)
        {
            foreach (var submodule in parent.Children)
            {
                var itemId = ItemIdCounter++;
                var childItems = new List<TreeViewItemData<SubmoduleHierarchyNode>>();
                var item = new TreeViewItemData<SubmoduleHierarchyNode>(itemId, submodule, childItems);

                // Recursively add child items
                if (submodule.Children.Count > 0)
                    AddTreeViewItems(submodule, childItems);

                treeViewItems.Add(item);
            }
        }

        private void OnSubmoduleToggleChange(ToggleUserData userData, ChangeEvent<bool> evt)
        {
            var submodule = userData.Submodule;

            // Update parents and children recursive
            submodule.Selected = evt.newValue;
            submodule.UpdateParentSelection();
            submodule.UpdateSelection(evt.newValue);

            m_TreeView.RefreshItems();
            SelectedSubmodulesChanged?.Invoke(SelectedSubmodules);
        }

        private void OnBatchSelection(bool selected)
        {
            m_Hierarchy.UpdateSelection(selected);
            m_TreeView.RefreshItems();
            SelectedSubmodulesChanged?.Invoke(SelectedSubmodules);
        }

        private void ShowSubmoduleDetails(IEnumerable<object> selectedItems)
        {
            m_DetailPane.Clear();

            // Get selected submodule
            SubmoduleHierarchyNode submodule = null;
            foreach (var current in selectedItems)
            {
                if (current is SubmoduleHierarchyNode selectedSubmodule)
                {
                    submodule = selectedSubmodule;
                    break;
                }
            }

            if (submodule == null)
            {
                m_DetailPane.Add(UIUtils.CreateDescriptionLabel("Select an item to view its details."));
            }
            else
            {
                m_DetailPane.Add(UIUtils.CreateTitleLabel(submodule.Name));
                m_DetailPane.Add(UIUtils.CreateDescriptionLabel(submodule.Description));
            }
        }

        internal enum MergeBehavior { Overwrite, Cancel, Combine }

        [ExcludeFromCodeCoverage] // File dialogs, impossible / very tricky to drive via CI
        void SetSubmodulesFromProfilingDataUsingFileDialog()
        {
            var path = EditorUtility.OpenFilePanelWithFilters(
                "Import Profiling Data",
                LastProfilingDataPath,
                new[] { "Submodule Profiling Report", "json", "All Files", "*" }
            );

            [ExcludeFromCodeCoverage] // Can't test dialog with automated testing
            MergeBehavior OverwriteSelection()
            {
                if (!SelectedSubmodules.Any())
                    return MergeBehavior.Overwrite;

                return (MergeBehavior)EditorUtility.DisplayDialogComplex(
                    "Import Profiling Data",
                    "Do you wish to overwrite the existing submodule selection?",
                    "Overwrite", // 0
                    "Cancel", // 1
                    "Combine" // 2
                );
            }

            SetSubmodulesFromProfilingData(path, OverwriteSelection);
            LastProfilingDataPath = path;
        }

        internal void SetSubmodulesFromProfilingData(string path, Func<MergeBehavior> mergeBehaviorSelector)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var report = SubmoduleProfilingReport.LoadFromFile(path);

            var mergeBehavior = SelectedSubmodules.Any() ? mergeBehaviorSelector() : MergeBehavior.Overwrite;
            if (mergeBehavior == MergeBehavior.Cancel)
                return;

            // When loading multiple files we want to select submodules for stripping
            // that are unused in all reports.
            if (mergeBehavior == MergeBehavior.Combine && SelectedSubmodules.Any())
                report.MergeWith(m_Hierarchy.GetSubmoduleProfilingReport());

            SelectedSubmodules = report.UnusedSubmodules;
            SelectedSubmodulesChanged?.Invoke(SelectedSubmodules);
        }
    }
}
