using System;

namespace Unity.PlatformToolkit.PlayMode
{
    [Serializable]
    internal class PlayModeEnvironmentWarningData
    {
        /// <summary>
        /// An invalid value means that it isn't explicitly set to 0, and that a default can be set based on available UI options.
        /// </summary>
        public static int k_InvalidMs = -1;

        // Note that none of these below settings are serialized and are considered per-session settings.
        public bool StorageWarningsEnabled { get; set; } = false;

        public int OpenFrequencyMs { get; set; } = k_InvalidMs;
        public int WriteFrequencyMs { get; set; } = k_InvalidMs;
        public int ReadFrequencyMs { get; set; } = k_InvalidMs;
        public int EnumFrequencyMs { get; set; } = k_InvalidMs;
        public int CommitFrequencyMs { get; set; } = k_InvalidMs;
        public int DeleteFrequencyMs { get; set; } = k_InvalidMs;

        public int UndisposedSavesMs { get; set; } = k_InvalidMs;
    }

    internal class PlayModeWarningSettings : IWarningSettings
    {
        private static int k_MaxTimeValueMs = 20 * 1000;

        private PlayModeEnvironmentWarningData m_SerializedData;

        public PlayModeWarningSettings(PlayModeEnvironmentWarningData serializedData)
        {
            m_SerializedData = serializedData ?? throw new ArgumentNullException(nameof(serializedData));
        }

        public bool StorageWarningsEnabled
        {
            get => m_SerializedData.StorageWarningsEnabled;
            set => m_SerializedData.StorageWarningsEnabled = value;
        }

        public TimeSpan OpenFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.OpenFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.OpenFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan WriteFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.WriteFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.WriteFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan ReadFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.ReadFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.ReadFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan EnumFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.EnumFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.EnumFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan CommitFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.CommitFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.CommitFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan DeleteFrequency
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.DeleteFrequencyMs,
                    PlayModeWarningTimeOptions.Frequency.Default.Milliseconds
                );
            set => m_SerializedData.DeleteFrequencyMs = ToMilliseconds(value);
        }

        public TimeSpan UndisposedSavesThreshold
        {
            get =>
                FromMilliseconds(
                    m_SerializedData.UndisposedSavesMs,
                    PlayModeWarningTimeOptions.UndisposedSaves.Default.Milliseconds
                );
            set => m_SerializedData.UndisposedSavesMs = ToMilliseconds(value);
        }

        public TimeSpan MaximumTimeSettingValue => TimeSpan.FromMilliseconds(k_MaxTimeValueMs);

        private TimeSpan FromMilliseconds(int ms, int defaultMs)
        {
            if (ms < 0)
                return TimeSpan.FromMilliseconds(defaultMs);

            if (ms > k_MaxTimeValueMs)
                throw new ArgumentOutOfRangeException(
                    $"Argument of {ms} milliseconds exceeds the maximum of {k_MaxTimeValueMs}"
                );

            return TimeSpan.FromMilliseconds(ms);
        }

        private int ToMilliseconds(TimeSpan span)
        {
            var ms = (int)span.TotalMilliseconds;

            if (ms < 0)
                return PlayModeEnvironmentWarningData.k_InvalidMs;

            if (ms > k_MaxTimeValueMs)
                throw new ArgumentOutOfRangeException(
                    $"Argument of timespan of {ms} milliseconds exceeds the maximum of {k_MaxTimeValueMs}"
                );

            return ms;
        }
    }
}
