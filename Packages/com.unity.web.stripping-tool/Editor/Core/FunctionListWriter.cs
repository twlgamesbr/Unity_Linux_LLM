// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Class <b>FunctionListWriter</b> creates a file that contains all ids
    /// of functions that should be removed from a wasm file.
    /// </summary>
    class FunctionListWriter
    {
        /// <summary>
        /// The submodule loaded definition file.
        /// </summary>
        public SubmoduleConfig SubmoduleConfig { get; set; } = new();

        /// <summary>
        /// The loaded symbol file. (Optional)
        /// </summary>
        public Dictionary<string, long>? SymbolFile { get; set; }

        /// <summary>
        /// An IL2CPP method map. (Optional)
        /// Stripping of C# code is skipped if method map is not configured.
        /// </summary>
        public MethodMap? MethodMap { get; set; }

        /// <summary>
        /// A list of submodules to strip.
        /// </summary>
        public List<string> SubmodulesToStrip { get; set; } = new();

        /// <summary>
        /// Enables verbose logging.
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Log output. Default: System.Console.Out
        /// </summary>
        public TextWriter Log = Console.Out;

        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        /// <summary>
        /// Write a function id list to the given path.
        /// </summary>
        /// <param name="path">Output path for the function id list.</param>
        /// <returns>Returns true if function id file is not empty.</returns>
        public bool Write(string path)
        {
            SubmodulesToStrip = new(SubmoduleListUtils.ExpandNestedSubmodules(SubmodulesToStrip, SubmoduleConfig, ErrorLog));
            var functionIds = GetFunctionIdList();

            if (Verbose)
                Log.WriteLine($"Functions to strip: {String.Join("\n", functionIds)}");

            // Write coverage as binary file
            File.WriteAllText(path, String.Join("\n", functionIds));
            return functionIds.Count > 0;
        }

        private HashSet<string> GetFunctionIdList()
        {
            var functionIds = new HashSet<string>();

            foreach (var submodule in SubmodulesToStrip)
            {
                var submoduleDefinition = SubmoduleConfig.submodules.Find(x => x.name == submodule);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                // We know that submoduleDefinition != null here because
                // we filtered the list for unknown submodules in ExpandNestedSubmodules()
                AddFunctionIdsToSet(submoduleDefinition.functions, functionIds);

                // Evaluate C# method filter
                var csharpFunctions = GetCSharpFunctionNames(submoduleDefinition.csharpMethodFilters);
                AddFunctionIdsToSet(csharpFunctions, functionIds);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            return functionIds;
        }

        private void AddFunctionIdsToSet(IEnumerable<string> functions, HashSet<string> outFunctionIds)
        {
            foreach (var functionName in functions)
            {
                var functionId = GetFunctionId(functionName);

                if (functionId != "-1")
                {
                    outFunctionIds.Add(functionId);
                }
            }
        }

        private string GetFunctionId(string functionName)
        {
            // Use function names if symbol file is not used
            if (SymbolFile == null)
            {
                return EscapeFunctionName(functionName);
            }

            // Find function id for function name in symbol file
            long functionId = -1;
            if (!SymbolFile.TryGetValue(functionName, out functionId))
            {
                ErrorLog.WriteLine($"Warning: function '{functionName}' not found in symbol file.");
            }

            return functionId.ToString();
        }

        /// <summary>
        /// Escape functions names as they appear in WASM file.
        /// The following reserved characters are escaped in the WASM file:
        /// - '(' -> "\28"
        /// - ')' -> "\29"
        /// - ' ' -> "\20"
        /// - ',' -> "\2c"
        /// </summary>
        /// <param name="functionName">A C/C++ function name.</param>
        /// <returns>The function name with special characters escaped.</returns>
        public static string EscapeFunctionName(string functionName)
        {
            return functionName
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace(" ", "\\20")
                .Replace(",", "\\2c");
        }

        private HashSet<string> GetCSharpFunctionNames(CSharpSubmoduleDefinition? cSharpSubmoduleDefinition)
        {
            if (MethodMap == null || cSharpSubmoduleDefinition == null)
                return new HashSet<string>();

            return FilterEvaluator.FindMethods(MethodMap, cSharpSubmoduleDefinition);
        }
    }
}
