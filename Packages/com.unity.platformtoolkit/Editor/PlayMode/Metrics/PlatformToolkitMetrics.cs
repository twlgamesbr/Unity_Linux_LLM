using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.PlatformToolkit.PlayMode
{
    /// <summary>
    /// Class used to implement metrics for warning on API usages that might risk submissions.
    /// Not used outside of Editor Play Mode Controls.
    ///
    /// An int AccountID is used to track unique users. Account objects are not used as they are not equatable across sign-ins.
    /// </summary>
    internal class PlatformToolkitMetrics
    {
        private IWarningSettings m_Settings;

        internal Func<DateTime> GetUtcNow { get; set; } = () => DateTime.UtcNow;

        internal event Action<string> OnWarningReported;

        /// <summary>
        /// Event types are used to bucket similar events together and only monitor them in isolation.
        /// This won't completely match up to some platform-specific frequency requirements as things opens, commits and deletes can be kinds of writes,
        /// but we instead try to flag rapid API usage on the same paths to catch possible issues in calling code.
        /// </summary>
        private enum EventType : int
        {
            OpenRead,
            OpenWrite,
            Read,
            Write,
            Enumerate,
            Commit,
            Delete,
        }

        private readonly struct Event
        {
            public Event(EventType type, int accountId, string location)
            {
                Type = type;
                AccountId = accountId;
                Location = location;
            }

            public readonly EventType Type;
            public readonly int AccountId;
            public readonly string Location;
        }

        private readonly struct EventKey : IEquatable<EventKey>
        {
            public readonly EventType Type;
            public readonly int AccountId;
            public readonly string Location;

            public EventKey(EventType type, int accountId, string location)
            {
                Type = type;
                AccountId = accountId;
                Location = location;
            }

            public override bool Equals(object obj)
            {
                throw new NotImplementedException("Not implemented to avoid boxing");
            }

            public bool Equals(EventKey other)
            {
                return Type == other.Type
                    && AccountId == other.AccountId
                    && string.Equals(Location, other.Location, StringComparison.Ordinal);
            }

            public override int GetHashCode()
            {
                if (Location == null)
                    return HashCode.Combine(Type, AccountId);

                return HashCode.Combine(Type, AccountId, Location);
            }
        }

        private readonly struct StoredAccount
        {
            public StoredAccount(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public readonly int Id;
            public readonly string Name;
        }

        private readonly struct StoredEvent
        {
            public StoredEvent(EventKey key, DateTime timeUtc)
            {
                TimeUtc = timeUtc;
                Key = key;
            }

            public readonly DateTime TimeUtc;
            public readonly EventKey Key;
        }

        private struct StoredHandleEvent
        {
            public StoredHandleEvent(
                int accountId,
                string accountName,
                string location,
                object disposable,
                DateTime timeUtc
            )
            {
                TimeUtc = timeUtc;
                AccountId = accountId;
                AccountName = accountName;
                Disposable = disposable;
                Location = location;
                HasWarned = false;
            }

            public readonly DateTime TimeUtc;
            public readonly int AccountId;
            public readonly string AccountName; // Warnings for undisposed handles can often be logged after sign-out, so cache the name for more useful logging.
            public readonly string Location;
            public readonly object Disposable;
            public bool HasWarned;
        }

        private AsyncLock m_Lock = new AsyncLock();

        /// <summary>
        /// Allows lookup of reoccuring events to identify repeated API operations.
        /// </summary>
        private Dictionary<EventKey, StoredEvent> m_EventRecords = new Dictionary<EventKey, StoredEvent>(16);
        private List<StoredHandleEvent> m_HandleRecords = new List<StoredHandleEvent>(16);
        private List<StoredAccount> m_Accounts = new List<StoredAccount>(4);

        private List<EventKey> m_TempKeysToPrune = new List<EventKey>(16);

        public PlatformToolkitMetrics(IWarningSettings warningSettings)
        {
            m_Settings = warningSettings;
        }

        public void ClearHistory()
        {
            using (var lck = m_Lock.Lock())
            {
                m_EventRecords.Clear();
                m_HandleRecords.Clear();
            }
        }

        public void AddUser(int accountId, string displayName)
        {
            using (var lck = m_Lock.Lock())
            {
                Assert.IsTrue(m_Accounts.FindIndex(x => x.Id == accountId) == -1);
                m_Accounts.Add(new StoredAccount(accountId, displayName));
            }
        }

        public void RemoveUser(int accountId)
        {
            using (var lck = m_Lock.Lock())
            {
                int existingIndex = m_Accounts.FindIndex(x => x.Id == accountId);
                Assert.IsFalse(existingIndex == -1);
                m_Accounts.RemoveAt(existingIndex);
            }
        }

        public void LogStorageOpenedDisposable(int accountId, string location, object disposable)
        {
            using (var lck = m_Lock.Lock())
            {
                if ((m_HandleRecords.FindIndex(x => x.Disposable == disposable) != -1))
                    Debug.LogError("LogStorageOpenedDisposable called for a disposable that's already tracked.");

                string accountName = accountId != -1 ? GetAccountName(accountId) : null;

                m_HandleRecords.Add(new StoredHandleEvent(accountId, accountName, location, disposable, GetUtcNow()));
            }
        }

        public void LogStorageDispose(object disposable)
        {
            using (var lck = m_Lock.Lock())
            {
                // Dispose might be called redundantly.
                // Low numbers of handles are expected to be open at once hence linear search.
                int existingIndex = m_HandleRecords.FindIndex(x => x.Disposable == disposable);
                if (existingIndex != -1)
                {
                    var handle = m_HandleRecords[existingIndex];
                    ValidateLifetime(GetUtcNow(), ref handle);
                    m_HandleRecords.RemoveAt(existingIndex);
                }
            }
        }

        public void Update()
        {
            using (var lck = m_Lock.Lock())
            {
                var timeUtc = GetUtcNow();
                PruneRecords(timeUtc);
                ValidateLifetimes(timeUtc);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageOpenRead(int accountId, string location) =>
            LogEvent(new Event(EventType.OpenRead, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageOpenWrite(int accountId, string location) =>
            LogEvent(new Event(EventType.OpenWrite, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageRead(int accountId, string location) =>
            LogEvent(new Event(EventType.Read, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageWrite(int accountId, string location) =>
            LogEvent(new Event(EventType.Write, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageEnumerate(int accountId, string location) =>
            LogEvent(new Event(EventType.Enumerate, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageCommit(int accountId, string location) =>
            LogEvent(new Event(EventType.Commit, accountId, location));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogStorageDelete(int accountId, string location) =>
            LogEvent(new Event(EventType.Delete, accountId, location));

        private bool TryGetEventTypeIntervalTicks(EventType type, out long ticks)
        {
            switch (type)
            {
                case EventType.OpenWrite:
                case EventType.OpenRead:
                    ticks = m_Settings.OpenFrequency.Ticks;
                    break;
                case EventType.Read:
                    ticks = m_Settings.ReadFrequency.Ticks;
                    break;
                case EventType.Write:
                    ticks = m_Settings.WriteFrequency.Ticks;
                    break;
                case EventType.Enumerate:
                    ticks = m_Settings.EnumFrequency.Ticks;
                    break;
                case EventType.Commit:
                    ticks = m_Settings.CommitFrequency.Ticks;
                    break;
                case EventType.Delete:
                    ticks = m_Settings.DeleteFrequency.Ticks;
                    break;
                default:
                    throw new NotImplementedException($"GetEventTypeInterval not implemented for EventType {type}");
            }

            return ticks > 0;
        }

        private bool TryGetUndisposedSaveIntervalTicks(out long ticks)
        {
            ticks = m_Settings.UndisposedSavesThreshold.Ticks;
            return ticks > 0;
        }

        private void LogEvent(in Event evt)
        {
            var eventKey = new EventKey(evt.Type, evt.AccountId, evt.Location);
            var eventTimeUtc = GetUtcNow();

            using (var lck = m_Lock.Lock())
            {
                if (m_EventRecords.TryGetValue(eventKey, out var lastEvent))
                {
                    var timePassed = eventTimeUtc - lastEvent.TimeUtc;
                    var timePassedTicks = timePassed.Ticks;

                    if (
                        TryGetEventTypeIntervalTicks(evt.Type, out var thresholdTicks)
                        && timePassedTicks <= thresholdTicks
                    )
                    {
                        var accountName = evt.AccountId != -1 ? GetAccountName(evt.AccountId) : null;

                        ReportLimitReached(
                            MakeEventReportString(
                                evt.Location,
                                accountName,
                                evt.Type,
                                (float)TimeSpan.FromTicks(thresholdTicks).TotalSeconds
                            )
                        );
                    }
                }

                m_EventRecords[eventKey] = new StoredEvent(eventKey, eventTimeUtc);
            }
        }

        private void PruneRecords(DateTime nowUtc)
        {
            Assert.IsTrue(m_TempKeysToPrune.Count == 0);
            foreach (var value in m_EventRecords.Values)
            {
                var secondsElapsed = (nowUtc - value.TimeUtc).TotalSeconds;
                if (secondsElapsed > m_Settings.MaximumTimeSettingValue.TotalSeconds)
                {
                    m_TempKeysToPrune.Add(value.Key);
                }
            }

            foreach (var key in m_TempKeysToPrune)
            {
                m_EventRecords.Remove(key);
            }
            m_TempKeysToPrune.Clear();
        }

        private string GetAccountName(int accountId)
        {
            int existingIndex = m_Accounts.FindIndex(x => x.Id == accountId);
            if (existingIndex != -1)
            {
                return m_Accounts[existingIndex].Name;
            }

            Debug.LogError("GetAccountName user name not found.");
            return $"[UNKNOWN]";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateLifetime(DateTime nowUtc, ref StoredHandleEvent handleEvent)
        {
            if (handleEvent.HasWarned)
                return;

            var timePassed = nowUtc - handleEvent.TimeUtc;
            var timePassedTicks = timePassed.Ticks;

            if (TryGetUndisposedSaveIntervalTicks(out var thresholdTicks) && timePassedTicks >= thresholdTicks)
            {
                ReportLimitReached(
                    MakeHandleReportString(
                        handleEvent.Location,
                        handleEvent.AccountName,
                        (float)TimeSpan.FromTicks(thresholdTicks).TotalSeconds
                    )
                );
                handleEvent.HasWarned = true;
            }
        }

        private void ValidateLifetimes(DateTime nowUtc)
        {
            for (int i = 0; i < m_HandleRecords.Count; i++)
            {
                var handle = m_HandleRecords[i];
                ValidateLifetime(nowUtc, ref handle);
                m_HandleRecords[i] = handle;
            }
        }

        private static string MakeEventReportString(
            string location,
            string accountName,
            EventType type,
            float thresholdSeconds
        )
        {
            string locationStr = location != null ? $"at location '{location}' " : string.Empty;
            string accountStr = accountName != null ? $"for account '{accountName}' " : string.Empty;

            string extraContext =
                type == EventType.Enumerate
                    ? "Note that Exists checks are often implemented as file enumerations, which may trigger this warning. "
                    : string.Empty;

            return $"Detected a repeated storage '{type}' event {accountStr}{locationStr}within a {thresholdSeconds} second period. "
                + $"{extraContext}"
                + "These warnings can be configured from the Play Mode Controls window.";
        }

        private static string MakeHandleReportString(string location, string accountName, double timePassedSeconds)
        {
            string locationStr = location != null ? $"at location '{location}' " : string.Empty;
            string accountStr = accountName != null ? $"for account '{accountName}' " : string.Empty;

            return $"Detected a storage handle {accountStr}{locationStr}has been held open for {timePassedSeconds:F2} seconds. "
                + "Storage objects should be used and disposed within a reasonable timeframe to ensure changes aren't lost, Operating System handles aren't left open, and submission requirements on certain platforms are met. "
                + "These warnings can be configured from the Play Mode Controls window.";
        }

        private void ReportLimitReached(string message)
        {
            if (m_Settings.StorageWarningsEnabled)
            {
                var fullMessage = $"Platform Toolkit metrics: {message}";
                Debug.LogWarning(fullMessage);

                OnWarningReported?.Invoke(fullMessage);
            }
        }
    }
}
