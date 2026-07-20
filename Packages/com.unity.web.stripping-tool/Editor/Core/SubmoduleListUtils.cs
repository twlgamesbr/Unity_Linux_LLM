// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System.Collections.Generic;
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Util class for managing list of submodules
    /// </summary>
    internal class SubmoduleListUtils
    {
        /// <summary>
        /// Expand all submodules with nested submodules in a list
        /// </summary>
        /// <param name="submodules">A list of submodule names</param>
        /// <param name="submoduleConfig">The submodule configuration file.</param>
        /// <param name="errorLog">Optional: error log output.</param>
        /// <returns>A list of unique submodule names with all submodules expanded.</returns>
        public static HashSet<string> ExpandNestedSubmodules(
            IEnumerable<string> submodules,
            SubmoduleConfig submoduleConfig,
            TextWriter? errorLog = null
        )
        {
            var expandedSubmodules = new HashSet<string>();

            foreach (var submodule in submodules)
            {
                GetNestedSubmodules(submodule, submoduleConfig, errorLog, expandedSubmodules);
            }

            return expandedSubmodules;
        }

        private static void GetNestedSubmodules(
            string submodule,
            SubmoduleConfig submoduleConfig,
            TextWriter? errorLog,
            HashSet<string> outSubmodules
        )
        {
            var submoduleDefinition = submoduleConfig.submodules.Find(x => x.name == submodule);
            if (submoduleDefinition == null)
            {
                // Skip if submodule definition was not found
                errorLog?.WriteLine($"Warning: submodule '{submodule}' not found in submodule definition file.");
                return;
            }

            // Only add submodules that exist to output list
            outSubmodules.Add(submodule);

            // Skip if submodule definition does not contain nested submodules
            if (submoduleDefinition.submodules.Count == 0)
            {
                return;
            }

            foreach (var childSubmodule in submoduleDefinition.submodules)
            {
                if (!outSubmodules.Contains(childSubmodule))
                {
                    // If not already in the list add submodule and traverse it
                    // (Check prevents infinite loops because of cycles in submodule hierarchy)
                    GetNestedSubmodules(childSubmodule, submoduleConfig, errorLog, outSubmodules);
                }
            }
        }
    }
}
