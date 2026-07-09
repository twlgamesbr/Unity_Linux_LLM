using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Unity.PlatformToolkit
{
    /// <summary>
    /// An async waitable evnt type for single comsumer, multiple writer. WaitAsync is not thread safe for multiple consumers.
    /// </summary>
    internal sealed class AsyncManualResetEvent : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> m_Source = new() { RunContinuationsAsynchronously = true };
        private readonly object m_Lock = new();
        private bool m_IsSet;

        public ValueTask WaitAsync() => new ValueTask(this, m_Source.Version);

        public void Set()
        {
            lock (m_Lock)
            {
                if (!m_IsSet)
                {
                    m_IsSet = true;
                    m_Source.SetResult(true);
                }
            }
        }

        public void Reset()
        {
            lock (m_Lock)
            {
                if (m_IsSet)
                {
                    m_IsSet = false;
                    m_Source.Reset();
                }
            }
        }

        void IValueTaskSource.GetResult(short token) => m_Source.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => m_Source.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            m_Source.OnCompleted(continuation, state, token, flags);
        }
    }
}
