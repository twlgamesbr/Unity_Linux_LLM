using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using System;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Controls what happens if a function of a stripped submodule is called.
    /// </summary>
    public enum MissingSubmoduleErrorHandlingType
    {
        /// <summary>
        /// Do nothing if a function of a stripped submodule is called.
        /// </summary>
        Ignore = 0,
        /// <summary>
        /// Log an error to the browser console if a function of a stripped submodule is called,
        /// but try to continue execution.
        /// </summary>
        LogError,
        /// <summary>
        /// Throw an exception if a function of a stripped submodule is called and halt execution.
        /// </summary>
        ThrowException
    }

    /// <summary>
    /// An asset for configuring submodule stripping settings.
    /// </summary>
    [CreateAssetMenu(fileName = "SubmoduleStrippingSettings", menuName = RootMenuName + "/Submodule Stripping Settings")]
    [HelpURL(PackageConstants.DocumentationUrl + "/manual/" + k_DocumentationPage + ".html")]
    public class SubmoduleStrippingSettings : ScriptableObject
    {
        // Path inside "Documentation~" folder to the documentation page containing the docs of this asset, no file extension.
        internal const string k_DocumentationPage = "submodule-reference";

        /// <summary>
        /// Raised when the values of the settings are changed.
        /// </summary>
        public event Action ValuesChanged;

        /// <summary>
        /// The root menu name used for various menu items.
        /// </summary>
        public const string RootMenuName = "Web Optimization";

        /// <summary>
        /// Run code optimization to reduce final build size and improve performance.
        /// Increases the stripping time significantly. Use on release builds.
        /// </summary>
        [Tooltip("Run code optimization to reduce final build size and improve performance. Increases the stripping time significantly. Use on release builds.")]
        public bool OptimizeCodeAfterStripping;

        /// <summary>
        /// Remove debug information after stripping.
        /// It is preferred to use <see cref="RemoveDebugInformation">RemoveDebugInformation</see> instead.
        /// </summary>
        [Tooltip("Remove debug information after stripping. Debug symbols are required to identify functions during stripping, but they increase the size of WebAssembly files. Use on release builds if debug symbols are not required for other uses cases.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool RemoveEmbeddedDebugSymbols;

        /// <summary>
        /// Remove debug information after stripping.
        /// Debug symbols are required to identify functions during stripping
        /// but they increase the size of WebAssembly files.
        /// Use on release builds if debug symbols are not required for other use cases.
        /// </summary>
        [Tooltip("Remove debug information after stripping. Debug symbols are required to identify functions during stripping, but they increase the size of WebAssembly files. Use on release builds if debug symbols are not required for other uses cases.")]
        public bool RemoveDebugInformation
        {
            get => RemoveEmbeddedDebugSymbols;

            set
            {
                RemoveEmbeddedDebugSymbols = value;
            }
        }

        /// <summary>
        /// Whether stripping can be performed with the current settings.
        /// Stripping can be performed if at least one of these conditions are fulfilled:
        /// - There is at least one SubmodulesToStrip configured.
        /// - OptimizeCodeAfterStripping is enabled.
        /// - RemoveDebugInformation is enabled.
        /// </summary>
        internal bool CanRunStripping => SubmodulesToStrip.Count > 0 || OptimizeCodeAfterStripping || RemoveDebugInformation;

        /// <summary>
        /// The error handling behavior when a stripped submodule is used.
        /// </summary>
        [Tooltip("The error handling behavior when a stripped submodule is used. The usage of a stripped submodule can be ignored, logged to the browser console, or thrown as an exception.")]
        public MissingSubmoduleErrorHandlingType MissingSubmoduleErrorHandling;

        /// <summary>
        /// The list of submodules to strip from a build.
        /// </summary>
        [Tooltip("The list of submodules to strip from a build.")]
        public List<string> SubmodulesToStrip = new();

        /// <summary>
        /// Save changes to the settings to disk.
        /// </summary>
        public void Save()
        {
            // Save the settings to disk
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        void Reset()
        {
            OptimizeCodeAfterStripping = false;
            SubmodulesToStrip = new List<string>();
        }

        void OnValidate()
        {
            ValuesChanged?.Invoke();
        }

        /// <summary>
        /// Creates a settings asset.
        /// </summary>
        /// <param name="assetPath">Note that the final name can be different if an asset with the same name already exists.</param>
        /// <returns>The created asset.</returns>
        public static SubmoduleStrippingSettings Create(string assetPath)
        {
            var settings = CreateInstance<SubmoduleStrippingSettings>();
            AssetDatabase.CreateAsset(settings, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }
    }
}
