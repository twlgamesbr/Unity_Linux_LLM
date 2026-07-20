using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.PlatformToolkit
{
    internal interface IGenericArchive : IAsyncDisposable, IDisposable
    {
        string Name { get; }

        Task<IReadOnlyList<string>> EnumerateFiles();
        Task<byte[]> ReadFile(string name);
        Task WriteFile(string name, byte[] data);
        Task DeleteFile(string name);
        Task<bool> ContainsFile(string name);

        Task Commit();

        /// <summary>
        /// Dispose to be called by internal systems.
        /// </summary>
        /// <param name="explicitDispose">True if user initiated, false if system initiated.</param>
        void DisposeInternal(bool explicitDispose);

        /// <summary>
        /// Async Dispose to be called by internal systems.
        /// </summary>
        /// <param name="explicitDispose">True if user initiated, false if system initiated.</param>
        ValueTask DisposeInternalAsync(bool explicitDispose);

        /// <summary>
        /// Called when the user explicitly closes a handle, even if the archive was already internally disposed.
        /// </summary>
        void NotifyExplicitDispose();
    }
}
