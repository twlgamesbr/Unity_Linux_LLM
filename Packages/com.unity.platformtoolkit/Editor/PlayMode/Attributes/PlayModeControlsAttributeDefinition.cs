using System;
using System.Linq;
using Unity.PlatformToolkit.Editor;
using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Unity.PlatformToolkit.PlayMode
{
    [Serializable]
    internal class PlayModeControlsAttributeDefinition
    {
        public static readonly Type[] k_SupportedValueTypes = { typeof(string), typeof(int),  typeof(long), typeof(Texture2D) };

        public WeakEvent ValueTypeChanged { get; } = new ();

        public ScriptableObjectDataChangePersistor Persistor { private get; set; }

        [SerializeField]
        private string m_Guid = System.Guid.NewGuid().ToString();
        public string Guid
        {
            get => m_Guid;
            set
            {
                if (m_Guid != null) return;
                m_Guid = value;
            }
        }

        [SerializeField]
        private string m_Name;

        [CreateProperty]
        public string Name
        {
            get => m_Name;
            set
            {
                if (m_Name == value) return;
                m_Name = value;
                Persistor?.PersistWrites();
            }
        }

        [SerializeField]
        private string m_ValueTypeName = k_SupportedValueTypes[0].FullName;
        private Type m_ValueType;
        [CreateProperty]
        public Type ValueType
        {
            get
            {
                if (m_ValueType != null)
                    return m_ValueType;

                m_ValueType = GetTypeFromFullName(m_ValueTypeName);
                return m_ValueType;
            }

            set
            {
                if (m_ValueType == value) return;
                if (!k_SupportedValueTypes.Contains(value))
                    throw new InvalidOperationException($"{value} is not a valid type for {nameof(PlayModeControlsAttributeDefinition)}");
                m_ValueType = value;
                m_ValueTypeName = m_ValueType.FullName;
                Persistor?.PersistWrites();
                ValueTypeChanged?.Invoke();
            }
        }

        private Type GetTypeFromFullName(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            #if UNITY_6000_4_OR_NEWER
                var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
            #else
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            #endif

            return assemblies
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(t => t != null);
        }
    }
}
