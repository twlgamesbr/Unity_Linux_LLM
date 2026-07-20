namespace Unity.PlatformToolkit.PlayMode
{
    internal static class PlayModeWarningTimeOptions
    {
        private static readonly TimeOption[] s_FrequencyOptions =
        {
            new() { Milliseconds = 0 },
            new() { Milliseconds = 100, FormattingSuffix = " (relaxed)" },
            new() { Milliseconds = 1000, FormattingSuffix = " (default)" },
            new() { Milliseconds = 5000, FormattingSuffix = " (strict)" },
        };

        /// <summary>Options for the per-operation frequency warning dropdowns. Default is 1000 ms.</summary>
        public static readonly TimeOptionConfiguration Frequency = new(s_FrequencyOptions, s_FrequencyOptions[2]);

        private static readonly TimeOption[] s_UndisposedSavesOptions =
        {
            new() { Milliseconds = 0 },
            new() { Milliseconds = 15000, FormattingSuffix = " (relaxed)" },
            new() { Milliseconds = 7000, FormattingSuffix = " (default)" },
            new() { Milliseconds = 1000, FormattingSuffix = " (strict)" },
        };

        /// <summary>Options for the undisposed saves lifetime dropdown. Default is 7000 ms.</summary>
        public static readonly TimeOptionConfiguration UndisposedSaves = new(
            s_UndisposedSavesOptions,
            s_UndisposedSavesOptions[2]
        );
    }
}
