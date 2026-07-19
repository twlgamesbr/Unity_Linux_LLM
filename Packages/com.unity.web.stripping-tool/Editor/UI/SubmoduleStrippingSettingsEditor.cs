using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace Unity.Web.Stripping.Editor
{
    [CustomEditor(typeof(SubmoduleStrippingSettings))]
    class SubmoduleStrippingSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty m_RunOptimizationPassProperty;
        SerializedProperty m_RemoveEmbeddedDebugSymbolsProperty;
        SerializedProperty m_MissingSubmoduleErrorHandlingProperty;
        SerializedProperty m_SubmodulesToStripProperty;
        SubmoduleSelectionWindow m_SelectionWindow;
        VisualElement m_ButtonRow;
        bool m_IsDiscarding;
        string m_OriginalJson;

        void OnEnable()
        {
            m_RunOptimizationPassProperty = serializedObject.FindProperty(nameof(SubmoduleStrippingSettings.OptimizeCodeAfterStripping));
            m_RemoveEmbeddedDebugSymbolsProperty = serializedObject.FindProperty(nameof(SubmoduleStrippingSettings.RemoveEmbeddedDebugSymbols));
            m_MissingSubmoduleErrorHandlingProperty = serializedObject.FindProperty(nameof(SubmoduleStrippingSettings.MissingSubmoduleErrorHandling));
            m_SubmodulesToStripProperty = serializedObject.FindProperty(nameof(SubmoduleStrippingSettings.SubmodulesToStrip));
            // Snapshot the current on-disk state so DiscardChanges can restore it.
            // AssetDatabase.ImportAsset does not revert in-memory changes for already-loaded assets,
            // so we capture the serialized state here and restore it manually on discard.
            m_OriginalJson = EditorJsonUtility.ToJson(target);

            // Set custom message when changes are made
            saveChangesMessage = $"The {ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings))} have unsaved changes. Save changes?";

            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            // We want to have a dedicated selection window per editor instance so close existing window when closing the editor
            if (m_SelectionWindow != null)
                m_SelectionWindow.Close();

            EditorApplication.update -= OnEditorUpdate;
        }

        // Ctrl+S saves assets via AssetDatabase.SaveAssets() without calling our SaveChanges() override,
        // so TrackSerializedObjectValue never fires (values didn't change, only the dirty flag cleared).
        // Poll here to detect that case and hide the buttons.
        void OnEditorUpdate()
        {
            if (hasUnsavedChanges && !EditorUtility.IsDirty(target))
            {
                m_OriginalJson = EditorJsonUtility.ToJson(target);
                hasUnsavedChanges = false;
                UpdateButtonVisibility();
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var visualElement = new VisualElement();

            // Add fields for properties
            var runOptimizationPassPropertyField = new PropertyField(
                m_RunOptimizationPassProperty,
                ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings.OptimizeCodeAfterStripping))
            );
            visualElement.Add(runOptimizationPassPropertyField);
            runOptimizationPassPropertyField.Bind(serializedObject);

            var removeEmbeddedDebugSymbolsPropertyField = new PropertyField(
                m_RemoveEmbeddedDebugSymbolsProperty,
                ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings.RemoveDebugInformation))
            );
            visualElement.Add(removeEmbeddedDebugSymbolsPropertyField);
            removeEmbeddedDebugSymbolsPropertyField.Bind(serializedObject);

            var missingSubmoduleErrorHandlingPropertyField = new PropertyField(
                m_MissingSubmoduleErrorHandlingProperty,
                ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings.MissingSubmoduleErrorHandling))
            );
            visualElement.Add(missingSubmoduleErrorHandlingPropertyField);
            missingSubmoduleErrorHandlingPropertyField.Bind(serializedObject);

            var submodulesToStripField = new PropertyField(
                m_SubmodulesToStripProperty,
                ObjectNames.NicifyVariableName(nameof(SubmoduleStrippingSettings.SubmodulesToStrip))
            );
            submodulesToStripField.SetEnabled(false);
            visualElement.Add(submodulesToStripField);
            submodulesToStripField.Bind(serializedObject);
            submodulesToStripField.RegisterCallback<GeometryChangedEvent>((evt) =>
                InitSubmoduleList(evt.target as PropertyField, (target as SubmoduleStrippingSettings).SubmodulesToStrip)
            );

            var button = CreateSelectButton();
            button.clicked += () => OpenSubmoduleSelectionWindow();
            visualElement.Add(button);

            m_ButtonRow = new VisualElement();
            m_ButtonRow.style.flexDirection = FlexDirection.Row;
            m_ButtonRow.style.justifyContent = Justify.FlexEnd;
            m_ButtonRow.style.marginTop = 6;
            m_ButtonRow.style.display = DisplayStyle.None;
            visualElement.Add(m_ButtonRow);

            var discardButton = new Button() { name = "unity-revert-button", text = "Revert" };
            discardButton.clicked += DiscardChanges;
            m_ButtonRow.Add(discardButton);

            var saveButton = new Button() { name = "unity-apply-button", text = "Apply" };
            saveButton.clicked += SaveChanges;
            m_ButtonRow.Add(saveButton);

            visualElement.TrackSerializedObjectValue(serializedObject, _ =>
            {
                // Suppress the callback triggered by DiscardChanges reverting values
                if (m_IsDiscarding)
                {
                    m_IsDiscarding = false;
                    return;
                }

                // If the asset is not dirty, the change was already saved externally (e.g. by SubmoduleStrippingWindow).
                // Sync our snapshot to the current on-disk state and clear any pending unsaved-changes UI.
                if (!EditorUtility.IsDirty(target))
                {
                    m_OriginalJson = EditorJsonUtility.ToJson(target);
                    hasUnsavedChanges = false;
                    UpdateButtonVisibility();
                    return;
                }

                hasUnsavedChanges = true;
                UpdateButtonVisibility();
            });

            return visualElement;
        }

        void UpdateButtonVisibility()
        {
            m_ButtonRow.style.display = hasUnsavedChanges ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void SaveChanges()
        {
            AssetDatabase.SaveAssetIfDirty(target);
            m_OriginalJson = EditorJsonUtility.ToJson(target);
            base.SaveChanges();
            UpdateButtonVisibility();
        }

        public override void DiscardChanges()
        {
            // Suppress the TrackSerializedObjectValue callback triggered by the value revert
            m_IsDiscarding = true;
            EditorJsonUtility.FromJsonOverwrite(m_OriginalJson, target);
            EditorUtility.ClearDirty(target);
            serializedObject.Update();
            base.DiscardChanges();
            UpdateButtonVisibility();
        }

        internal static Button CreateSelectButton() =>
            new() { name = "unity-select-submodules", text = "Select Submodules" };

        private void OpenSubmoduleSelectionWindow()
        {
            m_SelectionWindow = SubmoduleSelectionWindow.GetAsUtilityPopup();
            m_SelectionWindow.SelectedSubmodules = PropertyUtils.GetHashSetPropertyValue(m_SubmodulesToStripProperty);
            m_SelectionWindow.SelectedSubmodulesChanged += (selectedSubmodules) => {
                serializedObject.Update();
                PropertyUtils.SetHashSetPropertyValue(m_SubmodulesToStripProperty, selectedSubmodules);
                serializedObject.ApplyModifiedProperties();
            };
        }

        internal static ListView InitSubmoduleList(PropertyField listPropField, List<string> submodules)
        {
            var listView = listPropField.Q<ListView>();
            if (listView is null)
            {
                // This function was called too early, PropertyField for List hasn't initialized itself fully yet
                return null;
            }

            listView.reorderable = false;
            listView.showFoldoutHeader = true;
            // Show collection size but don't allow editing it
            listView.showBoundCollectionSize = true;
            listView.Q(className: "unity-list-view__size-field")?.SetEnabled(false);
            listView.showAddRemoveFooter = false;
            listView.makeItem = () => new Label();
            listView.bindItem = (item, index) =>
            {
                if (index < submodules.Count)
                    (item as Label).text = submodules[index];
            };
            return listView;
        }
    }
}