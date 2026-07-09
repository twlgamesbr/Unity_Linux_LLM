using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.Editor
{
    internal static class CommonConverters
    {
        [InitializeOnLoadMethod]
        public static void RegisterConverters()
        {
            var showWarningConverter = new ConverterGroup("Show Warning Converter");
            var showStyleEnum = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            var hideStyleEnum = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            showWarningConverter.AddConverter((ref bool val) => val ? showStyleEnum : hideStyleEnum);
            ConverterGroups.RegisterConverterGroup(showWarningConverter);
        }
    }
}
