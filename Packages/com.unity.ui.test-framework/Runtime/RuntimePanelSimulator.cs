using System;

namespace UnityEngine.UIElements.TestFramework
{
    /// <summary>
    /// A <see cref="PanelSimulator"/> for use with runtime tests.
    /// </summary>
    public sealed class RuntimePanelSimulator : PanelSimulator
    {
        /// <summary>
        /// Creates a `RuntimePanelSimulator` with
        /// a null `PanelSettings`.
        /// </summary>
        public RuntimePanelSimulator() : this(null) { }

        /// <summary>
        /// The distance used for the picking algorithm for pointer interactions in world space.
        /// </summary>
        public float pickingDistance
        {
            get { return m_PickingDistance; }
            set { m_PickingDistance = value; }
        }

        private float m_PickingDistance = 1.0f;

        /// <summary>
        /// The direction used for the picking algorithm for pointer interactions in world space.
        /// The picking direction can use the element's direction (default)
        /// or the panel's direction.
        /// When the panel's direction is used, picking occurs in the panel's forward axis
        /// regardless of the element's rotation within the panel.
        /// </summary>
        public PickingDirection pickingDirection
        {
            get { return m_PickingDirection; }
            set { m_PickingDirection = value; }
        }

        private PickingDirection m_PickingDirection = PickingDirection.ElementDirection;

        /// <summary>
        /// Creates a `RuntimePanelSimulator` with
        /// the provided <paramref name="panelSettings"/>.
        /// </summary>
        /// <param name="panelSettings">The `PanelSettings` object to set for the created panel.</param>
        public RuntimePanelSimulator(PanelSettings panelSettings)
            #pragma warning disable CS0618 // Disable warning on Internal usage
            : base()
            #pragma warning restore CS0618
        {
            if (panelSettings != null)
            {
                #pragma warning disable CS0618 // Disable warning on Internal usage
                AssignPanel(panelSettings);
                #pragma warning restore CS0618
            }
        }

        [System.Obsolete("For Internal Use Only.")]
        internal void AssignPanel(PanelSettings panelSettingsAsset)
        {
            if (panelSettingsAsset == null)
            {
                #pragma warning disable CS0618 // Disable warning on Internal usage
                SetPanel(null);
                #pragma warning restore CS0618
            }
            else
            {
                #pragma warning disable CS0618 // Disable warning on Internal usage
                SetPanel(panelSettingsAsset.panel);
                SetRootVisualElement(panelSettingsAsset.panel.visualTree);
                #pragma warning restore CS0618
            }
        }

        /// <inheritdoc cref="PanelSimulator.FrameUpdate(double)"/>
        public override void FrameUpdate(double time)
        {
            #pragma warning disable CS0618 // Disable warning on Internal usage
            EnsureFrameUpdateCalledDuringTest();
            DoFrameUpdate(time);
            #pragma warning restore CS0618
        }

        /// <summary>
        /// Makes a ray that goes towards the element's center.
        /// If @@pickingDirection@@ is used, that ray can be oriented to come from the element's
        /// forward direction or from the panel's forward direction.
        /// </summary>
        /// <param name="element">The element whose center must be intersected by the ray</param>
        /// <returns>If successful, returns a ray that intersects the element's center, expressed in the element's panel world coordinates. Otherwise, returns null.</returns>
        internal override Ray? MakeRayForWorldSpacePanel(VisualElement element)
        {
            if ((panel as BaseVisualElementPanel)?.isFlat != false)
                return null;

            var elementRay = new Ray();
            elementRay.origin = element.rect.center;

            // The element is facing towards the camera; we want the opposite side as the direction for the ray
            elementRay.direction = Vector3.back;

            var panelRay = element.LocalToWorld(elementRay);

            // Optionally, click directly into the panel
            if (pickingDirection == PickingDirection.PanelDirection)
                panelRay.direction = Vector3.forward;

            // Move the ray `pickingDistance` units back in world coordinates. This unfortunately requires us to
            // change the ray to world space, then move back, and then change it back to panel space.
            var panelComponent = element.FindRootPanelComponent();
            if (panelComponent == null)
                throw new ArgumentException("Element in world-space panel needs to come from a UIDocument or a PanelRenderer component.", nameof(element));
            var transform = panelComponent.gameObject.transform;
            var worldRay = transform.localToWorldMatrix.TransformRay(panelRay);
            worldRay.origin -= worldRay.direction * pickingDistance;
            panelRay = transform.worldToLocalMatrix.TransformRay(worldRay);

            return panelRay;
        }
    }
}
