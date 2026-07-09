using System;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A helper class for saving and restoring Web Player and build settings when making changes.
    /// </summary>
    public class WebPlayerSettingsScope : IDisposable
    {
        /// <summary>
        /// The original settings when an instance of this class is created.
        /// </summary>
        public WebPlayerSettings OriginalSettings { get; } = WebPlayerSettings.FromPlayerSettings();

        /// <summary>
        /// Restores the original settings.
        /// </summary>
        public void Dispose()
        {
            OriginalSettings.WriteToPlayerSettings();
        }
    }
}
