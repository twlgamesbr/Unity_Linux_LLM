namespace UnityEngine.UIElements.TestFramework
{
    /// <summary>
    /// The direction used for the picking algorithm during pointer interactions in world space.
    /// </summary>
    public enum PickingDirection
    {
        /// <summary>
        /// Picking is executed using the element's forward axis as the direction
        /// of the ray coming into the panel.
        /// </summary>
        /// <remarks>This is the default value.</remarks>
        ElementDirection,

        /// <summary>
        /// Picking is executed using the panel's forward axis.
        /// In that case, the element's rotation within the panel is not taken into account.
        /// </summary>
        PanelDirection,
    }
}
