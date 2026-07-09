using System;

namespace Unity.PlatformToolkit.PlayMode
{
    internal interface IWarningSettings
    {
        /// <summary>
        /// Overrides the enable state of all storage warnings.
        /// </summary>
        bool StorageWarningsEnabled { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Open operations.
        /// </summary>
        TimeSpan OpenFrequency { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Write operations.
        /// </summary>
        TimeSpan WriteFrequency { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Read operations.
        /// </summary>
        TimeSpan ReadFrequency { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Enumeration operations.
        /// </summary>
        TimeSpan EnumFrequency { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Commit operations.
        /// </summary>
        TimeSpan CommitFrequency { get; set; }

        /// <summary>
        /// Time window in which to warn about redundant Delete operations.
        /// </summary>
        TimeSpan DeleteFrequency { get; set; }

        /// <summary>
        /// Time after which a save dispose not being called should be flagged.
        /// </summary>
        TimeSpan UndisposedSavesThreshold { get; set; }

        /// <summary>
        /// Time after which frequency data can be safely invalidated.
        /// </summary>
        TimeSpan MaximumTimeSettingValue { get; }
    }
}
