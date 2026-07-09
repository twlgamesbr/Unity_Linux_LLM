// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A loaded submodule_definition.json file.
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class SubmoduleConfig
    {
        public List<SubmoduleDefinition> submodules { get; set; } = new List<SubmoduleDefinition>();

        public SubmoduleConfig Merge(SubmoduleConfig submoduleConfig, Version? unityVersion)
        {
            foreach (var newSubmodule in submoduleConfig.submodules)
            {
                // Skip submodule incompatible with the current unityVersion
                if (!newSubmodule.IsCompatibleWith(unityVersion))
                    continue;

                // Try to find submodule with the same name
                int index = submodules.FindIndex((submodule) => submodule.name == newSubmodule.name);

                if (index == -1)
                {
                    // Submodule does not exist yet, add it to the current config
                    submodules.Add(newSubmodule);
                }
                else if (newSubmodule.Version > submodules[index].Version)
                {
                    // Submodule is newer replace the existing submodule
                    submodules[index] = newSubmodule;
                }
            }

            return this;
        }
    }

    /// <summary>
    /// A definition of a submodule.
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class SubmoduleDefinition
    {
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public List<string> functions { get; set; } = new List<string>();
        public List<string> submodules { get; set; } = new List<string>();
        public CSharpSubmoduleDefinition? csharpMethodFilters { get; set; }
        public string? version { get; set; }
        public string? minUnityVersion { get; set; }
        public string? maxUnityVersion { get; set; }

        [JsonIgnore]
        public Version Version => version == null ? DefaultSubmoduleVersion : Version.Parse(version);
        [JsonIgnore]
        public Version? MinUnityVersion => SubmoduleDefinitionLoader.ParseUnityVersion(minUnityVersion);
        [JsonIgnore]
        public Version? MaxUnityVersion => SubmoduleDefinitionLoader.ParseUnityVersion(maxUnityVersion);

        /// <summary>
        /// Check if a Unity version is compatible with the submodule.
        /// </summary>
        /// <param name="unityVersion">The Unity version to compare against.</param>
        /// <returns>Returns true if the submodule is compatible with the Unity version.</returns>
        public bool IsCompatibleWith(Version? unityVersion)
        {
            if (unityVersion == null)
                return true;

            if (MinUnityVersion != null && unityVersion < MinUnityVersion)
                return false;

            if (MaxUnityVersion != null && unityVersion > MaxUnityVersion)
                return false;

            return true;
        }

        private static readonly Version DefaultSubmoduleVersion = new Version(1, 0, 0);
    }

    /// <summary>
    /// Definition of C# code in a submodule
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class CSharpSubmoduleDefinition
    {
        public List<CSharpSubmoduleFilter> include = new List<CSharpSubmoduleFilter>();
        public List<CSharpSubmoduleFilter>? exclude;
    }

    /// <summary>
    /// A filter rule for C# code
    /// </summary>
    [SuppressMessage("", "IDE1006", Justification = "Adheres to the JS naming convention")]
    class CSharpSubmoduleFilter
    {
        public string[]? methods { get; set; }
        public string[]? assemblies { get; set; }
    }

    /// <summary>
    /// Class <b>SubmoduleDefinitionLoader</b> loads a submodule definition file.
    /// It automatically demangles the function names in the file.
    /// </summary>
    class SubmoduleDefinitionLoader
    {
        /// <summary>
        /// Load submodule definition file.
        /// </summary>
        /// <param name="path">Path to submodule definition file.</param>
        public static SubmoduleConfig Load(string path)
        {
            // Read submodule configuration file
            var submoduleConfig = JsonConvert.DeserializeObject<SubmoduleConfig>(File.ReadAllText(path));

#pragma warning disable CS8603 // Possible null reference return.
            return submoduleConfig;
#pragma warning restore CS8603 // Possible null reference return.
        }

        /// <summary>
        /// Loads multiple submodule definition files and combines them into one.
        /// If there are conflicting submodule definitions the definition with the newer version is preferred.
        /// </summary>
        /// <param name="paths">A list of paths to submodule definition files.</param>
        /// <param name="unityVersion">Optional: The current unity version. If set will filter for submodules compatible with the current version.</param>
        public static SubmoduleConfig Load(List<string> paths, string? unityVersion = null)
        {
            SubmoduleConfig submoduleDefinition = new SubmoduleConfig();

            foreach (var file in paths)
            {
                var currentSubmoduleDefinition = Load(file);
                submoduleDefinition.Merge(currentSubmoduleDefinition, ParseUnityVersion(unityVersion));
            }

            return submoduleDefinition;
        }

        /// <summary>
        /// Parse a Unity version string.
        /// </summary>
        /// <param name="unityVersion">A unity version as string.</param>
        /// <returns>A version object.</returns>
        public static Version? ParseUnityVersion(string? unityVersion)
        {
            if (unityVersion == null)
                return null;

            return Version.Parse(Regex.Replace(unityVersion, @"(a|b|f)", "."));
        }
    }
}
