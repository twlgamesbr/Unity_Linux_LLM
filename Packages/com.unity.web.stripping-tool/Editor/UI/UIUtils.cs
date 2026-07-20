using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Web.Stripping.Editor
{
    static class UIUtils
    {
        public static void SetVisible(VisualElement elem, bool visible) =>
            elem.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        // NOTE: requires CommonStyles.uss for the styling
        public static Label CreateTitleLabel(string text)
        {
            var title = new DetailLabel(text);
            title.AddToClassList("title-label");
            return title;
        }

        // NOTE: requires CommonStyles.uss for the styling
        public static Label CreateDescriptionLabel(string text)
        {
            var label = new DetailLabel(text);
            label.AddToClassList("description-label");
            return label;
        }

        // 'page' page in the manual, if not provided, the default manual index is shown
        public static ToolbarButton AddHelpButton(Toolbar toolbar, string page = null)
        {
            var button = new ToolbarButton
            {
                iconImage = EditorGUIUtility.IconContent("_Help").image as Texture2D,
                tooltip = "Open package manual.",
            };
            button.clicked += () => Help.ShowHelpPage(PackageConstants.GetDocumentationUrl(page));
            toolbar.Add(button);
            return button;
        }
    }
}
