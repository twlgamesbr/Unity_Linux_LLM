using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.PlatformToolkit.PlayMode;

namespace Unity.PlatformToolkit
{
    internal class PlayModeSolidZipFileArchive : SolidZipFileArchive
    {
        private PlayModeSaveDataInfo m_playModeBoundInfo;
        private PlatformToolkitMetrics m_Metrics;
        private int m_AccountId;

        public PlayModeSolidZipFileArchive(
            Stream stream,
            string name,
            bool writable,
            PlayModeSaveDataInfo info,
            PlatformToolkitMetrics metrics,
            int accountId
        )
            : base(stream, name, writable)
        {
            m_playModeBoundInfo = info;
            m_playModeBoundInfo.Name = name;
            m_Metrics = metrics;
            m_AccountId = accountId;

            m_Metrics?.LogStorageOpenedDisposable(m_AccountId, name, this);
        }

        public override Task<IReadOnlyList<string>> EnumerateFiles()
        {
            m_Metrics?.LogStorageEnumerate(m_AccountId, m_playModeBoundInfo.Name);
            return base.EnumerateFiles();
        }

        protected override Task<byte[]> GetDataFromStorage(string name)
        {
            m_Metrics?.LogStorageRead(m_AccountId, $"{m_playModeBoundInfo.Name}: {name}");
            return base.GetDataFromStorage(name);
        }

        protected override Task WriteDataToStorage(string name, byte[] data)
        {
            m_Metrics?.LogStorageWrite(m_AccountId, $"{m_playModeBoundInfo.Name}: {name}");
            return base.WriteDataToStorage(name, data);
        }

        protected override void RemoveDataFromStorage(string name)
        {
            m_Metrics?.LogStorageDelete(m_AccountId, $"{m_playModeBoundInfo.Name}: {name}");
            base.RemoveDataFromStorage(name);
        }

        public override void DisposeInternal(bool explicitDispose)
        {
            if (explicitDispose)
                NotifyExplicitDispose();

            base.DisposeInternal(explicitDispose);
        }

        public override void NotifyExplicitDispose()
        {
            m_Metrics?.LogStorageDispose(this);
        }

        public override async ValueTask DisposeInternalAsync(bool explicitDispose)
        {
            if (explicitDispose)
                NotifyExplicitDispose();

            await base.DisposeInternalAsync(explicitDispose);
        }
    }
}
