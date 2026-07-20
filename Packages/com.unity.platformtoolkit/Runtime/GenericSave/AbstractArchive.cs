using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Unity.PlatformToolkit
{
    internal abstract class AbstractArchive : IGenericArchive
    {
        private bool m_OperationDataBuffering = true;

        // TODO: Remove this once all platforms are ready
        private readonly GenericSaveDataProvider m_DataProvider;

        public string Name { get; protected set; }

        public AbstractArchive(bool doDataBuffering)
        {
            m_OperationDataBuffering = doDataBuffering;
            if (m_OperationDataBuffering)
                m_DataProvider = new GenericSaveDataProvider();
        }

        public abstract Task<IReadOnlyList<string>> EnumerateFiles();

        public async Task<byte[]> ReadFile(string name)
        {
            return await GetDataFromStorage(name);
        }

        public Task WriteFile(string name, byte[] data)
        {
            if (m_OperationDataBuffering)
                m_DataProvider.WriteData(name, data);
            else
                WriteDataToStorage(name, data);

            return Task.CompletedTask;
        }

        public Task DeleteFile(string name)
        {
            if (m_OperationDataBuffering)
                m_DataProvider.DeleteData(name);

            // TODO Remove this call once CommitDataToStorage() deletes content for passed-in deleted files.
            RemoveDataFromStorage(name);
            return Task.CompletedTask;
        }

        public virtual async Task<bool> ContainsFile(string name)
        {
            var files = await EnumerateFiles();
            return files.Contains(name);
        }

        public async Task Commit()
        {
            if (m_OperationDataBuffering)
            {
                m_DataProvider.GetModifiedFiles(out var writtenFiles, out var deletedFiles);

                var files = await EnumerateFiles();
                var fileCountAfterCommit = writtenFiles.Count();

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (writtenFiles.All(wf => wf.name != file))
                            fileCountAfterCommit++;
                        if (deletedFiles.Contains(file))
                            fileCountAfterCommit--;
                    }
                }

                if (fileCountAfterCommit == 0)
                    throw new InvalidOperationException("Cannot commit empty save");

                foreach (var writtenFile in writtenFiles)
                {
                    // TODO Remove this call once CommitDataToStorage() writes content for passed-in written files.
                    await WriteDataToStorage(writtenFile.name, writtenFile.data);
                }
                await CommitDataToStorage(writtenFiles, deletedFiles);
            }
            else
            {
                await CommitDataToStorage(null, null);
            }
        }

        /// <summary>
        /// Returns data read from the given file.
        /// </summary>
        /// <param name="name">The name of the file to load</param>
        /// <returns>A non-null array of data read from the file</returns>
        /// <exception cref="T:System.IO.IOException">There was an error reading the data.</exception>
        /// <exception cref="T:System.IO.FileNotFoundException">Data for name is not found.</exception>
        protected abstract Task<byte[]> GetDataFromStorage(string name);
        protected abstract Task WriteDataToStorage(string name, byte[] data);

        /// <summary>
        /// Commit changed data.
        /// </summary>
        /// <param name="writtenFiles">Collection of all written files, their names and written data.</param>
        /// <param name="deletedFiles">Collection of deleted file names.</param>
        /// <returns>Task that represents the asynchronous commit operation.</returns>
        protected abstract Task CommitDataToStorage(
            IReadOnlyCollection<(string name, byte[] data)> writtenFiles,
            IReadOnlyCollection<string> deletedFiles
        );
        protected abstract void RemoveDataFromStorage(string name);

        public ValueTask DisposeAsync()
        {
            return DisposeInternalAsync(true);
        }

        public void Dispose()
        {
            DisposeInternal(true);
        }

        public virtual ValueTask DisposeInternalAsync(bool explicitDispose)
        {
            return new ValueTask();
        }

        public virtual void DisposeInternal(bool explicitDispose) { }

        public virtual void NotifyExplicitDispose() { }
    }
}
