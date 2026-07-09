// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A class for instrumenting Unity Web builds to profile for used/unused submodules.
    /// </summary>
    class SubmoduleProfiler
    {
        /// <summary>
        /// Exception that is thrown when "wasmImports" is not found inside JavaScript framework file.
        /// </summary>
        public class WasmImportNotFoundException : System.Exception
        {
            public string FrameworkFile { get; internal set; }

            public WasmImportNotFoundException(string frameworkFile)
                : base($"Unable to find definition of wasmImports in JavaScript framework file: {frameworkFile}")
            {
                FrameworkFile = frameworkFile;
            }
        }

        /// <summary>
        /// Exception that is thrown when a build already has submodule profiling code.
        /// </summary>
        public class BuildAlreadyInstrumented : System.Exception
        {
            public string FrameworkFile { get; internal set; }

            public BuildAlreadyInstrumented(string frameworkFile)
                : base($"The build appears to be already instrumented for profiling. JavaScript framework file: {frameworkFile}")
            {
                FrameworkFile = frameworkFile;
            }
        }

        /// <summary>
        /// Paths to all submodule definitions files
        /// </summary>
        public List<string> SubmoduleDefinitionFiles { get; set; } = new();
        /// <summary>
        /// Path to a symbol file if the build uses external debug symbols.
        /// </summary>
        public string SymbolFile { get; set; } = "";

        /// <summary>
        /// If specified, the method map is used for C# submodule stripping.
        /// </summary>
        public string MethodMapFile { get; set; } = "";

        /// <summary>
        /// A map from index of function to numerical id within a WebAssembly file. (Optional)
        /// WebAssembly with external debug symbols uses numerical names for functions that do not match the index.
        /// We need that info from the original WebAssembly file to correctly identify functions within a stripped WebAssembly.
        /// </summary>
        public Dictionary<long, string>? FunctionMap { get; set; } = null;

        /// <summary>
        /// The Unity version of the build to strip. If set this will filter the available submodules.
        /// </summary>
        public string? UnityVersion { get; set; } = null;

        /// <summary>
        /// Configures which submodules to profile. If this is set to null all available submodules are profiled.
        /// Otherwise only the submodules defined in this set.
        /// </summary>
        public HashSet<string>? SubmodulesToInstrument { get; set; } = null;

        /// <summary>
        /// Path to emscripten. This needs to be a version that supports the --instrument-functions pass.
        /// </summary>
        public string EmscriptenSdkPath { get; set; } = "";

        /// <summary>
        /// Path to Brotli
        /// </summary>
        public string BrotliPath { get; set; } = "";

        /// <summary>
        /// Path to 7zip.
        /// </summary>
        public string SevenZipPath { get; set; } = "";

        /// <summary>
        /// Enable verbose logging
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Emit WebAssembly text format.
        /// </summary>
        public bool EmitWat { get; set; } = false;

        /// <summary>
        /// Don't remove debug information when build is instrumented.
        /// </summary>
        public bool KeepDebugInformation { get; set; } = true;

        /// <summary>
        /// Enable if build was created with Emscripten 4 features.
        /// Emscripten 4 with WebAssembly 2023 enabled will use the WASM features:
        /// * bulk-memory
        /// * bulk-memory-opt
        /// * nontrapping-float-to-int-conversions
        /// </summary>
        public bool EnableEmscripten4Features { get; set; } = false;

        /// <summary>
        /// If the build was created as a development build (`UnityEditor.EditorUserBuildSettings.development`).
        /// </summary>
        public bool Development { get; set; } = false;

        /// <summary>
        /// The code optimization setting used for the build.
        /// </summary>
        public string CodeOptimization { get; set; } = "";

        /// <summary>
        /// Log function code replacement for un-minified code.
        /// </summary>
        public string LogFunctionCode { get; set; } = "var wasmImports = {\n  \"log_execution\": function(funcId, labelId) { },";

        /// <summary>
        /// Log function code replacement for minified code.
        /// </summary>
        public string MinifiedLogFunctionCode { get; set; } = "var wasmImports={\"log_execution\":function(funcId, labelId){},";

        /// <summary>
        /// Log output. Default: System.Console.Out
        /// </summary>
        public TextWriter Log = Console.Out;

        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        private string m_TempInstrumentationConfigFile = Path.Combine(Path.GetTempPath(), $"tmp_config_{Path.GetRandomFileName()}.txt");
        private string m_TempUnpackedWasmFile = Path.Combine(Path.GetTempPath(), $"tmp_{Path.GetRandomFileName()}.wasm");
        private string m_TempInstrumentedWasmFile = Path.Combine(Path.GetTempPath(), $"tmp_{Path.GetRandomFileName()}_instrumented.wasm");
        private string m_TempUnpackedSymbolFile = Path.Combine(Path.GetTempPath(), $"tmp_{Path.GetRandomFileName()}.symbols.json");
        private string m_TempUnpackedFrameworkFile = Path.Combine(Path.GetTempPath(), $"tmp_{Path.GetRandomFileName()}.framework.js");
        private string m_TempPatchedFrameworkFile = Path.Combine(Path.GetTempPath(), $"tmp_{Path.GetRandomFileName()}.framework.js");
        private string m_WasmImportsModule = "env";
        private Compression? m_Compression = null;

        // Regex and replacements to modify framework file
        private static readonly Regex WasmImportModuleRegex = new ("[\"']?([\\w\\d_-]+)[\"']?:\\s*wasmImports");
        private static readonly Regex WasmImportDefinitionRegex = new (@"(var )?wasmImports\s*=\s*\{");

        /// <summary>
        /// Adds instrumentation code to a WebAssembly file to profile for used/unused submodules.
        /// </summary>
        /// <param name="wasmFile">The input WASM file to instrument.</param>
        /// <param name="outputWasmFile">The output WASM file with submodule profiling code.</param>
        /// <param name="frameworkFile">The input JavaScript framework file.</param>
        /// <param name="outputFrameworkFile">The output JavaScript framework file with submodule profiling code.</param>
        /// <param name="instrumentationDataOutputPath">The output path for generated functions.json and labels.json file.</param>
        public void InstrumentBuild(string wasmFile, string outputWasmFile, string frameworkFile, string outputFrameworkFile, string instrumentationDataOutputPath)
        {
            try
            {
                InitCompression();

                // Load all input files
                WriteLog($"Loading submodule files: {string.Join(", ", SubmoduleDefinitionFiles)}");
                var submoduleDefinition = SubmoduleDefinitionLoader.Load(SubmoduleDefinitionFiles, UnityVersion);
                MethodMap? methodMap = null;
                if (!string.IsNullOrEmpty(MethodMapFile))
                {
                    WriteLog($"Loading method map: {MethodMapFile}");
                    methodMap = MethodMapLoader.Load(MethodMapFile);
                }

                var symbolFile = LoadSymbolFile(SymbolFile);

                // Instrument framework
                AddSubmoduleProfilingCodeToFramework(frameworkFile, outputFrameworkFile);

                // Instrument WebAssembly
                InstrumentWebAssembly(wasmFile, outputWasmFile, instrumentationDataOutputPath, submoduleDefinition, methodMap, symbolFile);
            }
            finally
            {
                if (File.Exists(m_TempInstrumentationConfigFile))
                {
                    File.Delete(m_TempInstrumentationConfigFile);
                }
            }
        }

        private void InitCompression()
        {
            m_Compression = new Compression()
            {
                SevenZipPath = SevenZipPath,
                BrotliPath = BrotliPath,
                Log = (text) => Log.WriteLine(text),
                ErrorLog = (text) => ErrorLog.WriteLine(text)
            };
        }

        private Dictionary<string, long>? LoadSymbolFile(string symbolFilePath)
        {
            if (string.IsNullOrEmpty(symbolFilePath))
                return null;

            try
            {
                // Check if symbol file needs to be unpacked
                if (Compression.IsGzipCompressed(symbolFilePath))
                {
                    m_Compression?.DecompressGzip(symbolFilePath, m_TempUnpackedSymbolFile);
                    symbolFilePath = m_TempUnpackedSymbolFile;
                }
                else if (Compression.IsBrotliCompressed(symbolFilePath))
                {
                    m_Compression?.DecompressBrotli(symbolFilePath, m_TempUnpackedSymbolFile);
                    symbolFilePath = m_TempUnpackedSymbolFile;
                }

                WriteLog($"Loading symbol file: {symbolFilePath}");
                var symbolFile = SymbolFileLoader.Load(symbolFilePath);

                return symbolFile;
            }
            finally
            {
                if (File.Exists(m_TempUnpackedSymbolFile))
                    File.Delete(m_TempUnpackedSymbolFile);
            }
        }

        private void AddSubmoduleProfilingCodeToFramework(string frameworkFile, string outputFrameworkFile)
        {
            try
            {
                WriteLog($"Instrument JavaScript framework: \"{frameworkFile}\" -> \"{outputFrameworkFile}\"");

                // Uncompress and load framework file
                var compressionType = Compression.GetCompressionType(frameworkFile);
                if (compressionType == CompressionType.Gzip)
                {
                    m_Compression?.DecompressGzip(frameworkFile, m_TempUnpackedFrameworkFile);
                    frameworkFile = m_TempUnpackedFrameworkFile;
                }
                else if (compressionType == CompressionType.Brotli)
                {
                    m_Compression?.DecompressBrotli(frameworkFile, m_TempUnpackedFrameworkFile);
                    frameworkFile = m_TempUnpackedFrameworkFile;
                }
                var frameWorkContent = File.ReadAllText(frameworkFile);

                // Simple sanity check if framework already contains instrumentation code
                if (frameWorkContent.Contains("log_execution"))
                    throw new BuildAlreadyInstrumented(frameworkFile);

                // Search for name of wasm imports module inside WebAssembly file
                m_WasmImportsModule = FindWasmImportsName(frameworkFile, frameWorkContent);

                // Search for definition of wasmImports and inject log_execution function
                frameWorkContent = WasmImportDefinitionRegex.Replace(frameWorkContent, (match) =>
                {
                    // Emscripten 4 builds assign wasmImport later in certain configurations. Drop the "var " in this case.
                    bool usesVar = match.Value.StartsWith("var wasmImports");
                    bool useMinifiedCode = match.Value != "var wasmImports = {" && match.Value != "wasmImports = {";
                    if (usesVar)
                        return useMinifiedCode ? MinifiedLogFunctionCode : LogFunctionCode;
                    else
                        return useMinifiedCode ? MinifiedLogFunctionCode.Replace("var wasmImports", "wasmImports") : LogFunctionCode.Replace("var wasmImports", "wasmImports");
                });

                // Write patched framework file and compress if necessary
                switch (compressionType)
                {
                    case CompressionType.Disabled:
                        File.WriteAllText(outputFrameworkFile, frameWorkContent);
                        break;
                    case CompressionType.Gzip:
                        File.WriteAllText(m_TempPatchedFrameworkFile, frameWorkContent);
                        m_Compression?.CompressGzip(m_TempPatchedFrameworkFile, outputFrameworkFile);
                        break;
                    case CompressionType.Brotli:
                        File.WriteAllText(m_TempPatchedFrameworkFile, frameWorkContent);
                        m_Compression?.CompressBrotli(m_TempPatchedFrameworkFile, outputFrameworkFile);
                        break;
                }
            }
            finally
            {
                // Cleanup temporary files
                if (File.Exists(m_TempUnpackedFrameworkFile))
                    File.Delete(m_TempUnpackedFrameworkFile);
                if (File.Exists(m_TempPatchedFrameworkFile))
                    File.Delete(m_TempPatchedFrameworkFile);
            }
        }

        private string FindWasmImportsName(string frameworkFile, string frameWorkContent)
        {
            var match = WasmImportModuleRegex.Match(frameWorkContent);
            if (match.Success && match.Groups.Count >= 2)
            {
                return match.Groups[1].ToString();
            }

            // We can't instrument a framework without wasmImports module
            throw new WasmImportNotFoundException(frameworkFile);
        }

        private void InstrumentWebAssembly(
            string wasmFile,
            string outputWasmFile,
            string instrumentationDataOutputPath,
            SubmoduleConfig submoduleDefinition,
            MethodMap? methodMap,
            Dictionary<string, long>? symbolFile
        )
        {
            try
            {
                var wasmOpt = new WasmOpt()
                {
                    EmscriptenSdkPath = EmscriptenSdkPath,
                    Verbose = Verbose,
                    EmitWat = EmitWat,
                    KeepDebugInformation = KeepDebugInformation,
                    EnableSimdSupport = true,
                    EnableEmscripten4Features = EnableEmscripten4Features,
                    Development =  Development,
                    CodeOptimization = CodeOptimization,
                    Log = Log,
                    ErrorLog = ErrorLog
                };

                // Decompress WebAssembly if necessary
                var compressionType = Compression.GetCompressionType(wasmFile);
                if (compressionType != CompressionType.Disabled)
                    WriteLog($"Decompress WebAssembly {wasmFile}");
                var tempWasmFile = outputWasmFile;
                if (compressionType == CompressionType.Gzip)
                {
                    m_Compression?.DecompressGzip(wasmFile, m_TempUnpackedWasmFile);
                    wasmFile = m_TempUnpackedWasmFile;
                    tempWasmFile = m_TempInstrumentedWasmFile;
                }
                else if (compressionType == CompressionType.Brotli)
                {
                    m_Compression?.DecompressBrotli(wasmFile, m_TempUnpackedWasmFile);
                    wasmFile = m_TempUnpackedWasmFile;
                    tempWasmFile = m_TempInstrumentedWasmFile;
                }

                // Load function map if a symbol file is used
                // and the function map was not given as an input
                var functionMap = FunctionMap;
                if (symbolFile != null && FunctionMap == null)
                {
                    WriteLog($"Load function map for {wasmFile}");
                    functionMap = wasmOpt.GetFunctionMap(wasmFile);
                }

                // Create an instrumentation config
                WriteLog($"Write instrumentation config file {m_TempInstrumentationConfigFile}");
                var instrumentationConfigWriter = new InstrumentationConfigWriter()
                {
                    SubmoduleConfig = submoduleDefinition,
                    SubmodulesToInstrument = SubmodulesToInstrument,
                    MethodMap = methodMap,
                    SymbolFile = symbolFile,
                    ErrorLog = ErrorLog
                };
                if (functionMap != null)
                {
                    instrumentationConfigWriter.FunctionMap = functionMap;
                }
                instrumentationConfigWriter.Write(m_TempInstrumentationConfigFile);


                // Run wasm-opt instrumentation pass
                // Instrument WebAssembly
                WriteLog($"Instrument WebAssembly {wasmFile}");
                var labelsFile = Path.Combine(instrumentationDataOutputPath, "labels.json");
                var functionsMapFile = Path.Combine(instrumentationDataOutputPath, "functions.json");

                wasmOpt.InstrumentFunctions(
                    wasmFile,
                    tempWasmFile,
                    new()
                    {
                        InstrumentationConfigFile = m_TempInstrumentationConfigFile,
                        WasmImportModule = m_WasmImportsModule,
                        LabelsFile = labelsFile,
                        FunctionsMapFile = functionsMapFile
                    }
                );

                // Compress WebAssembly file if necessary
                switch (compressionType)
                {
                    case CompressionType.Gzip:
                        m_Compression?.CompressGzip(tempWasmFile, outputWasmFile);
                        break;
                    case CompressionType.Brotli:
                        m_Compression?.CompressBrotli(tempWasmFile, outputWasmFile);
                        break;
                }

                // Change names in functions map file
                ReplaceNamesInFunctionsMapFile(functionsMapFile, symbolFile, functionMap);
            }
            finally
            {
                // Cleanup temporary files
                if (File.Exists(m_TempUnpackedWasmFile))
                    File.Delete(m_TempUnpackedWasmFile);
                if (File.Exists(m_TempInstrumentedWasmFile))
                    File.Delete(m_TempInstrumentedWasmFile);
            }
        }

        private void ReplaceNamesInFunctionsMapFile(string functionsMapFile, Dictionary<string, long>? symbolFile, Dictionary<long, string>? functionIndexMap)
        {
            // Skip this if build has embedded debug symbols
            if (symbolFile == null || functionIndexMap == null)
                return;

            // Create inverse maps of symbol file and function index map
            var inverseSymbolFile = new Dictionary<long, string>();
            foreach (var entry in symbolFile)
            {
                inverseSymbolFile[entry.Value] = entry.Key;
            }

            var inverseFunctionIndexMap = new Dictionary<string, long>();
            foreach (var entry in functionIndexMap)
            {
                inverseFunctionIndexMap[entry.Value] = entry.Key;
            }

            var functionsMap = JsonConvert.DeserializeObject<Dictionary<long, string>>(File.ReadAllText(functionsMapFile));
            // Skip if functionsMapFile could not be loaded
            if (functionsMap == null)
                return;

            var newFunctionsMap = new Dictionary<long, string>();
            foreach (var entry in functionsMap)
            {
                // Convert function id to proper name with two stage process
                // functionId -> function index in original wasm -> function name

                if (inverseFunctionIndexMap.TryGetValue(entry.Value, out var functionIndex) &&
                    inverseSymbolFile.TryGetValue(functionIndex, out var functionName)
                )
                {
                    // Set proper function name in entry
                    newFunctionsMap[entry.Key] = functionName;
                }
                else
                {
                    // Keep entry as is
                    newFunctionsMap[entry.Key] = entry.Value;
                }
            }

            // Overwrite existing file
            File.WriteAllText(functionsMapFile, JsonConvert.SerializeObject(newFunctionsMap));
        }

        private void WriteLog(string message)
        {
            if (!Verbose)
                return;

            Log.WriteLine(message);
        }
    }
}
