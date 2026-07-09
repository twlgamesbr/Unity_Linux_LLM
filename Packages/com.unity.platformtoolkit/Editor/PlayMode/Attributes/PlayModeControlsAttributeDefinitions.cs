using System;
using Unity.PlatformToolkit.Editor;
using UnityEngine;

namespace Unity.PlatformToolkit.PlayMode
{
    [Serializable]
    internal class PlayModeControlsAttributeDefinitions
    {
        private ScriptableObjectDataChangePersistor m_Persistor;

        public ScriptableObjectDataChangePersistor Persistor
        {
            private get => m_Persistor;
            set
            {
                m_Persistor = value;
                foreach (var def in Definitions)
                {
                    def.Persistor = m_Persistor;
                }
            }
        }

        [SerializeField]
        private ObservableSerializableList<PlayModeControlsAttributeDefinition> m_Definitions = new ();
        public ObservableSerializableList<PlayModeControlsAttributeDefinition> Definitions => m_Definitions;

        public void CreateDefinition()
        {
            var ad = new PlayModeControlsAttributeDefinition { Persistor = Persistor };
            Definitions.Add(ad);
            Persistor?.PersistWrites();
        }

        public void RemoveDefinition(int index)
        {
            Definitions.RemoveAt(index);
            Persistor?.PersistWrites();
        }
    }
}
