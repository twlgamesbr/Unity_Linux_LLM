// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Enum for the selection state of a submodule.
    /// A submodule can either be selected, not selected or partially selected(it is not selected but at least one of it's descendants is.)
    /// </summary>
    enum SubmoduleSelectionState
    {
        NotSelected = 0,
        PartiallySelected = 1,
        Selected = 2
    }

    /// <summary>
    /// A class for organizing submodules according to their hierarchy.
    /// </summary>
    class SubmoduleHierarchyNode
    {
        public SubmoduleDefinition? Submodule = null;
        public SubmoduleHierarchyNode? Parent = null;
        public List<SubmoduleHierarchyNode> Children = new();
        public string Name => Submodule?.name ?? "";
        public string Description => Submodule?.description ?? "";
        public bool Selected { get; set; } = false;

        public SubmoduleSelectionState State
        {
            get
            {
                if (Selected)
                    return SubmoduleSelectionState.Selected;

                foreach (var child in Children)
                {
                    if (child.State != SubmoduleSelectionState.NotSelected)
                        return SubmoduleSelectionState.PartiallySelected;
                }

                return SubmoduleSelectionState.NotSelected;
            }
        }

        /// <summary>
        /// Select submodules in hierarchy according to set of selected submodules.
        /// Child submodules will be automatically selected if parent is selected.
        /// </summary>
        /// <param name="selectedSubmodules">A set of selected submodules.</param>
        public void SetSelection(HashSet<string> selectedSubmodules)
        {
            // Select if the parent is selected or 'this' is a valid submodule and its name is in the set
            Selected = Parent?.Selected == true || (Submodule != null && selectedSubmodules.Contains(Name));

            var allChildrenSelected = Children.Count > 0;
            foreach (var child in Children)
            {
                child.SetSelection(selectedSubmodules);
                if (!child.Selected)
                    allChildrenSelected = false;
            }

            // Update selection of this node if all children become selected
            Selected = Selected || allChildrenSelected;
        }

        /// <summary>
        /// Recursively update selection.
        /// </summary>
        /// <param name="selected">Wether a submodule and its descendants are selected.</param>
        public void UpdateSelection(bool selected)
        {
            Selected = selected;

            foreach (var child in Children)
            {
                child.UpdateSelection(selected);
            }
        }

        /// <summary>
        /// Recursively update selection of parent submodules.
        /// </summary>
        public void UpdateParentSelection()
        {
            if (Parent == null)
                return;

            // Check if all siblings are selected
            bool allSelected = true;
            foreach (var sibling in Parent.Children)
            {
                if (!sibling.Selected)
                {
                    allSelected = false;
                    break;
                }
            }

            Parent.Selected = allSelected;
            Parent.UpdateParentSelection();
        }

        /// <summary>
        /// Get the (sparse) set of selected submodules.
        /// If a submodule is selected and contains children only the parent is part of the set.
        /// </summary>
        /// <returns>Set of selected submodules.</returns>
        public HashSet<string> GetSelection()
        {
            var selectedSubmodules = new HashSet<string>();
            AddSelectedSubmodules(selectedSubmodules);

            return selectedSubmodules;
        }

        /// <summary>
        /// Export the current submodule selection as a submodule profiling report.
        /// All selected submodules are marked as unused in the report.
        /// </summary>
        /// <returns>A submodule profiling report with</returns>
        public SubmoduleProfilingReport GetSubmoduleProfilingReport()
        {
            var report = new SubmoduleProfilingReport();
            AddSelectedSubmodulesToReport(report);

            return report;
        }

        /// <summary>
        /// Traverse the submodule hierarchy and call the iterator for each node.
        /// </summary>
        /// <param name="iterator">An iterator function that gets called with the hierarchy nodes.</param>
        public void Traverse(Action<SubmoduleHierarchyNode> iterator)
        {
            iterator(this);

            foreach (var child in Children)
            {
                child.Traverse(iterator);
            }
        }

        private void AddSelectedSubmodules(HashSet<string> selectedSubmodules)
        {
            if (Selected && Submodule != null)
                selectedSubmodules.Add(Name);

            // Child submodules are implicitly selected if parent is in the list
            // Only add children if submodule is partially selected
            if (Submodule == null || State == SubmoduleSelectionState.PartiallySelected)
            {
                foreach (var child in Children)
                    child.AddSelectedSubmodules(selectedSubmodules);
            }
        }

        private void AddSelectedSubmodulesToReport(SubmoduleProfilingReport report)
        {
            // Only add submodule to report if it has functions or methods of its own
            bool HasCode = Submodule?.functions.Count > 0 || Submodule?.csharpMethodFilters != null;
            if (Submodule != null && HasCode)
            {
                // Consider submodules selected for stripping as unused
                report.submodules[Submodule.name] = !Selected;
            }

            // Add child submodules
            foreach (var child in Children)
                child.AddSelectedSubmodulesToReport(report);
        }
    }

    /// <summary>
    /// Class <b>SubmoduleHierarchyGenerator</b> organizes submodules according to their hierarchy and can prints all submodules of a submodule definition file in a text table.
    /// </summary>
    class SubmoduleHierarchyGenerator
    {
        Dictionary<string, SubmoduleDefinition> Submodules = new();
        HashSet<string> InsertedSubmodules = new();
        static string TableFormat = "|{0,-50}|{1,-120}|";

        /// <summary>
        /// Print submodule defintion file as table. The table will include name and description of a submodule and show nested submodules.
        /// </summary>
        /// <param name="config">A loaded submodule definition file.</param>
        /// <returns>Table as text.</returns>
        public string FormatSubmodules(SubmoduleConfig config)
        {
            // Create hierarchy
            SubmoduleHierarchyNode hierarchy = CreateSubmoduleHierarchy(config);

            // Format submodule hierarchy documentation
            var output = new StringBuilder();

            // Write table header
            output.AppendLine("Available submodules:\n");
            output.AppendLine(string.Format(TableFormat, "Name", "Description"));
            output.AppendLine($"|{new string('-', 50)}|{new string('-', 120)}|");

            // Write table rows for all submodules
            FormatSubmodules(hierarchy, -1, output);

            return output.ToString();
        }

        /// <summary>
        /// Organize submodules in definition file into a hierarchy.
        /// </summary>
        /// <param name="config">A loaded submodule definition file.</param>
        /// <returns>Hierarchy of submodules.</returns>
        public SubmoduleHierarchyNode CreateSubmoduleHierarchy(SubmoduleConfig config)
        {
            Submodules.Clear();
            InsertedSubmodules.Clear();

            // Convert list of submodules to dictionary for easy lookup
            var rootSubmodules = new HashSet<string>();
            foreach (var submodule in config.submodules)
            {
                Submodules[submodule.name] = submodule;

                // Add submodule as root candidate
                rootSubmodules.Add(submodule.name);
            }

            // Remove submodules that are referenced by other submodules
            foreach (var submodule in config.submodules)
            {
                foreach (var child in submodule.submodules)
                {
                    rootSubmodules.Remove(child);
                }
            }

            // Create hierarchy starting from root
            var hierarchy = new SubmoduleHierarchyNode();
            foreach (var submoduleName in rootSubmodules)
            {
                var submodule = Submodules[submoduleName];
                AddSubmoduleToHierarchy(hierarchy, submodule);
            }

            // Handle all submodules not handled yet, e.g.,
            // submodules that have circular dependency.
            foreach (var submodule in config.submodules)
            {
                if (InsertedSubmodules.Contains(submodule.name))
                    continue;

                AddSubmoduleToHierarchy(hierarchy, submodule);
            }

            // Sort hierarchy by name
            hierarchy.Traverse((node) =>
            {
                node.Children.Sort((a, b) => String.Compare(a.Name, b.Name));
            });

            return hierarchy;
        }

        void AddSubmoduleToHierarchy(SubmoduleHierarchyNode parent, SubmoduleDefinition submodule)
        {
            // Only handle each submodule once to prevent infinite recursion
            // when there are cycles.
            if (InsertedSubmodules.Contains(submodule.name))
                return;

            var hierarchyNode = new SubmoduleHierarchyNode()
            {
                Submodule = submodule,
                Parent = parent
            };
            parent.Children.Add(hierarchyNode);
            InsertedSubmodules.Add(submodule.name);

            // Recursivly add ancestors
            foreach (var child in submodule.submodules)
            {
                if (Submodules.TryGetValue(child, out var childSubmodule))
                {
                    AddSubmoduleToHierarchy(hierarchyNode, childSubmodule);
                }
            }
        }

        void FormatSubmodules(SubmoduleHierarchyNode node, int indentation, StringBuilder output)
        {
            if (node.Submodule != null)
            {
                output.AppendLine(string.Format(TableFormat, $"{GetIndentationString(indentation)}{node.Submodule.name}", node.Submodule.description));

                // Add nested submodule list header
                if (node.Children.Count > 0)
                    output.AppendLine(string.Format(TableFormat, $"{GetIndentationString(indentation)}Submodules:", ""));
            }

            // Print nested submodules
            foreach (var child in node.Children)
            {
                FormatSubmodules(child, indentation + 1, output);
            }

            // Add an empty line if submodule had nested submodules
            if (node.Children.Count > 0)
                output.AppendLine(string.Format(TableFormat, "", ""));
        }

        static string GetIndentationString(int indentation)
        {
            if (indentation <= 0)
            {
                return "";
            }

            return new string('*', indentation);
        }
    }
}
