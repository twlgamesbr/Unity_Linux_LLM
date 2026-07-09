using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.Editor
{
    internal class BuildProfileSettings : ScriptableObject
    {
        internal const string k_PlatformImplementationKeyProperty = nameof(m_PlatformImplementationKey);

        [SerializeField]
        private string m_PlatformImplementationKey;

        /// <summary>
        /// Returns the configured implementation key if it is valid for the given profile.
        /// </summary>
        public string GetImplementationKey(BuildProfile buildProfile, out bool isKeyValidChoice)
        {
            var choices = GatherValidImplementations(buildProfile);
            if (choices.Count == 0)
            {
                isKeyValidChoice = false;
                return m_PlatformImplementationKey;
            }

            var match = choices.Find(x => x.Key.Equals(m_PlatformImplementationKey, StringComparison.OrdinalIgnoreCase));

            isKeyValidChoice = match != null;
            return m_PlatformImplementationKey;
        }

        /// <summary>
        /// If the stored key is null or empty, resets it to the first available implementation and marks the asset dirty.
        /// Explicit values must be corrected by the user.
        /// </summary>
        public void AssignKeyIfEmpty(BuildProfile buildProfile)
        {
            string key = GetImplementationKey(buildProfile, out bool isKeyValidChoice);
            if (!isKeyValidChoice &&string.IsNullOrEmpty(key))
            {
                var choices = GatherValidImplementations(buildProfile);
                m_PlatformImplementationKey = choices.Count > 0 ? choices[0].Key : null;
                EditorUtility.SetDirty(this);
            }
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += RepairKeyDeferred;
        }

        private void RepairKeyDeferred()
        {
            if (this == null)
                return;

            var path = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path))
                return;

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var buildProfile = type == typeof(BuildProfile)
                ? AssetDatabase.LoadAssetAtPath<BuildProfile>(path)
                : null;

            if (buildProfile != null)
                AssignKeyIfEmpty(buildProfile);
        }

        public static List<IPlatformToolkitSupportDeclaration> GatherValidImplementations(BuildProfile buildProfile)
        {
            var choices = new List<IPlatformToolkitSupportDeclaration>();
            var implementations = SupportDeclarationManager.SupportDeclarations;

            var serializedProfile = new SerializedObject(buildProfile);
            var profilePlatformIdProperty = serializedProfile.FindProperty("m_PlatformId");
            Assert.IsNotNull(profilePlatformIdProperty);
            Assert.IsTrue(profilePlatformIdProperty.propertyType == SerializedPropertyType.String);

            GUID profilePlatformId = new GUID(profilePlatformIdProperty.stringValue);

            foreach (var implInfo in implementations)
            {
                if (IsImplementationSupported(implInfo, profilePlatformId))
                {
                    choices.Add(implInfo);
                }
            }
            return choices;
        }

        private static bool IsImplementationSupported(IPlatformToolkitSupportDeclaration implInfo, GUID profilePlatformGuid)
        {
            if (implInfo.SupportedBuildProfileGuids == null)
                return false;

            foreach (var platformGuid in implInfo.SupportedBuildProfileGuids)
            {
                if (platformGuid == profilePlatformGuid)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_6000_4_OR_NEWER
        [BuildProfileSettingsProvider(typeof(BuildProfileSettings))]
        static BuildProfileSettingsProvider createProvider() => new BuildProfileSettingsProvider("Platform Toolkit Settings")
        {
            canAddSetting = static (BuildProfile profile) =>
            {
                var valid = GatherValidImplementations(profile);
                return valid != null && valid.Count > 0;
            },
            hasCustomEditor = true,
            tooltip = "Provide Platform Toolkit with settings for which implementation to use with this profile."
        };
#endif
    }


#if UNITY_6000_4_OR_NEWER
    /// <summary>
    /// Adds a dropdown where applicable for the component, allowing selection of a specific implementation.
    /// Settings are only modified if an option is explicitly changed.
    /// </summary>
    [CustomEditor(typeof(BuildProfileSettings))]
    internal class BuildProfileComponentEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var targetAsset = serializedObject.targetObject as BuildProfileSettings;
            BuildProfile buildProfile = null;
            if (targetAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(targetAsset);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                buildProfile = type == typeof(BuildProfile) ? AssetDatabase.LoadAssetAtPath<BuildProfile>(path) : null;
            }

            if (buildProfile == null)
                return new Label("Unsupported target object");

            return BuildProfileInspectorGUI.CreateInspectorGUI(buildProfile, serializedObject);
        }
    }
#endif

    internal static class BuildProfileInspectorGUI
    {
        public static VisualElement CreateInspectorGUI(BuildProfile profile, SerializedObject serializedObject)
        {
            BuildProfileSettings targetAsset = serializedObject.targetObject as BuildProfileSettings;

            var keyProperty = serializedObject.FindProperty(BuildProfileSettings.k_PlatformImplementationKeyProperty);
            Debug.Assert(keyProperty != null);

            var choices = BuildProfileSettings.GatherValidImplementations(profile);

            VisualElement customInspector = new VisualElement();
            customInspector.Add(CreateImpElement(choices, keyProperty));
            return customInspector;
        }

        private static VisualElement CreateImpElement(List<IPlatformToolkitSupportDeclaration> choices, SerializedProperty keyProperty)
        {
            const string typeHeader = "Platform implementation";

            if (choices.Count <= 0)
                return new Label("No available implementations");

            var dropdown = new PopupField<IPlatformToolkitSupportDeclaration>(typeHeader);
            dropdown.labelElement.style.minWidth = 150;

            string missingKey = null;
            Func<IPlatformToolkitSupportDeclaration, string> formatCallback = (IPlatformToolkitSupportDeclaration info) =>
                {
                    if (info == null)
                        return missingKey != null ? $"{missingKey} (Missing)" : "None";

                    return info.DisplayName;
                };

            int selectedIndex = -1;
            foreach (var choice in choices)
            {
                dropdown.choices.Add(choice);

                if (choice.Key.Equals(keyProperty.stringValue, System.StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = dropdown.choices.Count - 1;
                }
            }

            // Use a match if found
            if (selectedIndex != -1)
            {
                dropdown.index = selectedIndex;
            }
            // If no matches exist but a value was serialized, show the key as missing in case the user can restore it
            else if (!string.IsNullOrEmpty(keyProperty.stringValue))
            {
                missingKey = keyProperty.stringValue;
                dropdown.choices.Insert(0, null);
                dropdown.index = 0;
            }
            // Otherwise default to something else
            else
            {
                dropdown.index = 0;
            }

            // Setting these will trigger a refresh, hence set them last. The selection text can end up cached wrong if done earlier.
            dropdown.formatListItemCallback = formatCallback;
            dropdown.formatSelectedValueCallback = formatCallback;

            dropdown.RegisterValueChangedCallback((ChangeEvent<IPlatformToolkitSupportDeclaration> evt) =>
            {
                keyProperty.stringValue = evt.newValue?.Key;
                keyProperty.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }
    }
}

