// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Class <b>InstrumentationConfigWriter</b> creates a file that configures
    /// the instrumentation of a .wasm file for finding unused submodules.
    /// </summary>
    class InstrumentationConfigWriter
    {
        /// <summary>
        /// The loaded submodule definition file.
        /// </summary>
        public SubmoduleConfig SubmoduleConfig { get; set; } = new();

        /// <summary>
        /// Configures which submodules to profile. If this is set to null all available submodules are profiled.
        /// Otherwise only the submodules defined in this set.
        /// </summary>
        public HashSet<string>? SubmodulesToInstrument { get; set; } = null;

        /// <summary>
        /// The loaded symbol file. (Optional)
        /// It maps the function name to the index of the function within the WebAssembly file.
        /// </summary>
        public Dictionary<string, long>? SymbolFile { get; set; }

        /// <summary>
        /// A map from index of function to numerical id within a WebAssembly file.
        /// WebAssembly with external debug symbols uses numerical names for functions that do not match the index.
        /// We need that info from the original WebAssembly file to correctly identify functions within a stripped WebAssembly.
        /// </summary>
        public Dictionary<long, string> FunctionMap { get; set; } = new();

        /// <summary>
        /// An IL2CPP method map. (Optional)
        /// Stripping of C# code is skipped if method map is not configured.
        /// </summary>
        public MethodMap? MethodMap { get; set; }

        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        private Dictionary<string, HashSet<string>> m_FunctionSubmodulesMap = new();

        public void Write(string path)
        {
            var config = GetInstrumentationConfig();

            // Write config file
            File.WriteAllText(path, String.Join("\n", config));
        }

        private List<string> GetInstrumentationConfig()
        {
            m_FunctionSubmodulesMap.Clear();
            var config = new List<string>();
            var expandedSubmodules = SubmodulesToInstrument != null
                ? SubmoduleListUtils.ExpandNestedSubmodules(SubmodulesToInstrument, SubmoduleConfig, ErrorLog)
                : null;

            foreach (var submodule in SubmoduleConfig.submodules)
            {
                // Skip submodules if SubmodulesToInstrument is set
                if (expandedSubmodules != null && !expandedSubmodules.Contains(submodule.name))
                    continue;

                // Add native functions
                if (submodule.functions.Count > 0)
                {
                    foreach (var function in submodule.functions)
                    {
                        AddFunction(function, submodule.name);
                    }
                }

                // Add C# methods
                if (MethodMap != null && submodule.csharpMethodFilters != null)
                {
                    var cSharpFunctions = FilterEvaluator.FindMethods(MethodMap, submodule.csharpMethodFilters);
                    foreach (var function in cSharpFunctions)
                    {
                        AddFunction(function, submodule.name);
                    }
                }
            }

            // Convert map of functions to config format
            foreach (var entry in m_FunctionSubmodulesMap)
            {
                AddFunctionToConfig(config, entry.Key, string.Join(",", entry.Value));
            }

            return config;
        }

        private void AddFunction(string function, string submoduleName)
        {
            if (m_FunctionSubmodulesMap.TryGetValue(function, out var submodules))
            {
                submodules.Add(submoduleName);
            }
            else
            {
                m_FunctionSubmodulesMap[function] = new HashSet<string> { submoduleName };
            }
        }

        private void AddFunctionToConfig(List<string> config, string function, string submoduleName)
        {
            if (SymbolFile == null)
            {
                config.Add($"{FunctionListWriter.EscapeFunctionName(function)};{submoduleName}");
            }
            else if (
                SymbolFile.TryGetValue(function, out var functionIndex) &&
                FunctionMap.TryGetValue(functionIndex, out var functionId)
            )
            {
                // Translate the index of the function to the internal numerical function name
                config.Add($"{functionId};{submoduleName}");
            } else {
                ErrorLog.WriteLine($"Could not find function id for {function}");
            }
        }
    }
}
