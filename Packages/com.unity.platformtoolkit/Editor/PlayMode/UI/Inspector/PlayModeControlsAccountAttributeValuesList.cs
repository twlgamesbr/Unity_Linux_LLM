using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    [UxmlElement]
    internal partial class PlayModeControlsAccountAttributeValuesList : VisualElement
    {
        public PlayModeControlsAccountAttributeValuesList()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.unity.platformtoolkit/Editor/PlayMode/UI/Inspector/PlayModeControlsAccountAttributeValuesList.uxml"
            );
            uxml.CloneTree(this);
        }
    }
}
