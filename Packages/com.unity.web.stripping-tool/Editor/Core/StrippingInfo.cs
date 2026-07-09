// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Newtonsoft.Json;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Stripping meta data used for writing a JSON file.
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class StrippingInfo
    {
        /// <summary>
        /// Serialize stripping info to json.
        /// </summary>
        /// <returns></returns>
        public string ToJson() => JsonConvert.SerializeObject(this);

        /// <summary>
        /// Name of the tool
        /// </summary>
        public string toolName { get; set; } = "";

        /// <summary>
        /// Current Unity version number (as a way to distinguish what version of this tool is applied).
        /// </summary>
        public string version { get; set; } = "";

        /// <summary>
        /// A list of submodules that should be stripped from the build.
        /// </summary>
        public string[] submodulesToStrip { get; set; } = new string[] { };

        /// <summary>
        /// Number of stripped submodules
        /// </summary>
        public int numberOfSubmodules { get; set; } = 0;

        /// <summary>
        /// A list of submodules that were stripped from the build. This
        /// could be different from <see cref="submodulesToStrip"/> because the compiler
        /// may have already optimized out all functions of a submodule.
        /// </summary>
        public string[] strippedSubmodules { get; set; } = new string[] { };

        /// <summary>
        /// The total size of stripped subcomponents in bytes.
        /// </summary>
        public long strippedSize { get; set; } = 0;

        /// <summary>
        /// Whether code optimization was run after stripping.
        /// </summary>
        public bool? optimizeCodeAfterStripping { get; set; } = null;

        /// <summary>
        /// Whether debug information was removed after stripping.
        /// </summary>
        public bool? removeDebugInformation { get; set; } = null;

        /// <summary>
        /// The error handling behavior when a stripped submodule is used.
        /// </summary>
        public string? missingSubmoduleErrorHandling { get; set; } = null;

        /// <summary>
        /// Optional: Size reduction of code section.
        /// </summary>
        public long? strippedCodeSize  { get; set; } = null;

        /// <summary>
        /// Optional: Size reduction of custom "name" section. Used by Emscripten to store function names.
        /// </summary>
        public long? strippedNameSize  { get; set; } = null;

        /// <summary>
        /// Optional: Size reduction of custom section with DWARF debug information.
        /// This includes the sections .debug_loc, .debug_line, .debug_ranges, .debug_str, .debug_abbrev, .debug_info
        /// </summary>
        public long? strippedDwarfSize  { get; set; } = null;
    }

    /// <summary>
    /// Creates meta data of the stripping process and writes it to a file.
    /// </summary>
    class StrippingInfoWriter
    {
        /// <summary>
        /// The path to the input WASM file.
        /// </summary>
        public string WasmFile = "";

        /// <summary>
        /// The path to the output WASM file.
        /// </summary>
        public string OutputWasmFile = "";

        /// <summary>
        /// A list of submodules configured to be stripped.
        /// </summary>
        public List<string> SubmodulesToStrip = new();

        /// <summary>
        /// The submodule definition file.
        /// </summary>
        public SubmoduleConfig SubmoduleConfig = new();

        /// <summary>
        /// The expanded list of submodules to strip.
        /// </summary>
        public List<string> ExpandedSubmodulesToStrip = new();

        /// <summary>
        /// Set of functions stripped by wasm-opt.
        /// </summary>
        public HashSet<string> StrippedFunctions = new();

        /// <summary>
        /// The symbol file to map function ids to function names
        /// </summary>
        public Dictionary<string, long>? SymbolFile = null;

        /// <summary>
        /// The IL2CPP method map to map C# names to native function names.
        /// </summary>
        public MethodMap? MethodMap = null;

        /// <summary>
        /// Name of the stripping tool.
        /// </summary>
        public string ToolName = "Unity.Web.Stripping.Editor";

        /// <summary>
        /// Product version to be written into the stripping info file.
        /// </summary>
        public string ProductVersion = "1.0.0";

        /// <summary>
        /// Whether code optimization was run after stripping.
        /// </summary>
        public bool OptimizeCodeAfterStripping = false;

        /// <summary>
        /// Whether debug information was removed after stripping.
        /// </summary>
        public bool RemoveDebugInformation = false;

        /// <summary>
        /// The error handling behavior when a stripped submodule is used.
        /// </summary>
        public string MissingSubmoduleErrorHandling = "Ignore";

        /// <summary>
        /// Optional: The section size information of the original WebAssembly file.
        /// </summary>
        public WasmSectionSizes? OriginalWasmFileSectionSizeInformation = null;

        /// <summary>
        /// Optional: The section size information of the stripped WebAssembly file.
        /// </summary>
        public WasmSectionSizes? WasmFileSectionSizeInformation = null;


        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        /// <summary>
        /// Writes a meta data file to the given path.
        /// </summary>
        /// <param name="path">Output path of the meta data file.</param>
        public void Write(string path)
        {
            // Measure size difference
            var originalFileSize = new FileInfo(WasmFile).Length;
            var strippedFileSize = new FileInfo(OutputWasmFile).Length;

            // Determine list of submodules that were actually stripped.
            var strippedSubmodules = GetStrippedSubmodules();

            // Write meta data as JSON file
            var strippingInfo = new StrippingInfo()
            {
                toolName = ToolName,
                version = ProductVersion,
                submodulesToStrip = SubmodulesToStrip.ToArray(),
                numberOfSubmodules = strippedSubmodules.Length,
                strippedSubmodules = strippedSubmodules,
                strippedSize = (originalFileSize - strippedFileSize),
                optimizeCodeAfterStripping = OptimizeCodeAfterStripping,
                removeDebugInformation = RemoveDebugInformation,
                missingSubmoduleErrorHandling = MissingSubmoduleErrorHandling
            };

            // Add information on the individual sections
            if (OriginalWasmFileSectionSizeInformation != null && WasmFileSectionSizeInformation != null)
            {
                strippingInfo.strippedCodeSize = OriginalWasmFileSectionSizeInformation.Code - WasmFileSectionSizeInformation.Code;
                strippingInfo.strippedNameSize = OriginalWasmFileSectionSizeInformation.Name - WasmFileSectionSizeInformation.Name;
                strippingInfo.strippedDwarfSize = OriginalWasmFileSectionSizeInformation.Dwarf - WasmFileSectionSizeInformation.Dwarf;
            }
            File.WriteAllText(path, strippingInfo.ToJson());
        }

        /// <summary>
        /// Gets a list of submodules that were actually stripped from a WASM file.
        /// A submodule is considered as stripped if at least one function of it was stripped.
        /// </summary>
        /// <returns>A list of stripped submodules.</returns>
        private string[] GetStrippedSubmodules()
        {
            var strippedSubmodules = new HashSet<string>();
            var strippedFunctions = MapStrippedFunctions(StrippedFunctions);
            // UnityEngine.Debug.Log(String.Join(", ", strippedFunctions.ToArray()));

            foreach (var submodule in ExpandedSubmodulesToStrip)
            {
                var submoduleDefinition = SubmoduleConfig.submodules.Find(x => x.name == submodule);
                bool submoduleWasStripped = false;

                if (submoduleDefinition == null)
                {
                    ErrorLog.WriteLine($"Warning: submodule '{submodule}' not found in submodule definition file.");
                    continue;
                }

                // Check if at least one function of the submodule was stripped
                foreach (var function in submoduleDefinition.functions)
                {
                    if (strippedFunctions.Contains(function))
                    {
                        submoduleWasStripped = true;
                        break;
                    }
                }

                // Check if at lease one C# method was stripped
                // Skip this check if submodule was already found to be stripped
                if (!submoduleWasStripped && MethodMap != null && submoduleDefinition.csharpMethodFilters != null)
                {
                    var csharpFunctions = FilterEvaluator.FindMethods(MethodMap, submoduleDefinition.csharpMethodFilters);
                    foreach (var function in csharpFunctions)
                    {
                        if (strippedFunctions.Contains(function))
                        {
                            submoduleWasStripped = true;
                            break;
                        }
                    }
                }


                if (submoduleWasStripped)
                {
                    strippedSubmodules.Add(submodule);
                }
            }

            string[] strippedSubmodulesArray = new string[strippedSubmodules.Count];
            strippedSubmodules.CopyTo(strippedSubmodulesArray);

            return strippedSubmodulesArray;
        }

        /// <summary>
        /// If the stripping process uses external debug symbols the list of stripped functions
        /// uses function ids instead of names. This functions translates all ids to names.
        /// </summary>
        /// <param name="strippedFunctions">A list of stripped functions.</param>
        /// <returns>A list of stripped functions with their name.</returns>
        private HashSet<string> MapStrippedFunctions(HashSet<string> strippedFunctions)
        {
            if (SymbolFile == null)
                return strippedFunctions;

            var newStrippedFunctions = new HashSet<string>();

            foreach (var functionId in strippedFunctions)
            {
                newStrippedFunctions.Add(MapFunctionIdToName(SymbolFile, functionId));
            }

            return newStrippedFunctions;
        }

        /// <summary>
        /// Map a function id to it's name using a symbol file dictionary.
        /// </summary>
        /// <param name="symbolFile">The function name to id dictionary.</param>
        /// <param name="functionId">The id of the function.</param>
        /// <returns>Either the mapped function name or the original function id if it is not found.</returns>
        private static string MapFunctionIdToName(Dictionary<string, long> symbolFile, string functionId)
        {
            foreach (var function in symbolFile)
            {
                if (function.Value.ToString() == functionId)
                    return function.Key;
            }

            return functionId;
        }
    }
}
