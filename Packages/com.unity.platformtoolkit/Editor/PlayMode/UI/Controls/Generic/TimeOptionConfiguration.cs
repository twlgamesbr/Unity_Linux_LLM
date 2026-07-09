using System.Collections.Generic;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class TimeOptionConfiguration
    {
        public IReadOnlyList<TimeOption> Options { get; }
        public TimeOption Default { get; }

        public TimeOptionConfiguration(IReadOnlyList<TimeOption> options, TimeOption defaultOption)
        {
            Options = options;
            Default = defaultOption;
        }
    }
}
