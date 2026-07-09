using UnityEngine.UIElements;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A label UI element that allows to select and copy it's content.
    /// </summary>
    class DetailLabel : Label
    {
        /// <summary>
        /// Constructs a detail label.
        /// </summary>
        public DetailLabel() : this(string.Empty) {}

        /// <summary>
        /// Constructs a detail label.
        /// <param name="text">The text to be displayed.</param>
        /// </summary>
        public DetailLabel(string text) : base(text)
        {
            selection.isSelectable = true;
        }
    }
}
