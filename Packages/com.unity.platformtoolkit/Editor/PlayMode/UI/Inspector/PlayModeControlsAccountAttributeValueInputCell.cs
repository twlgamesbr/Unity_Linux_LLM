using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    [UxmlElement]
    internal partial class PlayModeControlsAccountAttributeValueInputCell : VisualElement, INotifyValueChanged<object>
    {
        private object m_Value;

        [CreateProperty]
        public object value
        {
            get => m_Value;
            set
            {
                if (EqualityComparer<object>.Default.Equals(m_Value, value))
                    return;

                var previous = m_Value;
                SetValueWithoutNotify(value);

                using var evt = ChangeEvent<object>.GetPooled(previous, m_Value);
                evt.target = this;
                SendEvent(evt);
            }
        }

        private Type m_ValueType;

        [CreateProperty]
        public Type valueType
        {
            get => m_ValueType;
            set
            {
                if (m_ValueType == value)
                    return;
                m_ValueType = value;
                UpdateUI();
            }
        }

        public void SetValueWithoutNotify(object newValue)
        {
            m_Value = newValue;
        }

        private void UpdateUI()
        {
            VisualElement valueField;
            if (valueType == typeof(string))
            {
                var textField = new TextField();
                textField.SetBinding(
                    "value",
                    new DataBinding
                    {
                        dataSourcePath = new PropertyPath("Value"),
                        updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                    }
                );
                valueField = textField;
            }
            else if (valueType == typeof(int))
            {
                var intField = new IntegerField();
                intField.SetBinding(
                    "value",
                    new DataBinding
                    {
                        dataSourcePath = new PropertyPath("Value"),
                        updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                    }
                );
                valueField = intField;
            }
            else if (valueType == typeof(long))
            {
                var longField = new LongField();
                longField.SetBinding(
                    "value",
                    new DataBinding
                    {
                        dataSourcePath = new PropertyPath("Value"),
                        updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                    }
                );
                valueField = longField;
            }
            else if (valueType == typeof(Texture2D))
            {
                var objectField = new ObjectField { objectType = typeof(Texture2D) };
                objectField.searchContext = SearchService.CreateContext("asset", $"t:{nameof(Texture2D)}");
                objectField.SetBinding(
                    "value",
                    new DataBinding
                    {
                        dataSourcePath = new PropertyPath("Value"),
                        updateTrigger = BindingUpdateTrigger.OnSourceChanged,
                    }
                );
                valueField = objectField;
            }
            else
            {
                throw new NotSupportedException(
                    $"Serialization not implemented for type: {valueType?.FullName ?? "null"}"
                );
            }
            Clear();
            Add(valueField);
        }
    }
}
