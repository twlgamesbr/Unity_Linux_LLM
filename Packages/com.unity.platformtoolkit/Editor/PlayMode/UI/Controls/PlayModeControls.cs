using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeControls
    {
        private VisualElement m_ControlsContainer;
        private VisualElement m_ControlsContainerParent;
        private PlayModeControlsSystemControlsField m_SystemControlsField;
        private PlayModeControlsAccountControlsField m_AccountsField;
#if INPUT_SYSTEM_AVAILABLE
        private PlayModeControlsInputMappingField m_InputMappingField;
#endif

        private bool m_GuiCreated;

        public PlayModeControls(VisualElement rootElement)
        {
            m_ControlsContainer = rootElement.Q("controls-container");
            m_SystemControlsField = rootElement.Q<PlayModeControlsSystemControlsField>("play-mode-controls-system-controls");
            m_AccountsField = rootElement.Q<PlayModeControlsAccountControlsField>("play-mode-controls-account-controls");
#if INPUT_SYSTEM_AVAILABLE
            m_InputMappingField = rootElement.Q<PlayModeControlsInputMappingField>("play-mode-controls-input-mapping");
#endif

            // Default to not showing the controls container. We do this by removing from the hierarchy to avoid still evaluating bindings.
            m_ControlsContainerParent = m_ControlsContainer.parent;
            m_ControlsContainer.RemoveFromHierarchy();

            BindElements();
            m_GuiCreated = true;
        }

        public void BindElements()
        {
            UnbindElements();

            if (PlayModeControlsEditorSettings.instance.CurrentSettings is { } settings)
            {
                var playModeControlsViewModel = settings.ViewModel;
                if (playModeControlsViewModel != null && playModeControlsViewModel.IsValid)
                {
                    m_ControlsContainer.dataSource = playModeControlsViewModel;
                    if (m_ControlsContainer.parent == null)
                        m_ControlsContainerParent.Add(m_ControlsContainer);

                    m_AccountsField.Bind(playModeControlsViewModel);
                    m_SystemControlsField.Bind(playModeControlsViewModel);
#if INPUT_SYSTEM_AVAILABLE
                    m_InputMappingField.Bind(playModeControlsViewModel);
#endif
                }
            }
        }

        public void UnbindElements()
        {
            if (m_GuiCreated)
            {
                m_AccountsField.Unbind();
                m_SystemControlsField.Unbind();
#if INPUT_SYSTEM_AVAILABLE
                m_InputMappingField.Unbind();
#endif
                m_ControlsContainer.RemoveFromHierarchy();
                m_ControlsContainer.dataSource = null;
            }
        }
    }
}
