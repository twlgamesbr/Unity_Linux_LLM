namespace NPCSystem.Editor
{
    static class NPCBuildConfigProvider
    {
        const string k_PreferencesPath = "Preferences/NPC Build Config";

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider()
        {
            return new UserSettingsProvider(
                k_PreferencesPath,
                NPCBuildSettingsManager.instance,
                new[] { typeof(NPCBuildConfigProvider).Assembly }
            );
        }
    }
}
