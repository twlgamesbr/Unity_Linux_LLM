using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Helper class to handle JavaScript code template assets
    /// </summary>
    static class TemplateAssetsHelper
    {
        public static string DataPath => Path.Combine(Utils.PackagePath, "ProfilingTemplate~");

        /// <summary>
        /// Load a code file from the template assets
        /// </summary>
        /// <param name="codeFile">The name of the code file.</param>
        /// <param name="replacements">
        /// Optional: A dictionary with values to replace in the code file.
        /// The key is the name of the placeholder and value is the value to insert.
        /// In JavaScript code the placeholders are defined with this syntax: {{{ PLACEHOLDER_NAME }}}
        /// </param>
        /// <returns>The code inside the code file.</returns>
        public static string GetFunctionCode(string codeFile, Dictionary<string, string> replacements = default)
        {
            var code = File.ReadAllText(Path.Combine(DataPath, codeFile));

            if (replacements?.Count > 0)
            {
                return ReplacePlaceholders(code, replacements);
            }

            return code;
        }

        /// <summary>
        /// Replaces placeholders in JavaScript code, i.e., {{{ PLACEHOLDER_NAME }}} with the value from the dictionary.
        /// </summary>
        /// <param name="code">The original JavaScript code</param>
        /// <param name="replacements">A replacement dictionary where the key is the name of the placeholder and value is the value to insert.</param>
        /// <returns>The code with placeholders in dictionary replaced.</returns>
        static string ReplacePlaceholders(string code, Dictionary<string, string> replacements)
        {
            foreach (var replacement in replacements)
            {
                var regex = new Regex("{{{\\s?" + Regex.Escape(replacement.Key) + "\\s?}}}");
                code = regex.Replace(code, replacement.Value);
            }

            return code;
        }
    }
}
