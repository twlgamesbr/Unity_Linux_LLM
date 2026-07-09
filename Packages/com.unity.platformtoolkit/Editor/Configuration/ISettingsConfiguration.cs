using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.Editor
{
    /// <summary>Settings instance and additional functionality to manage display and storage of settings.</summary>
    internal interface ISettingsConfiguration
    {
        /// <summary>Settings object which is returned by <see cref="PlatformToolkitEditor.TryGetSettings{TSettings}"/>.</summary>
        /// <remarks>The actual type of this object must be castable to <see cref="IPlatformToolkitSettingsProvider.SettingsType"/> of the provider that created this configuration.</remarks>
        object Settings { get; }

        /// <summary>Populate the provided containers with UI for this platform's settings.</summary>
        /// <param name="settingsContainer">Add platform-specific settings UI elements here.</param>
        /// <param name="attributeContainer">Add an <see cref="AttributeSettingsField"/> here if this platform has attribute settings.</param>
        void CreateSettingsUI(VisualElement settingsContainer, VisualElement attributeContainer);
    }
}
