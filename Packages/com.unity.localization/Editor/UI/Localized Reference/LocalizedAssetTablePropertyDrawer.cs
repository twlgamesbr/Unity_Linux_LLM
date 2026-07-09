using UnityEngine.Localization;

namespace UnityEditor.Localization.UI
{
    [CustomPropertyDrawer(typeof(LocalizedAssetTable), true)]
    class LocalizedAssetTablePropertyDrawer : LocalizedTablePropertyDrawer<AssetTableCollection>
    {
        static LocalizedAssetTablePropertyDrawer()
        {
            GetProjectTableCollections = LocalizationEditorSettings.GetAssetTableCollections;
        }
    }
}
