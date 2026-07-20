using EditorAttributes.Editor.Utility;
using UnityEngine.UIElements;

namespace EditorAttributes.Editor
{
    public class GUIColorDrawer
    {
        public static void ColorField(VisualElement root, IColorAttribute colorAttribute)
        {
            HelpBox errorBox = new();

            EditorExtension.GLOBAL_COLOR = ColorUtils.GetColorFromAttribute(colorAttribute, errorBox);
            ColorUtils.ApplyColor(root, colorAttribute, errorBox);
            PropertyDrawerBase.DisplayErrorBox(root, errorBox);
        }
    }
}
