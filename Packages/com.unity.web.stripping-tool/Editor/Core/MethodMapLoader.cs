// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System.Collections.Generic;
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// An entry in a method map with assembly name, method name and native function name.
    /// </summary>
    class MethodMapEntry
    {
        public string Assembly { get; set; } = "";
        public string MethodName { get; set; } = "";
        public string NativeFunctionName { get; set; } = "";
    }

    /// <summary>
    /// A method map to lookup the native function name of a C# method.
    /// </summary>
    class MethodMap
    {
        /// <summary>
        /// Look up method by assembly name and then method name.
        /// </summary>
        public Dictionary<string, Dictionary<string, List<MethodMapEntry>>> MethodsByAssembly = new();
        /// <summary>
        /// Look up method by method name.
        /// </summary>
        public Dictionary<string, List<MethodMapEntry>> Methods = new();
    }

    /// <summary>
    /// Class for loading a MethodMap.tsv file.
    /// </summary>
    class MethodMapLoader
    {
        private MethodMap m_MethodMap = new MethodMap();

        /// <summary>
        /// Loads a IL2CPP method map from a MethodMap.tsv file.
        /// </summary>
        /// <param name="path">The path to the MethodMap.tsv file</param>
        /// <returns>The loaded method map.</returns>
        public static MethodMap Load(string path)
        {
            var methodMapLoader = new MethodMapLoader();

            return methodMapLoader.LoadInternal(path);
        }

        private MethodMap LoadInternal(string path)
        {
            var lines = File.ReadLines(path);

            foreach (var line in lines)
            {
                var components = line.Split("\t");
                var entry = new MethodMapEntry()
                {
                    Assembly = components[2],
                    MethodName = components[1],
                    NativeFunctionName = components[0]
                };

                // Skip methods without native function name
                if (entry.NativeFunctionName == "NULL")
                    continue;

                // Add method to method list and assembly dictionary
                InsertMethod(m_MethodMap.Methods, entry);
                InsertMethod(GetOrCreateAssembly(entry.Assembly), entry);
            }

            return m_MethodMap;
        }

        private Dictionary<string, List<MethodMapEntry>> GetOrCreateAssembly(string assembly)
        {
            if (!m_MethodMap.MethodsByAssembly.TryGetValue(assembly, out var methodDictionary))
            {
                // Insert new method dictionary for assembly
                methodDictionary = new();
                m_MethodMap.MethodsByAssembly[assembly] = methodDictionary;
            }

            return methodDictionary;
        }

        private void InsertMethod(Dictionary<string, List<MethodMapEntry>> methodDictionary, MethodMapEntry entry)
        {
            List<MethodMapEntry>? methodList;
            if (!methodDictionary.TryGetValue(entry.MethodName, out methodList))
            {
                methodList = new List<MethodMapEntry>();
                methodDictionary[entry.MethodName] = methodList;
            }

            methodList.Add(entry);
        }
    }
}
