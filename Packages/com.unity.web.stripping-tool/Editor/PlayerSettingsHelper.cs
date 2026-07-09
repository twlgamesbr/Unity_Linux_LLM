#nullable enable
using System.Reflection;
using UnityEditor;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Helper class for changing certain player settings not present in all Unity Editor versions.
    /// </summary>
    static class PlayerSettingsHelper
    {
        static PropertyInfo? SubmoduleStrippingCompatibilitySetting => typeof(PlayerSettings.WebGL).GetProperty("enableSubmoduleStrippingCompatibility");

        /// <summary>
        /// Check whether the current Unity version supports PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility
        /// </summary>
        public static bool IsSubmoduleStrippingCompatibilityAvailable => SubmoduleStrippingCompatibilitySetting != null;

        /// <summary>
        /// Access "PlayerSettings.WebGL.enableSubmoduleStrippingCompatibility" through reflection to be compatible with older Unity versions.
        /// </summary>
        public static bool EnableSubmoduleStrippingCompatibility
        {
            get
            {
                if (SubmoduleStrippingCompatibilitySetting == null)
                    return false;

                return (bool)SubmoduleStrippingCompatibilitySetting.GetValue(null);
            }
            set
            {
                SubmoduleStrippingCompatibilitySetting?.SetValue(null, value);
            }
        }
    }
}
