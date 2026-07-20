using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    /// <summary>
    /// Foldout element with a toggle checkbox attached to it.
    /// This element uses a custom content container to allow for the toggle to not be disrupted by bounds changes from the Foldout's normal content container.
    /// </summary>
    [UxmlElement]
    internal partial class ToggleFoldout : VisualElement
    {
        VisualElement m_HeaderContainer = new VisualElement();
        Foldout m_Foldout = new Foldout();
        Toggle m_Toggle = new Toggle();
        Label m_Label = new Label();
        VisualElement m_Container = new VisualElement { name = "pt-content" };

        private string m_ToggleValueDataSourcePath;

        public ToggleFoldout()
            : base()
        {
            m_Container.AddToClassList(Foldout.contentUssClassName);

            m_Label.AddToClassList("toggle-foldout__header__label");
            m_HeaderContainer.AddToClassList("toggle-foldout__header");

            // As a custom container for the foldout that sits in its own hierarchy, this needs to manually handle visibility changes.
            m_Container.style.display = m_Foldout.value ? DisplayStyle.Flex : DisplayStyle.None;
            m_Foldout.RegisterValueChangedCallback(evt =>
            {
                m_Container.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            // An event is needed to correct the visibility state once persistence data has deserialized and been applied for the child element. This doesn't notify as a normal data change.
            m_Foldout.RegisterCallbackOnce(
                (GeometryChangedEvent evt) =>
                {
                    m_Container.style.display = m_Foldout.value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            );

            // Default to folded before deserialization.
            m_Foldout.value = false;

            // Wipe styles on the unused Foldout container because it could mess with the layout.
            m_Foldout.contentContainer.ClearClassList();

            m_HeaderContainer.hierarchy.Add(m_Foldout);
            m_HeaderContainer.hierarchy.Add(m_Toggle);
            m_HeaderContainer.hierarchy.Add(m_Label);

            hierarchy.Add(m_HeaderContainer);
            hierarchy.Add(m_Container);
        }

        public override VisualElement contentContainer => m_Container;

        /// <summary>
        /// Tooltip is applied to the header element and not the root, to avoid the activation and placement looking erratic.
        /// </summary>
        [UxmlAttribute(nameof(tooltip))]
        public string tooltipOverride
        {
            get => m_HeaderContainer.tooltip;
            set { m_HeaderContainer.tooltip = value; }
        }

        [UxmlAttribute]
        public bool foldoutValue
        {
            get { return m_Foldout.value; }
            set { m_Foldout.value = value; }
        }

        [UxmlAttribute]
        public string text
        {
            get { return m_Label.text; }
            set { m_Label.text = value; }
        }

        /// <summary>
        /// Propogate data source path to the correct child without affecting content children of this element.
        /// </summary>
        [UxmlAttribute]
        public string toggleValueDataSourcePath
        {
            get { return m_ToggleValueDataSourcePath; }
            set
            {
                if (!string.Equals(m_ToggleValueDataSourcePath, value))
                {
                    m_ToggleValueDataSourcePath = value;

                    m_Toggle.ClearBindings();
                    m_Toggle.SetBinding("value", new DataBinding { dataSourcePath = new PropertyPath(value) });
                }
            }
        }

        /// <summary>
        /// Sets the viewDataKey for the underlying Foldout element.
        /// Note that if a foldout value was serialized as well, that will override any persistence.
        /// </summary>
        [UxmlAttribute(nameof(viewDataKey))]
        public string viewDataKeyOverride
        {
            get { return base.viewDataKey; }
            set
            {
                base.viewDataKey = value;
                if (!string.IsNullOrEmpty(value))
                {
                    m_Foldout.viewDataKey = "foldoutInternal";
                }
                else
                {
                    m_Foldout.viewDataKey = null;
                }
            }
        }
    }
}
