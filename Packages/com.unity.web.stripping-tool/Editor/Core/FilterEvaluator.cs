// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Unity.Web.Stripping.Editor
{
    class FilterEvaluator
    {
        /// <summary>
        /// A filter to check against a name.
        /// </summary>
        private interface IFilter
        {
            /// <summary>
            /// Iterate over all items that match the filter and
            /// calls the iterator callback with the key and value.
            /// </summary>
            /// <typeparam name="T">The value type in the dictionary.</typeparam>
            /// <param name="collection">A dictionary with a string key.</param>
            /// <param name="iterator">A callback that gets called for each item in the dictionary that matches the filter.</param>
            public void ForEachItemIn<T>(Dictionary<string, T> collection, Action<string, T> iterator);
        }

        /// <summary>
        /// A filter that does simple string comparison.
        /// </summary>
        private class StringComparisonFilter : IFilter
        {
            private readonly string Filter;

            public StringComparisonFilter(string filter, bool replaceKeywordsWithTypes)
            {
                Filter = replaceKeywordsWithTypes ? ReplaceKeywordsWithTypes(filter) : filter;
            }

            public void ForEachItemIn<T>(Dictionary<string, T> collection, Action<string, T> iterator)
            {
                if (collection.TryGetValue(Filter, out var item))
                {
                    iterator(Filter, item);
                }
            }
        }

        /// <summary>
        /// A filter that uses placeholders.
        /// </summary>
        private class RegexFilter : IFilter
        {
            private readonly Regex Filter;

            public RegexFilter(string filter, bool replaceKeywordsWithTypes)
            {
                Filter = new Regex(
                    "^"
                        + Regex
                            .Escape(replaceKeywordsWithTypes ? ReplaceKeywordsWithTypes(filter) : filter)
                            .Replace("\\*", ".*?")
                        + "$",
                    RegexOptions.Compiled
                );
            }

            public void ForEachItemIn<T>(Dictionary<string, T> collection, Action<string, T> iterator)
            {
                foreach (var pair in collection)
                {
                    if (Filter.IsMatch(pair.Key))
                    {
                        iterator(pair.Key, pair.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Create a filter object from the string filter definition.
        /// </summary>
        /// <param name="filter">A filter rule from a submodule definition file.</param>
        /// <param name="replaceKeywordsWithTypes">Replace keywords with .NET type names.</param>
        /// <returns>A filter object depending on the type of filter(StringComparisonFilter or RegexFilter).</returns>
        private static IFilter CreateFilter(string filter, bool replaceKeywordsWithTypes)
        {
            if (filter.Contains("*"))
            {
                // Use a RegexFilter if the filter rule contains * placeholders
                return new RegexFilter(filter, replaceKeywordsWithTypes);
            }
            else
            {
                // Use a StringComparisonFilter if the filter rule does not have * placeholders
                return new StringComparisonFilter(filter, replaceKeywordsWithTypes);
            }
        }

        /// <summary>
        /// A class for evaluating C# method filter rules to find methods in a method map.
        /// </summary>
        private class CSharpFilter
        {
            private readonly List<IFilter> MethodFilters = new List<IFilter>();
            private readonly List<IFilter> AssemblyFilters = new List<IFilter>();

            public CSharpFilter(CSharpSubmoduleFilter csharpFilter)
            {
                // Convert filter strings to IFilter instances
                if (csharpFilter.assemblies != null)
                {
                    foreach (var assemblyFilter in csharpFilter.assemblies)
                    {
                        AssemblyFilters.Add(CreateFilter(assemblyFilter, false));
                    }
                }

                if (csharpFilter.methods != null)
                {
                    foreach (var methodFilter in csharpFilter.methods)
                    {
                        MethodFilters.Add(CreateFilter(methodFilter, true));
                    }
                }
            }

            /// <summary>
            /// Find all methods in method map that match the filter rules and return their native function names.
            /// </summary>
            /// <param name="methodMap">A method map loaded from a IL2CPP method map file.
            /// The map allows to look up method information by the assembly name and the method name or by the method name only.
            /// </param>
            /// <returns>A set of native function names for C# methods that match the filter rules.</returns>
            public HashSet<string> FindMethods(MethodMap methodMap)
            {
                if (AssemblyFilters.Count > 0)
                {
                    // Search by assembly
                    return FindMethodInAssembly(methodMap);
                }
                else if (MethodFilters.Count > 0)
                {
                    // Search by method name
                    return FindMethodInMethodDictionary(methodMap);
                }

                return new HashSet<string>();
            }

            private HashSet<string> FindMethodInAssembly(MethodMap methodMap)
            {
                var functionNames = new HashSet<string>();

                foreach (var assemblyFilter in AssemblyFilters)
                {
                    assemblyFilter.ForEachItemIn(
                        methodMap.MethodsByAssembly,
                        (assemblyName, methodDictionary) =>
                        {
                            if (MethodFilters.Count == 0)
                            {
                                // Add all methods if no method filter is defined
                                foreach (var methodList in methodDictionary.Values)
                                {
                                    foreach (var method in methodList)
                                    {
                                        functionNames.Add(method.NativeFunctionName);
                                    }
                                }
                            }
                            else
                            {
                                // Search by method name within assembly
                                foreach (var methodFilter in MethodFilters)
                                {
                                    methodFilter.ForEachItemIn(
                                        methodDictionary,
                                        (methodName, methodList) =>
                                        {
                                            foreach (var method in methodList)
                                            {
                                                functionNames.Add(method.NativeFunctionName);
                                            }
                                        }
                                    );
                                }
                            }
                        }
                    );
                }

                return functionNames;
            }

            private HashSet<string> FindMethodInMethodDictionary(MethodMap methodMap)
            {
                var functionNames = new HashSet<string>();

                // Apply method filter
                foreach (var filter in MethodFilters)
                {
                    filter.ForEachItemIn(
                        methodMap.Methods,
                        (methodName, methodList) =>
                        {
                            foreach (var method in methodList)
                            {
                                functionNames.Add(method.NativeFunctionName);
                            }
                        }
                    );
                }

                return functionNames;
            }
        }

        /// <summary>
        /// Find all methods in method map that match the filter rules and return their native function names.
        /// </summary>
        /// <param name="methodMap">A method map loaded from a IL2CPP method map file.
        /// The map allows to look up method information by the assembly name and the method name or by the method name only.
        /// </param>
        /// <param name="csharpSubmoduleDefinition">A C# submodule definition with filter rules.</param>
        /// <returns>A set of native function names for C# methods that match the filter rules.</returns>
        public static HashSet<string> FindMethods(
            MethodMap methodMap,
            CSharpSubmoduleDefinition csharpSubmoduleDefinition
        )
        {
            // Create list of functions to include
            var includeList = new HashSet<string>();
            foreach (var csharpMethodFilter in csharpSubmoduleDefinition.include)
            {
                var compiledFilter = new CSharpFilter(csharpMethodFilter);
                includeList.UnionWith(compiledFilter.FindMethods(methodMap));
            }

            // Create list of functions to exclude
            var excludeList = new HashSet<string>();
            if (csharpSubmoduleDefinition.exclude != null)
            {
                foreach (var csharpMethodFilter in csharpSubmoduleDefinition.exclude)
                {
                    var compiledFilter = new CSharpFilter(csharpMethodFilter);
                    excludeList.UnionWith(compiledFilter.FindMethods(methodMap));
                }
            }

            // Return list of found methods without the methods in the exclude list
            includeList.ExceptWith(excludeList);
            return includeList;
        }

        // The surrounding \b Regex ensures that the whole word is matched
        // and no replacement of type names within words can happen.
        static ReadOnlyDictionary<string, Regex> DotNetTypeReplacements = new(
            new Dictionary<string, Regex>
            {
                { "System.Void", new(@"\bvoid\b") },
                { "System.Boolean", new(@"\bbool\b") },
                { "System.SByte", new(@"\bsbyte\b") },
                { "System.Byte", new(@"\bbyte\b") },
                { "System.Char", new(@"\bchar\b") },
                { "System.Decimal", new(@"\bdecimal\b") },
                { "System.Double", new(@"\bdouble\b") },
                { "System.Single", new(@"\bfloat\b") },
                { "System.UIntPtr", new(@"\bnuint\b") },
                { "System.IntPtr", new(@"\bnint\b") },
                { "System.UInt32", new(@"\buint\b") },
                { "System.Int32", new(@"\bint\b") },
                { "System.UInt64", new(@"\bulong\b") },
                { "System.Int64", new(@"\blong\b") },
                { "System.UInt16", new(@"\bushort\b") },
                { "System.Int16", new(@"\bshort\b") },
                { "System.String", new(@"\bstring\b") },
                { "System.Object", new(@"\bobject\b") },
            }
        );

        /// <summary>
        /// Replace C# keywords, such as 'void', 'bool', and 'int', and so on, with the
        /// full .NET type names, 'System.Void', 'System.Boolean', 'System.Int32', and so on.
        /// <param name="filter">A filter for methods</param>
        /// </summary>
        public static string ReplaceKeywordsWithTypes(string filter)
        {
            string outFilter = filter;
            foreach (var pair in DotNetTypeReplacements)
            {
                outFilter = pair.Value.Replace(outFilter, pair.Key);
            }

            return outFilter;
        }
    }
}
