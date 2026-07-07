using UnityEditor;
using UnityEditor.SettingsManagement;

namespace NPCSystem.Editor
{
    class NPCBuildSetting<T> : UserSetting<T>
    {
        public NPCBuildSetting(string key, T value, SettingsScope scope = SettingsScope.Project)
            : base(NPCBuildSettingsManager.instance, key, value, scope) { }
    }
}
