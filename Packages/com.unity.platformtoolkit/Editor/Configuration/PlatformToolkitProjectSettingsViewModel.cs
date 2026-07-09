using Unity.Properties;

namespace Unity.PlatformToolkit.Editor
{
    internal class PlatformToolkitProjectSettingsViewModel
    {
        [CreateProperty]
        public bool ShowBuildProfileWarning
        {
            get
            {
#if UNITY_6000_4_OR_NEWER
                var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
                return profile != null && profile.GetComponent<BuildProfileSettings>() != null;
#else
                return false;
#endif
            }
        }
    }
}
