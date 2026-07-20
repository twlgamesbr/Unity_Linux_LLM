using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unity.PlatformToolkit.Editor;
using Unity.Properties;
using UnityEngine;

namespace Unity.PlatformToolkit.PlayMode
{
    [Serializable]
    internal class PlayModeControlsAccountAttributeValues
    {
        private ScriptableObjectDataChangePersistor m_Persistor;
        private int m_UserIndex;

        [SerializeField]
        private List<PlayModeControlsAccountAttributeValue> m_AttributeValues = new();

        [CreateProperty]
        public IReadOnlyList<PlayModeControlsAccountAttributeValue> AttributeValues => m_AttributeValues;

        private ObservableSerializableList<PlayModeControlsAttributeDefinition> m_SubscribedAttributeDefinitions;

        public void Init(
            ScriptableObjectDataChangePersistor persistor,
            ObservableSerializableList<PlayModeControlsAttributeDefinition> attributeDefinitions,
            int userIndex
        )
        {
            m_Persistor = persistor;
            m_UserIndex = userIndex;

            var guids = attributeDefinitions.Select(a => a.Guid).ToHashSet();
            m_AttributeValues.RemoveAll(av => !guids.Contains(av.AttributeDefinitionGuid));

            foreach (var definition in attributeDefinitions)
            {
                var attributeValue = m_AttributeValues.FirstOrDefault(av =>
                    av.AttributeDefinitionGuid == definition.Guid
                );
                if (attributeValue != null)
                    attributeValue.Init(m_Persistor, definition, m_UserIndex);
                else
                    AddAttributeValue(definition);
            }

            // Quick fix as AccountData (which holds this object) does not dispose. Ideally this logic should be in a ViewModel
            // Attribute Definitions should never change therefore we can keep the same subscription.
            if (m_SubscribedAttributeDefinitions == attributeDefinitions)
                return;
            if (m_SubscribedAttributeDefinitions != null)
                m_SubscribedAttributeDefinitions.CollectionChanged -= OnAttributeDefinitionsListChanged;

            m_SubscribedAttributeDefinitions = attributeDefinitions;
            m_SubscribedAttributeDefinitions.CollectionChanged += OnAttributeDefinitionsListChanged;
        }

        public bool TryGetAttributeValue<T>(string attributeName, out T value)
        {
            var attributeValue = m_AttributeValues.FirstOrDefault(av => av.AttributeDefinition.Name == attributeName);
            if (attributeValue?.AttributeDefinition.ValueType == typeof(T) && attributeValue.Value is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        private void OnAttributeDefinitionsListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var addedAttributeDefinition = e.NewItems[0] as PlayModeControlsAttributeDefinition;
                    AddAttributeValue(addedAttributeDefinition, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    var removedAttributeDefinition = e.OldItems[0] as PlayModeControlsAttributeDefinition;
                    RemoveAttributeValue(removedAttributeDefinition);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    var oldAttributeDefinition = e.OldItems[0] as PlayModeControlsAttributeDefinition;
                    var attributeValue = m_AttributeValues.First(av =>
                        av.AttributeDefinition == oldAttributeDefinition
                    );
                    m_AttributeValues.Remove(attributeValue);
                    AddAttributeValue(e.NewItems[0] as PlayModeControlsAttributeDefinition, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    m_AttributeValues.Clear();
                    m_Persistor?.PersistWrites();
                    break;
            }
        }

        private void AddAttributeValue(PlayModeControlsAttributeDefinition attributeDefinition, int index = -1)
        {
            var attributeValue = new PlayModeControlsAccountAttributeValue();
            attributeValue.Init(m_Persistor, attributeDefinition, m_UserIndex);
            attributeValue.SetDefaultAttributeValue();

            if (index > -1 && index < m_AttributeValues.Count)
                m_AttributeValues.Insert(index, attributeValue);
            else
                m_AttributeValues.Add(attributeValue);
            m_Persistor?.PersistWrites();
        }

        private void RemoveAttributeValue(PlayModeControlsAttributeDefinition attributeDefinition)
        {
            var attributeValue = m_AttributeValues.FirstOrDefault(av =>
                av.AttributeDefinition.Guid == attributeDefinition.Guid
            );
            if (attributeValue == null)
                return;
            m_AttributeValues.Remove(attributeValue);
            m_Persistor?.PersistWrites();
        }
    }
}
