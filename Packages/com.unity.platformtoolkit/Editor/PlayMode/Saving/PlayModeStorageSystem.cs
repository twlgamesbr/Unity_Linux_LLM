using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeStorageSystem : AbstractStorageSystem
    {
        private readonly IEnvironment m_Environment;
        private readonly int m_AccountId;
        private PlayModeSaveData m_SaveData;
        private PlatformToolkitMetrics m_Metrics;

        public PlayModeStorageSystem(
            IEnvironment environment,
            int accountId,
            PlayModeSaveData saveData,
            PlatformToolkitMetrics metrics
        )
        {
            m_Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            m_AccountId = accountId;
            m_SaveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
            m_Metrics = metrics;
        }

        public override Task<IReadOnlyList<string>> EnumerateArchives()
        {
            IReadOnlyList<string> names = m_SaveData.GetSaveNames();
            return Task.FromResult(names);
        }

        public override Task<IGenericArchive> GetReadOnlyArchive(string name)
        {
            byte[] data;
            try
            {
                data = m_SaveData.ReadSave(name);
            }
            catch (KeyNotFoundException)
            {
                throw new FileNotFoundException($"Save {name} does not exist and can't be opened in read only mode");
            }

            var dataStream = new MemoryStream(data, false);
            var saveInfo = m_SaveData.GetSaveInfo(name);
            return Task.FromResult<IGenericArchive>(
                new PlayModeSolidZipFileArchive(dataStream, name, false, saveInfo, m_Metrics, m_AccountId)
            );
        }

        public override async Task<IGenericArchive> GetWriteOnlyArchive(string name)
        {
            var dataStream = new MemoryStream();
            var saveInfo = new PlayModeSaveDataInfo();

            if (m_SaveData.ContainsSave(name))
            {
                await dataStream.WriteAsync(m_SaveData.ReadSave(name));
                saveInfo = m_SaveData.GetSaveInfo(name);
            }

            var archive = new PlayModeSolidZipFileArchive(dataStream, name, true, saveInfo, m_Metrics, m_AccountId);
            archive.OnCommit += name =>
            {
                m_Metrics?.LogStorageCommit(m_AccountId, name);

                if (m_Environment.FullStorage)
                    throw new IOException("There is not enough space on the disk");
                var dataToWrite = dataStream.ToArray();
                m_SaveData.WriteSave(name, dataToWrite, saveInfo);
                return Task.CompletedTask;
            };
            return archive;
        }

        public override Task DeleteArchive(string name)
        {
            m_Metrics?.LogStorageDelete(m_AccountId, name);

            m_SaveData.RemoveSave(name);
            return Task.CompletedTask;
        }
    }
}
