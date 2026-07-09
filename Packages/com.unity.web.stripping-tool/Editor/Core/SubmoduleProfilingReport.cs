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
    /// An exception that is thrown when a submodule profiling report can not be loaded.
    /// </summary>
    class InvalidSubmoduleProfilingReportException : Exception
    {
        /// <summary>
        /// Path to the submodule profiling report.
        /// </summary>
        public string Path { get; internal set; } = "";

        public InvalidSubmoduleProfilingReportException(string path)
            : base($"Failed to load submodule profiling report from: '{path}'.\nInvalid report format.")
        {
            Path = path;
        }
    }

    /// <summary>
    /// A submodule profiling report created by a submodule profiling run.
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class SubmoduleProfilingReport
    {
        /// <summary>
        /// A dictionary of submodules used in the report. The value of the dictionary is set
        /// to true if the submodule was used and false if it was not used during a profiling run.
        /// Submodules not present in the dictionary were not profiled so no data is available for them.
        /// </summary>
        [JsonRequired]
        public Dictionary<string, bool> submodules = new();

        /// <summary>
        /// A list of functions inside submodules that were called during the profiling run.
        /// We currently don't use that information but it is useful for designing new submodules.
        /// </summary>
        public HashSet<string> calledFunctions = new();

        /// <summary>
        /// Load a submodule profiling report from a file.
        /// </summary>
        /// <param name="path">Path to the submodule profiling report.</param>
        /// <returns>A submodule profiling report.</returns>
        /// <exception cref="InvalidSubmoduleProfilingReportException"></exception>
        public static SubmoduleProfilingReport LoadFromFile(string path)
        {
            try
            {
                var report = JsonConvert.DeserializeObject<SubmoduleProfilingReport>(File.ReadAllText(path));

                if (report == null)
                    throw new InvalidSubmoduleProfilingReportException(path);

                return report;
            }
            catch (JsonException)
            {
                throw new InvalidSubmoduleProfilingReportException(path);
            }
        }

        /// <summary>
        /// Loads multiple submodule profiling reports and merges them into one report.
        /// </summary>
        /// <param name="paths">A list of paths to submodule reports.</param>
        /// <returns>A submodule profiling report.</returns>
        public static SubmoduleProfilingReport LoadFromFiles(IEnumerable<string> paths)
        {
            var report = new SubmoduleProfilingReport();

            // Load reports one by one and merge them
            foreach (var path in paths)
            {
                var otherReport = LoadFromFile(path);
                report.MergeWith(otherReport);
            }

            return report;
        }

        /// <summary>
        /// Merges this submodule profiling report with another report.
        /// A submodule is considered as "used" if it is marked as used in this report or the other report.
        /// Submodules only present in the other report will be added to this report.
        /// </summary>
        /// <param name="other"></param>
        public void MergeWith(SubmoduleProfilingReport other)
        {
            foreach (var entry in other.submodules)
            {
                // Add submodule to this profiling report if it is marked as "used"
                // in the other report or not present in this report
                if (entry.Value || !submodules.ContainsKey(entry.Key))
                {
                    submodules[entry.Key] = entry.Value;
                }
            }

            calledFunctions.UnionWith(other.calledFunctions);
        }

        /// <summary>
        /// A set of of submodules marked as unused in this report
        /// </summary>
        public HashSet<string> UnusedSubmodules
        {
            get
            {
                var unusedSubmodules = new HashSet<string>();
                foreach (var entry in submodules)
                {
                    if (!entry.Value)
                        unusedSubmodules.Add(entry.Key);
                }

                return unusedSubmodules;
            }
        }
    }
}
