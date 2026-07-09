using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeControlsAttributeDefinitionsList : VisualElement
    {
        public PlayModeControlsAttributeDefinitionsList(PlayModeControlsViewModel viewModel)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/PlayMode/UI/Inspector/PlayModeControlsAttributeDefinitionsList.uxml");
            uxml.CloneTree(this);

            var attributeDefinitions = this.Q<MultiColumnListView>("attribute-definitions");

            // This is a workaround for a UI Toolkit bug where you cannot set the MultiColumnListView's template cell to a custom C# UXML element
            var typeCellUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/PlayMode/UI/Inspector/PlayModeControlsAttributeDefinitionValueTypePopupField.uxml");
            var typeColumn = attributeDefinitions.columns.First(c => c.name == "Type");
            typeColumn.makeCell = () => typeCellUxml.Instantiate();

            attributeDefinitions.onAdd += _ => viewModel.CreateAttributeDefinition();
            attributeDefinitions.onRemove += list =>
            {
                var index = list.selectedIndex < 0 ? viewModel.AttributeDefinitions.Count - 1 : list.selectedIndex;
                viewModel.RemoveAttributeDefinition(index);

                list.ClearSelection();
                list.RefreshItems();
            };
        }
    }
}
