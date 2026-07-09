using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.PlatformToolkit.PlayMode
{
    internal class PlayModeSavingSystem : ISavingSystem
    {
        private readonly IEnvironment m_Environment;
        private readonly GenericSavingSystem m_GenericSystem;
        private PlatformToolkitMetrics m_Metrics;
        int m_AccountId;

        public PlayModeSavingSystem(IEnvironment environment, int accountId, PlayModeSaveData saveData, PlatformToolkitMetrics metrics)
        {
            m_Environment = environment;
            m_Metrics = metrics;
            m_AccountId = accountId;
            m_GenericSystem = new(new PlayModeStorageSystem(environment, m_AccountId, saveData, m_Metrics), runInBackground: false);
        }

        public async Task<IReadOnlyList<string>> EnumerateSaveNames()
        {
            m_Metrics.LogStorageEnumerate(m_AccountId, null);
            await m_Environment.WaitIfPaused();
            return await m_GenericSystem.EnumerateSaveNames();
        }

        public async Task<ISaveReadable> OpenSaveReadable(string name)
        {
            m_Metrics.LogStorageOpenRead(m_AccountId, name);
            await m_Environment.WaitIfPaused();
            var readable = await m_GenericSystem.OpenSaveReadable(name);
            return readable;
        }

        public async Task<ISaveWritable> OpenSaveWritable(string name)
        {
            m_Metrics.LogStorageOpenWrite(m_AccountId, name);
            await m_Environment.WaitIfPaused();
            var writable = await m_GenericSystem.OpenSaveWritable(name);
            return writable;
        }

        public async Task<bool> SaveExists(string name)
        {
            m_Metrics.LogStorageEnumerate(m_AccountId, name); // Typically expected to be implemented as an enumerate on most platforms
            await m_Environment.WaitIfPaused();
            return await m_GenericSystem.SaveExists(name);
        }

        public async Task DeleteSave(string name)
        {
            await m_Environment.WaitIfPaused();
            await m_GenericSystem.DeleteSave(name);
        }
    }
}
