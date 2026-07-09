using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeControlsAccountDataList : VisualElement
    {
        // Dummy instance placed on each fresh ListView item in makeItem to ensure reference not null on binding evaluation
        private static readonly PlayModeAccountData s_DefaultAccountData = new();

        public PlayModeControlsAccountDataList(PlayModeControlsViewModel playModeControlsView)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/Playmode/UI/Inspector/PlayModeControlsAccountDataList.uxml");
            uxml.CloneTree(this);

            // Note the binding is configured in UXML
            var accountDataList = this.Q<ListView>("account-data-list");

            accountDataList.makeItem = () =>
            {
                var field = new PlayModeControlsAccountDataField(playModeControlsView);
                field.dataSource = s_DefaultAccountData;
                return field;
            };
            accountDataList.bindItem = (v, i) =>
                ((PlayModeControlsAccountDataField)v).Bind(playModeControlsView.AccountData.ElementAt(i));
            accountDataList.onAdd += AddAccount;
            accountDataList.onRemove += RemoveAccount;

            void AddAccount(BaseListView listView) => playModeControlsView.CreateNewAccount();

            void RemoveAccount(BaseListView listView)
            {
                int removalIndex = -1;

                if (listView.selectedIndex >= 0)
                    removalIndex = listView.selectedIndex;
                else if (listView.itemsSource.Count > 0)
                    removalIndex = listView.itemsSource.Count - 1;

                if (removalIndex != -1)
                {
                    playModeControlsView.RemoveAccount(removalIndex);

                    // Must call this as overriding onRemove stops the ListView internally doing this itself
                    accountDataList.ClearSelection();

                    listView.Rebuild();
                }
            }
        }
    }
}
