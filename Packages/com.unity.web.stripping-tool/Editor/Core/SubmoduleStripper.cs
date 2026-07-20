// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    class SubmoduleStripper
    {
        public CompressionType BuildCompressionType { get; set; } = CompressionType.Disabled;
        public List<string> SubmoduleDefinitionFiles { get; set; } = new();
        public string SymbolFile { get; set; } = "";
        public List<string> SubmodulesToStrip { get; set; } = new List<string>();
        public bool Optimize { get; set; } = false;
        public bool Verbose { get; set; } = false;
        public bool EmitWat { get; set; } = false;
        public bool KeepDebugInformation { get; set; } = false;
        public bool EnableSimdSupport { get; set; } = true;
        public bool EnableBigIntSupport { get; set; } = false;
        public bool EnableEmscripten4Features { get; set; } = false;
        public bool Development { get; set; } = false;
        public string CodeOptimization { get; set; } = "";
        public bool EnableLoggingOfMissingSubmodule { get; set; } = false;
        public string LogFunctionCode { get; set; } =
            "var wasmImports = {\n  \"log_execution\": function(funcId, labelId) { },";
        public string MinifiedLogFunctionCode { get; set; } =
            "var wasmImports={\"log_execution\":function(funcId, labelId){},";
        public string EmscriptenSdkPath { get; set; } = "";
        public string BrotliPath { get; set; } = "";
        public string SevenZipPath { get; set; } = "";
        public string StrippingInfoFile { get; set; } = "";

        /// <summary>
        /// If specified, the method map is used for C# submodule stripping.
        /// </summary>
        public string MethodMapFile { get; set; } = "";

        /// <summary>
        /// The Unity version of the build to strip. If set this will filter the available submodules.
        /// </summary>
        public string? UnityVersion { get; set; } = null;

        /// <summary>
        /// Name of the stripping tool.
        /// </summary>
        public string? ToolName { get; set; } = null;

        /// <summary>
        /// Product version to be written into the stripping info file.
        /// Default: 1.0.0.
        /// </summary>
        public string? ProductVersion = null;

        /// <summary>
        /// The error handling behavior when a stripped submodule is used.
        /// </summary>
        public string MissingSubmoduleErrorHandling { get; set; } = "Ignore";

        /// <summary>
        /// The file name used for stripping info that is stored next to the output files.
        /// </summary>
        public const string StrippingInfoFileName = "stripping_info.json";

        /// <summary>
        /// Log output. Default: System.Console.Out
        /// </summary>
        public TextWriter Log = Console.Out;

        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        public delegate void ProgressCallback(int step, int totalSteps, string description);

        /// <summary>
        /// A callback for updating progress on the stripping process.
        /// </summary>
        public ProgressCallback? OnProgress;

        private string TempUncompressedWasmFile = Path.Combine(
            Path.GetTempPath(),
            $"tmp_uncompressed_{Path.GetRandomFileName()}.wasm"
        );
        private string TempUncompressedSymbolFile = Path.Combine(
            Path.GetTempPath(),
            $"tmp_symbols_{Path.GetRandomFileName()}.symbols.json"
        );
        private string TempFunctionIdsFile = Path.Combine(
            Path.GetTempPath(),
            $"tmp_functions_{Path.GetRandomFileName()}.txt"
        );
        private string TempWasmFile = Path.Combine(Path.GetTempPath(), $"tmp_stripped_{Path.GetRandomFileName()}.wasm");
        private string TempLoggingWasmFile = Path.Combine(
            Path.GetTempPath(),
            $"tmp_logging_{Path.GetRandomFileName()}.wasm"
        );
        private string TempOptimizedWasmFile = Path.Combine(
            Path.GetTempPath(),
            $"tmp_optimized_{Path.GetRandomFileName()}.wasm"
        );
        private int m_CurrentStep = 0;
        private int m_TotalSteps = 0;
        private string m_LastDescription = "";

        /// <summary>
        /// Performs stripping of submodules and write
        /// </summary>
        /// <param name="wasmFile">The input WASM file to strip.</param>
        /// <param name="outputWasmFile">The output WASM file.</param>
        /// <param name="frameworkFile">Optional: The input JavaScript framework file.</param>
        /// <param name="outputFrameworkFile">Optional: The output JavaScript framework file with missing submodule logging code.</param>
        public void StripSubmodules(
            string wasmFile,
            string outputWasmFile,
            string frameworkFile = "",
            string outputFrameworkFile = ""
        )
        {
            try
            {
                StartProgressTracking();

                // Load submodule definition and symbol file
                UpdateProgress("Loading submodule list.");
                var submoduleDefinition = SubmoduleDefinitionLoader.Load(SubmoduleDefinitionFiles, UnityVersion);
                UpdateProgress();

                // Setup compression helper
                var compression = new Compression()
                {
                    BrotliPath = BrotliPath,
                    SevenZipPath = SevenZipPath,
                    Log = (text) => Log.WriteLine(text),
                    ErrorLog = (text) => ErrorLog.WriteLine(text),
                };

                // Setup wasm-opt
                var wasmOpt = new WasmOpt()
                {
                    EmscriptenSdkPath = EmscriptenSdkPath,
                    Verbose = Verbose,
                    EmitWat = EmitWat,
                    // Keep debug information during this pass if EnableLoggingOfMissingSubmodule is enabled.
                    // Debug information is required for the missing submodule logging pass.
                    // The following passes will remove the debug information if necessary.
                    KeepDebugInformation = EnableLoggingOfMissingSubmodule || KeepDebugInformation,
                    EnableSimdSupport = EnableSimdSupport,
                    EnableBigIntSupport = EnableBigIntSupport,
                    EnableEmscripten4Features = EnableEmscripten4Features,
                    Development = Development,
                    // Code optimization can be skipped during remove-functions pass if code is optimized later
                    CodeOptimization = (EnableLoggingOfMissingSubmodule || Optimize) ? "" : CodeOptimization,
                    Log = Log,
                    ErrorLog = ErrorLog,
                };

                // Uncompress wasm if necessary
                var wasmFilePath = wasmFile;
                var outputWasmFilePath = outputWasmFile;
                if (BuildCompressionType == CompressionType.Gzip)
                {
                    WriteLog($"Decompressing wasm file to: \"{TempUncompressedWasmFile}\".");
                    wasmFilePath = TempUncompressedWasmFile;
                    outputWasmFilePath = TempOptimizedWasmFile;
                    compression.DecompressGzip(wasmFile, TempUncompressedWasmFile);
                }
                else if (BuildCompressionType == CompressionType.Brotli)
                {
                    WriteLog($"Decompressing wasm file to: \"{TempUncompressedWasmFile}\".");
                    wasmFilePath = TempUncompressedWasmFile;
                    outputWasmFilePath = TempOptimizedWasmFile;
                    compression.DecompressBrotli(wasmFile, TempUncompressedWasmFile);
                }

                // Get section sizes of original wasm file (uncompressed)
                var originalWasmFileSizeInformation = WasmParser.ParseSectionSizes(wasmFilePath);

                // Load symbol file if necessary
                Dictionary<string, long>? symbolFile = null;
                Dictionary<long, string>? functionMap = null;
                if (SymbolFile != "")
                {
                    WriteLog($"Loading symbol file: \"{SymbolFile}\".");
                    UpdateProgress("Loading debug symbols.");
                    var symbolFilePath = SymbolFile;
                    if (BuildCompressionType == CompressionType.Gzip)
                    {
                        WriteLog($"Decompressing symbol file to: \"{TempUncompressedSymbolFile}\".");
                        symbolFilePath = TempUncompressedSymbolFile;
                        compression.DecompressGzip(SymbolFile, TempUncompressedSymbolFile);
                    }
                    else if (BuildCompressionType == CompressionType.Brotli)
                    {
                        WriteLog($"Decompressing symbol file to: \"{TempUncompressedSymbolFile}\".");
                        symbolFilePath = TempUncompressedSymbolFile;
                        compression.DecompressBrotli(SymbolFile, TempUncompressedSymbolFile);
                    }

                    symbolFile = SymbolFileLoader.Load(symbolFilePath);

                    // Load function map as well
                    WriteLog($"Load function map for: {wasmFilePath}");
                    functionMap = wasmOpt.GetFunctionMap(wasmFilePath);
                    UpdateProgress();
                }

                // Load method map file if present
                MethodMap? methodMap = null;
                if (!string.IsNullOrEmpty(MethodMapFile))
                {
                    WriteLog($"Loading method map file: \"{MethodMapFile}\".");
                    UpdateProgress("Loading method map.");
                    methodMap = MethodMapLoader.Load(MethodMapFile);
                    UpdateProgress();
                }

                // Create a function ids file with all functions that should be removed.\
                WriteLog($"Creating function ids file: \"{TempFunctionIdsFile}\".");
                var functionListWriter = new FunctionListWriter()
                {
                    SubmoduleConfig = submoduleDefinition,
                    SymbolFile = symbolFile,
                    MethodMap = methodMap,
                    SubmodulesToStrip = SubmodulesToStrip,
                    Verbose = Verbose,
                    Log = Log,
                    ErrorLog = ErrorLog,
                };
                bool hasFunctionsToStrip = functionListWriter.Write(TempFunctionIdsFile);

                // Run wasm opt to remove all "not executed" functions
                UpdateProgress("Removing submodules.");
                WriteLog("Stripping submodules with wasm-opt.");
                var strippedWasmFile =
                    (EnableLoggingOfMissingSubmodule || Optimize) ? TempWasmFile : outputWasmFilePath;
                HashSet<string> strippedFunctions = new();
                if (hasFunctionsToStrip)
                {
                    // Strip functions
                    strippedFunctions = wasmOpt.RemoveFunctions(wasmFilePath, strippedWasmFile, TempFunctionIdsFile);
                }
                else if (!wasmOpt.KeepDebugInformation)
                {
                    // Only remove debug information from WASM file
                    wasmOpt.RemoveDebugInformation(wasmFilePath, strippedWasmFile);
                }
                else
                {
                    // No functions to strip or debug information to remove:
                    // Copy the WASM file and only run other passes
                    File.Copy(wasmFilePath, strippedWasmFile);
                }
                UpdateProgress();

                // Add error logging to stripped submodules if enabled
                if (EnableLoggingOfMissingSubmodule)
                {
                    WriteLog("Adding missing submodule logging to wasm file.");
                    UpdateProgress("Adding missing submodule logging to wasm file.");
                    var wasmFileWithLogging = Optimize ? TempLoggingWasmFile : outputWasmFilePath;
                    var submoduleProfiler = new SubmoduleProfiler()
                    {
                        SubmoduleDefinitionFiles = SubmoduleDefinitionFiles,
                        SymbolFile = SymbolFile,
                        MethodMapFile = MethodMapFile,
                        FunctionMap = functionMap,
                        UnityVersion = UnityVersion,
                        SubmodulesToInstrument = new(SubmodulesToStrip),
                        EmscriptenSdkPath = EmscriptenSdkPath,
                        BrotliPath = BrotliPath,
                        SevenZipPath = SevenZipPath,
                        Verbose = Verbose,
                        EmitWat = EmitWat,
                        KeepDebugInformation = KeepDebugInformation,
                        EnableEmscripten4Features = EnableEmscripten4Features,
                        Development = Development,
                        // Code optimization can be skipped during instrument-functions pass if code is optimized later
                        CodeOptimization = Optimize ? "" : CodeOptimization,
                        LogFunctionCode = LogFunctionCode,
                        MinifiedLogFunctionCode = MinifiedLogFunctionCode,
                        Log = Log,
                        ErrorLog = ErrorLog,
                    };
                    submoduleProfiler.InstrumentBuild(
                        strippedWasmFile,
                        wasmFileWithLogging,
                        frameworkFile,
                        outputFrameworkFile,
                        Path.GetDirectoryName(outputWasmFile) ?? ""
                    );
                    // Replace stripped wasm file path with wasm file with logging path
                    // in case optimize pass follows
                    strippedWasmFile = wasmFileWithLogging;
                    UpdateProgress();
                }

                // Run additional optimization pass if enabled
                if (Optimize)
                {
                    WriteLog("Optimizing wasm file.");
                    UpdateProgress("Optimizing wasm file.");
                    wasmOpt.Optimize(strippedWasmFile, outputWasmFilePath);
                    UpdateProgress();
                }
                WriteLog($"Created optimized wasm file \"{outputWasmFilePath}\"");

                // Get section sizes of stripped wasm file (uncompressed)
                var wasmFileSizeInformation = WasmParser.ParseSectionSizes(outputWasmFilePath);

                // Compress wasm if necessary
                if (BuildCompressionType == CompressionType.Gzip)
                {
                    UpdateProgress("Compressing wasm file");
                    compression.CompressGzip(outputWasmFilePath, outputWasmFile);
                    WriteLog($"Created compressed wasm file \"{outputWasmFile}\"");
                    UpdateProgress();
                }
                else if (BuildCompressionType == CompressionType.Brotli)
                {
                    UpdateProgress("Compressing wasm file");
                    compression.CompressBrotli(outputWasmFilePath, outputWasmFile);
                    WriteLog($"Created compressed wasm file \"{outputWasmFile}\"");
                    UpdateProgress();
                }

                // Write stripping info file
                // Create default meta data file if not set
                if (StrippingInfoFile == "")
                {
                    var basePath = Path.GetDirectoryName(outputWasmFile) ?? "";
                    StrippingInfoFile = Path.Combine(basePath, StrippingInfoFileName);
                }

                WriteLog($"Write stripping info file \"{StrippingInfoFile}\"");
                UpdateProgress("Writing stripping info file.");
                var strippingInfoWriter = new StrippingInfoWriter()
                {
                    WasmFile = wasmFile,
                    OutputWasmFile = outputWasmFile,
                    SubmodulesToStrip = SubmodulesToStrip,
                    SubmoduleConfig = submoduleDefinition,
                    ExpandedSubmodulesToStrip = functionListWriter.SubmodulesToStrip,
                    StrippedFunctions = strippedFunctions,
                    SymbolFile = symbolFile,
                    MethodMap = methodMap,
                    OptimizeCodeAfterStripping = Optimize,
                    RemoveDebugInformation = !KeepDebugInformation,
                    MissingSubmoduleErrorHandling = MissingSubmoduleErrorHandling,
                    OriginalWasmFileSectionSizeInformation = originalWasmFileSizeInformation,
                    WasmFileSectionSizeInformation = wasmFileSizeInformation,
                    ErrorLog = ErrorLog,
                };
                if (!string.IsNullOrEmpty(ToolName))
                    strippingInfoWriter.ToolName = ToolName!;
                if (!string.IsNullOrEmpty(ProductVersion))
                    strippingInfoWriter.ProductVersion = ProductVersion!;
                strippingInfoWriter.Write(StrippingInfoFile);
                UpdateProgress();
            }
            finally
            {
                // Delete all temporary files
                if (File.Exists(TempUncompressedWasmFile))
                    File.Delete(TempUncompressedWasmFile);
                if (File.Exists(TempUncompressedSymbolFile))
                    File.Delete(TempUncompressedSymbolFile);
                if (File.Exists(TempFunctionIdsFile))
                    File.Delete(TempFunctionIdsFile);
                if (File.Exists(TempWasmFile))
                    File.Delete(TempWasmFile);
                if (File.Exists(TempOptimizedWasmFile))
                    File.Delete(TempOptimizedWasmFile);
            }
        }

        private void WriteLog(string message)
        {
            if (!Verbose)
                return;

            Log.WriteLine(message);
        }

        private void StartProgressTracking()
        {
            m_CurrentStep = 0;
            m_LastDescription = "";

            m_TotalSteps = 6;
            if (SymbolFile != "")
            {
                m_TotalSteps += 2;
                if (BuildCompressionType != CompressionType.Disabled)
                    m_TotalSteps += 2;
            }

            if (!string.IsNullOrEmpty(MethodMapFile))
            {
                m_TotalSteps += 2;
            }

            if (EnableLoggingOfMissingSubmodule)
                m_TotalSteps += 2;
            if (Optimize)
                m_TotalSteps += 2;
            if (BuildCompressionType != CompressionType.Disabled)
                m_TotalSteps += 4;
        }

        /// <summary>
        /// Tracks progress of stripping and calls OnProgress event.
        /// </summary>
        private void UpdateProgress(string? description = null)
        {
            if (OnProgress == null)
                return;

            if (description == null)
                description = m_LastDescription;
            else
                m_LastDescription = description;

            OnProgress(m_CurrentStep++, m_TotalSteps, description);
        }
    }
}
