using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeControlsAccountDataField : VisualElement
    {
        private PlayModeControlsAccountAchievementsList m_AchievementsList;
        private PlayModeControlsSaveDataField m_SavesList;

        // Dummy instance set on the inner achievements list to ensure dataSource is not null on uxml binding evaluation
        private static readonly PlayModeAccountAchievementData s_DefaultAchievementListData = new();

        public PlayModeControlsAccountDataField(PlayModeControlsViewModel playModeControlsView)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/PlayMode/UI/Inspector/PlayModeControlsAccountDataField.uxml");
            uxml.CloneTree(this);
            m_AchievementsList = this.Q<PlayModeControlsAccountAchievementsList>();
            m_AchievementsList.dataSource = s_DefaultAchievementListData;
            m_SavesList = this.Q<PlayModeControlsSaveDataField>();

            // Init sets a dummy dataSource on the inner SaveListView (see PlayModeControlsSaveDataField.Init).
            m_SavesList.Init(playModeControlsView, isPerAccountSave: true);
        }

        public void Bind(PlayModeAccountData accountData)
        {
            dataSource = accountData;
            m_AchievementsList.Bind(accountData.Achievements);
            m_SavesList.Bind(accountData.Saves, "Saves");
        }
    }
}
