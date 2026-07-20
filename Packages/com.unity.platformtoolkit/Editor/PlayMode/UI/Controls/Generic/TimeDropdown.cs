using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.PlatformToolkit.PlayMode
{
    /// <summary>
    /// Equality is defined to only compare time values. PopupField will match any set values to actual defined choices.
    /// </summary>
    [UxmlObject]
    internal partial class TimeOption : IEquatable<TimeOption>
    {
        [UxmlAttribute]
        public int Milliseconds;

        [UxmlAttribute]
        public string FormattingSuffix;

        public override bool Equals(object other)
        {
            return this.Equals((TimeOption)other);
        }

        public override int GetHashCode()
        {
            return Milliseconds.GetHashCode();
        }

        public bool Equals(TimeOption other)
        {
            return Milliseconds == other.Milliseconds;
        }

        /// <summary>
        /// UI type converter for TimeOption to TimeSpan.
        /// </summary>
        public static TimeSpan ConvertValueToTimeSpan(in TimeOption value)
        {
            if (value.Milliseconds >= 0)
            {
                return TimeSpan.FromMilliseconds(value.Milliseconds);
            }
            return default;
        }

        /// <summary>
        /// UI type converter for TimeSpan to TimeOption.
        /// </summary>
        public static TimeOption ConvertTimeSpanToValue(TimeSpan span)
        {
            int milliseconds = (int)span.TotalMilliseconds;
            return new TimeOption { Milliseconds = milliseconds, FormattingSuffix = string.Empty };
        }
    }

    /// <summary>
    /// This dropdown element can be used to specify a bunch of time-based values without having to parse various display string values back to valid numbers.
    /// Choices are specified in digits instead via the <see cref="Configuration"/> property.
    /// </summary>
    [UxmlElement]
    internal partial class TimeDropdown : PopupField<TimeOption>
    {
        private const int k_OffValue = 0;

        private int m_DefaultOptionIndex = -1;
        private TimeOptionConfiguration m_Configuration;

        [UxmlAttribute]
        public string FormattingPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Sets the available options and default selection from a <see cref="TimeOptionConfiguration"/>.
        /// </summary>
        [CreateProperty]
        public TimeOptionConfiguration Configuration
        {
            get => m_Configuration;
            set
            {
                m_Configuration = value;
                if (value == null)
                    return;

                var currentValue = this.value;

                choices.Clear();
                foreach (var opt in value.Options)
                    choices.Add(opt);

                m_DefaultOptionIndex = -1;
                int defaultIdx = choices.IndexOf(value.Default);
                if (defaultIdx >= 0)
                    m_DefaultOptionIndex = defaultIdx;

                // Use SetValueWithoutNotify to avoid a spurious ChangeEvent (and TwoWay write-back)
                // when Configuration is bound before value during UXML initialization.
                currentValue = CalculateValueToSet(in currentValue, choices, m_DefaultOptionIndex);
                SetValueWithoutNotify(currentValue);
            }
        }

        /// <summary>
        /// The underlying value property needs a UxmlObjectReference override to cope with the value type being a custom UxmlObject.
        /// </summary>
        [UxmlObjectReference("value")]
        public TimeOption ValueOverride
        {
            get => this.value;
            set => this.value = value;
        }

        /// <summary>
        /// Constrain value sets to valid choices.
        /// </summary>
        public override TimeOption value
        {
            get => base.value;
            set
            {
                var valueToUse = value;
                base.value = CalculateValueToSet(in valueToUse, choices, m_DefaultOptionIndex);
            }
        }

        public TimeDropdown()
            : base()
        {
            formatSelectedValueCallback = FormatSelectedItem;
            formatListItemCallback = FormatItem;
        }

        private static TimeOption CalculateValueToSet(in TimeOption value, List<TimeOption> choices, int defaultIndex)
        {
            // Find valid match or fall back to default
            int index = choices.IndexOf(value);
            if (index == -1)
            {
                if (defaultIndex >= 0 && defaultIndex < choices.Count)
                {
                    return choices[defaultIndex];
                }
            }
            else
            {
                // Since equality only matches the time value, get the authored choice value for any additional fields.
                return choices[index];
            }
            return value;
        }

        private string FormatSelectedItem(TimeOption option)
        {
            return FormatItem(option, string.Empty);
        }

        private string FormatItem(TimeOption option)
        {
            return FormatItem(option, option?.FormattingSuffix ?? string.Empty);
        }

        private string FormatItem(TimeOption option, string formattingSuffix)
        {
            if (option == null || option.Milliseconds < 0)
                return null;

            if (option.Milliseconds == k_OffValue)
                return "Off";
            else if (option.Milliseconds == 1000)
                return $"{FormattingPrefix}1 Second{formattingSuffix}";

            return $"{FormattingPrefix}{option.Milliseconds / 1000.0f} Seconds{formattingSuffix}";
        }
    }
}
