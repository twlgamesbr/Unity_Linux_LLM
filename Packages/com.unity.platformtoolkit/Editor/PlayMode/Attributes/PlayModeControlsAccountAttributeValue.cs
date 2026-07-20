using System;
using Unity.PlatformToolkit.Editor;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    [Serializable]
    internal class PlayModeControlsAccountAttributeValue : INotifyBindablePropertyChanged
    {
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
        private ScriptableObjectDataChangePersistor m_Persistor;
        private int m_UserIndex;

        [SerializeField]
        private string m_AttributeDefinitionGuid;
        public string AttributeDefinitionGuid => m_AttributeDefinitionGuid;

        [CreateProperty]
        public PlayModeControlsAttributeDefinition AttributeDefinition { get; private set; }

        [SerializeField]
        private string m_SerializedValue;

        private object m_Value;

        [CreateProperty]
        public object Value
        {
            get => m_Value;
            set
            {
                if (Equals(m_Value, value))
                    return;
                m_Value = value;
                SerializeValue();
                m_Persistor?.PersistWrites();
                propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Value)));
            }
        }

        public void Init(
            ScriptableObjectDataChangePersistor persistor,
            PlayModeControlsAttributeDefinition attributeDefinition,
            int userIndex
        )
        {
            if (!string.IsNullOrEmpty(m_AttributeDefinitionGuid))
                Assert.AreEqual(m_AttributeDefinitionGuid, attributeDefinition.Guid);

            AttributeDefinition?.ValueTypeChanged.RemoveListener(SetDefaultAttributeValue);

            AttributeDefinition = attributeDefinition;
            AttributeDefinition.ValueTypeChanged.AddWeakListener(SetDefaultAttributeValue);
            m_AttributeDefinitionGuid = attributeDefinition.Guid;

            m_Persistor = persistor;
            m_UserIndex = userIndex;
            DeserializeValue();
        }

        public void SetDefaultAttributeValue()
        {
            var displayIndex = m_UserIndex + 1;
            if (AttributeDefinition.ValueType == typeof(int))
                Value = displayIndex;
            else if (AttributeDefinition.ValueType == typeof(long))
                Value = (long)displayIndex;
            else if (AttributeDefinition.ValueType == typeof(string))
                Value = $"placeholder-{displayIndex}";
            else if (AttributeDefinition.ValueType == typeof(Texture2D))
                Value = PlayModeControlsAssetTracker.GetAttributeTextureByIndex(displayIndex);
            else
                throw new NotSupportedException(
                    $"AttributeDefinition.ValueType {AttributeDefinition.ValueType.FullName} is not implemented"
                );
        }

        private void SerializeValue()
        {
            if (AttributeDefinition.ValueType == typeof(string))
            {
                if (m_Value == null)
                {
                    m_SerializedValue = null;
                    return;
                }

                if (m_Value is not string stringValue)
                {
                    Debug.LogWarning(
                        $"Failed to serialize the value of attribute {AttributeDefinition.Name}. The Value type and the attribute definition ValueType do not match"
                    );
                    return;
                }
                m_SerializedValue = stringValue;
            }
            else if (AttributeDefinition.ValueType == typeof(int))
            {
                if (m_Value is not int intValue)
                {
                    Debug.LogWarning(
                        $"Failed to serialize the value of attribute {AttributeDefinition.Name}. The Value type and the attribute definition ValueType do not match"
                    );
                    return;
                }
                m_SerializedValue = intValue.ToString();
            }
            else if (AttributeDefinition.ValueType == typeof(long))
            {
                if (m_Value is not long)
                {
                    Debug.LogWarning(
                        $"Failed to serialize the value of attribute {AttributeDefinition.Name}. The Value type and the attribute definition ValueType do not match"
                    );
                    return;
                }
                m_SerializedValue = m_Value.ToString();
            }
            else if (AttributeDefinition.ValueType == typeof(Texture2D))
            {
                if (m_Value == null)
                {
                    m_SerializedValue = null;
                    return;
                }
                if (m_Value is not Texture2D textureValue)
                {
                    Debug.LogWarning(
                        $"Failed to serialize the value of attribute {AttributeDefinition.Name}. The Value type and the attribute definition ValueType do not match"
                    );
                    return;
                }
                var path = AssetDatabase.GetAssetPath(textureValue);
                m_SerializedValue = AssetDatabase.AssetPathToGUID(path);
            }
            else
            {
                throw new NotSupportedException(
                    $"AttributeDefinition.ValueType {AttributeDefinition.ValueType.FullName} is not implemented"
                );
            }
        }

        private void DeserializeValue()
        {
            if (AttributeDefinition == null)
                return;

            if (AttributeDefinition.ValueType == typeof(string))
            {
                Value = m_SerializedValue ?? string.Empty;
            }
            else if (AttributeDefinition.ValueType == typeof(int))
            {
                Value = int.TryParse(m_SerializedValue, out var intValue) ? intValue : 0;
            }
            else if (AttributeDefinition.ValueType == typeof(long))
            {
                Value = long.TryParse(m_SerializedValue, out var longValue) ? longValue : 0;
            }
            else if (AttributeDefinition.ValueType == typeof(Texture2D))
            {
                if (string.IsNullOrEmpty(m_SerializedValue))
                {
                    Value = null;
                    return;
                }
                var path = AssetDatabase.GUIDToAssetPath(m_SerializedValue);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"Unable to find texture for {AttributeDefinition.Name}");
                    Value = null;
                }
                else
                {
                    Value = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"AttributeDefinition.ValueType {AttributeDefinition.ValueType.FullName} is not implemented"
                );
            }
        }
    }
}
