using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.SharpZipLib.Zip;
using UnityEngine.Assertions;

namespace Unity.PlatformToolkit
{
    internal class SolidZipFileArchive : AbstractArchive
    {
        public event Func<string, Task> OnCommit;
        public event Func<string, Task> OnCleanup;

        private ZipFile m_Archive;
        private Stream m_Stream;
        private readonly bool m_Writable;

        public SolidZipFileArchive(Stream stream, string name, bool writable)
            : base(doDataBuffering: false)
        {
            Name = name;
            m_Writable = writable;
            m_Stream = stream;

            try
            {
                m_Archive = new ZipFile(m_Stream, false);
            }
            catch (ZipException e)
            {
                m_Stream?.Dispose();
                m_Stream = null;
                throw new IOException(e.Message, e);
            }
        }

        ~SolidZipFileArchive()
        {
            if (m_Stream != null)
            {
                UnityEngine.Debug.LogError("SolidZipFileArchive was not disposed correctly and is leaking a stream.");
                m_Stream.Dispose();
            }
        }

        public override Task<IReadOnlyList<string>> EnumerateFiles()
        {
            Assert.IsFalse(m_Writable);

            List<string> result = new();
            for (int entryIndex = 0; entryIndex < m_Archive.Count; ++entryIndex)
            {
                if (!m_Archive[entryIndex].IsDirectory)
                {
                    result.Add(m_Archive[entryIndex].Name);
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        public override Task<bool> ContainsFile(string name)
        {
            Assert.IsFalse(m_Writable);
            return Task.FromResult(m_Archive.GetEntry(name) != null);
        }

        protected override async Task<byte[]> GetDataFromStorage(string name)
        {
            var entry = m_Archive.GetEntry(name);
            if (entry == null)
            {
                throw new FileNotFoundException($"Could not get data for file {name} in archive {Name}.");
            }

            var data = await ReadDataFromArchiveEntry(entry);
            return data;
        }


        protected override Task WriteDataToStorage(string name, byte[] data)
        {
            var existingEntry = m_Archive.GetEntry(name);

            MemoryStream inputStream = new MemoryStream(data, writable: false);

            m_Archive.BeginUpdate();
            if (existingEntry != null)
            {
                m_Archive.Delete(existingEntry);
            }
            m_Archive.Add(new MemoryDataSource(inputStream), name, CompressionMethod.Stored);
            m_Archive.CommitUpdate();
            return Task.CompletedTask;
        }

        protected override void RemoveDataFromStorage(string name)
        {
            if (m_Archive.GetEntry(name) != null)
            {
                m_Archive.BeginUpdate();
                m_Archive.Delete(name);
                m_Archive.CommitUpdate();
            }
        }

        protected override async Task CommitDataToStorage(IReadOnlyCollection<(string name, byte[] data)> writtenFiles, IReadOnlyCollection<string> deletedFiles)
        {
            if (m_Archive.Count == 0)
                throw new InvalidOperationException("Cannot commit empty save");

            m_Archive.Close();
            m_Archive = null;
            m_Stream.Close();
            await OnCommit.Invoke(Name);
        }

        public override void DisposeInternal(bool explicitDispose)
        {
            m_Archive?.Close();
            m_Stream?.Dispose();

            OnCleanup?.Invoke(Name);

            OnCleanup = null;
            m_Archive = null;
            m_Stream = null;
        }

        public override async ValueTask DisposeInternalAsync(bool explicitDispose)
        {
            if (m_Archive != null)
            {
                m_Archive.IsStreamOwner = false; // Prevent the archive disposing the stream since we want to do it async
                m_Archive.Close();
            }

            if (m_Stream != null)
            {
                await m_Stream.DisposeAsync();
            }

            var cleanUpTask = OnCleanup?.Invoke(Name);
            if (cleanUpTask != null)
                await cleanUpTask;

            OnCleanup = null;
            m_Archive = null;
            m_Stream = null;
        }

        private async Task<byte[]> ReadDataFromArchiveEntry(ZipEntry entry)
        {
            var size = entry.Size;
            await using var stream = m_Archive.GetInputStream(entry);
            var data = new byte[size];
            var read = stream.Read(data);
            if (read != size)
            {
                throw new IOException($"Didn't read enough bytes for file {entry.Name} in archive {Name}");
            }
            return data;
        }

        private class MemoryDataSource : IStaticDataSource
        {
            private Stream m_Stream;
            public Stream GetSource()
            {
                return m_Stream;
            }

            public MemoryDataSource(Stream inputStream)
            {
                m_Stream = inputStream;
                m_Stream.Position = 0;
            }
        }
    }
}
