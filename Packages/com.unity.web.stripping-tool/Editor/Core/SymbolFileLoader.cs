// NOTE: generic code, do not use UnityEngine here
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Unity.Web.Stripping.Editor
{
    using JsonSymbolFile = Dictionary<string, string>;
    using SymbolFile = Dictionary<string, long>;

    /// <summary>
    /// Class <b>SymbolFileLoader</b> loads a emscription .symbol file.
    /// </summary>
    class SymbolFileLoader
    {
        private static string EscapedUTF8CharacterPattern = @"\\([a-fA-F0-9]{2})";
        private static string EscapedUTF8CharacterReplacement = @"\u00$1";

        /// <summary>
        /// Load emscripten .symbol file from given path.
        /// </summary>
        /// <param name="path">Path to emscripten symbol file.</param>
        /// <returns>A dictionary that maps a function name to its id in a WASM binary.</returns>
        public static SymbolFile Load(string path)
        {
            if (path.EndsWith(".symbols.json"))
            {
                // Load json encoded symbol file
                return LoadJsonFile(path);
            }

            // Load text encoded symbol file
            return LoadTextFile(path);
        }

        public static SymbolFile LoadTextFile(string path)
        {
            // Read symbol file
            var lines = File.ReadLines(path);
            var symbolFile = new SymbolFile();

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');
                if (index == -1)
                    continue;

                var functionId = long.Parse(line.Substring(0, index));
                var functionName = UnescapeFunctionName(line.Substring(index + 1));

                symbolFile[functionName] = functionId;
            }

            return symbolFile;
        }

        private static SymbolFile LoadJsonFile(string path)
        {
            var symbolFile = new SymbolFile();
            var jsonFile = JsonConvert.DeserializeObject<JsonSymbolFile>(File.ReadAllText(path));
            if (jsonFile == null)
                return symbolFile;

            foreach (var item in jsonFile)
            {
                var functionId = long.Parse(item.Key);
                var functionName = item.Value;

                symbolFile[functionName] = functionId;
            }

            return symbolFile;
        }

        private static string UnescapeFunctionName(string functionName)
        {
            return Regex.Unescape(
                Regex.Replace(functionName, EscapedUTF8CharacterPattern, EscapedUTF8CharacterReplacement)
            );
        }
    }
}
