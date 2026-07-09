using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.PlatformToolkit
{
    /// <summary>
    /// An asynchronous lock class that provides "scoped" disposable locks that release on disposal.
    /// This is a less error-prone alternative to SemaphoreSlim, since it's harder to forget to Release() this lock.
    ///
    /// Sample use:
    /// class MyClass {
    ///     private readonly AsyncLock m_Lock = new();
    ///
    ///     async Task MyMethod() {
    ///         using (var lck = await m_Lock.LockAsync())
    ///         {
    ///             // Synchronized logic here.
    ///         }
    ///     }
    /// };
    /// </summary>
    internal class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim m_Semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// This value is given to ScopedLock instances. This is used to prevent ScopedLock copies (as they are value types) from performing multiple
        /// releases as well as emitting an error.
        /// The value is intended to be updated and read from within the scope of the semaphore lock.
        /// </summary>
        private int m_LockInstanceId;

        public void Dispose()
        {
            m_Semaphore.Dispose();
        }

        /// <summary>
        /// Wait to enter the lock.
        /// </summary>
        /// <returns>A disposable object that releases the lock on disposal.</returns>
        /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
        public ScopedLock Lock()
        {
            m_Semaphore.Wait();
            return new ScopedLock(this, m_LockInstanceId);
        }

        /// <summary>
        /// Asynchronously wait to enter the lock.
        /// </summary>
        /// <returns>A disposable object that releases the lock on disposal.</returns>
        /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
        public async ValueTask<IDisposable> LockAsync()
        {
            await m_Semaphore.WaitAsync();
            return new ScopedLock(this, m_LockInstanceId);
        }

        /// <summary>
        /// Release the sempahore if the given ID matches the current lock instance.
        /// </summary>
        /// <param name="lockId">ID of a specific ScopedLock instance.</param>
        private void ReleaseInternal(int lockId)
        {
            int currentLockInstanceId = m_LockInstanceId;
            if (Interlocked.CompareExchange(ref m_LockInstanceId, currentLockInstanceId + 1, lockId) == lockId)
            {
                m_Semaphore.Release();
            }
#if DEBUG
            else
            {
                Debug.LogError("An instance of a ScopedLock was copied and redudantly Disposed. Only one instance should be disposed. This is safely handled, however it indicates there is a programming error.");
            }
#endif
        }

        /// <summary>
        /// Disposable struct that's returned by Lock and LockAsync, which releases the underlying semaphore upon disposal.
        /// Multiple disposal via copies is safe to perform with this implementation, but it produces an error to enforce a well-defined disposal pattern.
        /// </summary>
        public struct ScopedLock : IDisposable
        {
            private readonly AsyncLock m_Owner;
            private readonly int m_LockInstanceId;

            public ScopedLock(AsyncLock owner, int lockInstanceId)
            {
                m_Owner = owner;
                m_LockInstanceId = lockInstanceId;
            }

            public void Dispose()
            {
                m_Owner.ReleaseInternal(m_LockInstanceId);
            }
        }
    }
}
