using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    [UxmlElement]
    internal partial class PlayModeControlsAttributeDefinitionValueTypePopupField : PopupField<Type>
    {
        public PlayModeControlsAttributeDefinitionValueTypePopupField()
        {
            choices = PlayModeControlsAttributeDefinition.k_SupportedValueTypes.ToList();
            index = 0;
            formatSelectedValueCallback = FormatItem;
            formatListItemCallback = FormatItem;
        }

        static string FormatItem(Type valueType)
        {
            if (valueType == null)
                throw new ArgumentNullException();
            return valueType.FullName;
        }
    }
}
