using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Application;

namespace Unity.PlatformToolkit
{
    internal class GenericLocalStorageSystem : AbstractStorageSystem
    {
        private const string k_TempSaveSuffix = ".tmp";
        private const string k_BackupSaveSuffix = ".bac";

        protected readonly string m_SavesPath = Path.Combine(Application.persistentDataPath, "pt_saves");

        public GenericLocalStorageSystem(string pathOverride = null)
        {
            if (pathOverride != null)
                m_SavesPath = pathOverride;

            if (!Directory.Exists(m_SavesPath))
                Directory.CreateDirectory(m_SavesPath);
        }

        public override Task<IReadOnlyList<string>> EnumerateArchives()
        {
            var savePaths = Directory.GetFiles(m_SavesPath);

            var filteredCopy = new List<string>(savePaths.Length);
            foreach (var savePath in savePaths)
            {
                string filteredName;
                if (savePath.EndsWith(k_BackupSaveSuffix))
                {
                    var originalSavePath = savePath[..^k_BackupSaveSuffix.Length];
                    if (!File.Exists(originalSavePath))
                        filteredName = Path.GetFileName(originalSavePath);
                    else
                        continue;
                }
                else
                {
                    filteredName = Path.GetFileName(savePath);
                }

                if (SaveNameValidator.IsValidSaveName(filteredName))
                {
                    filteredCopy.Add(filteredName);
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(filteredCopy);
        }

        public override Task<IGenericArchive> GetReadOnlyArchive(string name)
        {
            GetPathsForSave(name, out var savePath, out var tempSavePath, out var backupSavePath);
            var saveExists = File.Exists(savePath);
            var tempSaveExists = File.Exists(tempSavePath);
            var backupSaveExists = File.Exists(backupSavePath);

            string savePathToOpen = savePath;

            if (saveExists)
            {
                if (tempSaveExists)
                    DeleteTempSaveIgnoreException(tempSavePath);
                if (backupSaveExists)
                    DeleteBackupSaveIgnoreException(backupSavePath);
            }
            else if (backupSaveExists)
            {
                if (tempSaveExists)
                    DeleteTempSaveIgnoreException(tempSavePath);

                try
                {
                    ExceptionTesting.TriggerException(ExceptionPoint.FailureToRestoreSaveBackup);
                    File.Move(backupSavePath, savePath);
                }
                catch
                {
                    savePathToOpen = backupSavePath;
                }
            }
            else if (tempSaveExists)
            {
                HandleTempSaveOnlyInReadonlyCase(tempSavePath);
            }

            var fileStream = new FileStream(savePathToOpen, FileMode.Open, FileAccess.Read);
            var archive = new SolidZipFileArchive(fileStream, name, false);
            archive.OnCleanup += Cleanup;
            return Task.FromResult<IGenericArchive>(archive);
        }

        public override Task<IGenericArchive> GetWriteOnlyArchive(string name)
        {
            GetPathsForSave(name, out var savePath, out var tempSavePath, out var backupSavePath);
            var saveExists = File.Exists(savePath);
            var tempSaveExists = File.Exists(tempSavePath);
            var backupSaveExists = File.Exists(backupSavePath);

            if (saveExists)
            {
                if (tempSaveExists)
                {
                    DeleteTempFileWithIOException(tempSavePath);
                }
                if (backupSaveExists)
                {
                    DeleteBackupSaveWithIOException(backupSavePath);
                }
            }
            else if (backupSaveExists)
            {
                if (tempSaveExists)
                    DeleteTempFileWithIOException(tempSavePath);
                RestoreBackupWithIOException(backupSavePath, savePath);
            }
            else if (tempSaveExists)
            {
                DeleteTempFileWithIOException(tempSavePath);
            }

            if (File.Exists(savePath))
                File.Copy(savePath, tempSavePath, true);

            var fileStream = new FileStream(tempSavePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var archive = new SolidZipFileArchive(fileStream, name, true);
            archive.OnCommit += Commit;
            archive.OnCleanup += Cleanup;
            return Task.FromResult<IGenericArchive>(archive);
        }

        private Task Commit(string name)
        {
            GetPathsForSave(name, out var savePath, out var tempSavePath, out var backupSavePath);

            ExceptionTesting.TriggerException(ExceptionPoint.PreCommit);

            try
            {
                if (File.Exists(savePath))
                {
                    ExceptionTesting.TriggerException(ExceptionPoint.FailureToMoveSavetoBackup);
                    File.Move(savePath, backupSavePath);
                }
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToMoveTempToSave);
                File.Move(tempSavePath, savePath);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                throw new IOException("Save operations could not be performed.");
            }

            DeleteBackupSaveIgnoreException(backupSavePath);

            return Task.CompletedTask;
        }

        private Task Cleanup(string name)
        {
            try
            {
                GetPathsForSave(name, out _, out var tempSavePath, out _);
                File.Delete(tempSavePath);
            }
            catch
            {
                // ignore
            }

            return Task.CompletedTask;
        }

        public override Task DeleteArchive(string name)
        {
            GetPathsForSave(name, out var savePath, out var tempSavePath, out var backupSavePath);

            // Deleting save and backup first, if any of these files remains then the save will get restored.
            // Failure to delete any of these files will cause an IOException.
            DeleteSaveAndBackupWithIOException(backupSavePath, savePath);

            // If only temp file remains, the save is ignored, so it's safe to ignore any failures here.
            DeleteTempSaveIgnoreException(tempSavePath);

            return Task.CompletedTask;
        }

        private string GetPathForSave(string saveName)
        {
            return Path.Combine(m_SavesPath, saveName);
        }

        private void GetPathsForSave(
            string saveName,
            out string savePath,
            out string tempSavePath,
            out string backupSavePath
        )
        {
            savePath = GetPathForSave(saveName);
            tempSavePath = $"{savePath}{k_TempSaveSuffix}";
            backupSavePath = $"{savePath}{k_BackupSaveSuffix}";
        }

        #region StateRestorationMethods

        private void DeleteBackupSaveIgnoreException(string backupSavePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToDeleteBackupSaveFile);
                File.Delete(backupSavePath);
            }
            catch
            {
                // ignored
            }
        }

        private void DeleteTempSaveIgnoreException(string tempSavePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToDeleteTemporarySaveFile);
                File.Delete(tempSavePath);
            }
            catch
            {
                // ignored
            }
        }

        private void DeleteTempFileWithIOException(string tempFilePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToDeleteTemporarySaveFile);
                File.Delete(tempFilePath);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new IOException("Temporary files from previous write operations could not be deleted.");
            }
        }

        private void DeleteBackupSaveWithIOException(string backupSavePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToDeleteBackupSaveFile);
                File.Delete(backupSavePath);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new IOException("Backup files from previous write operations could not be deleted.");
            }
        }

        private void HandleTempSaveOnlyInReadonlyCase(string tempSavePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToDeleteTemporarySaveFile);
                File.Delete(tempSavePath);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new FileNotFoundException(
                    "Temporary files from previous write operations could not be deleted, this prevents the save from being written."
                );
            }
            throw new FileNotFoundException();
        }

        private void DeleteSaveAndBackupWithIOException(string backupSavePath, string savePath)
        {
            try
            {
                File.Delete(backupSavePath);
                File.Delete(savePath);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new IOException("Could not delete save file.");
            }
        }

        private void RestoreBackupWithIOException(string backupSavePath, string savePath)
        {
            try
            {
                ExceptionTesting.TriggerException(ExceptionPoint.FailureToRestoreSaveBackup);
                File.Move(backupSavePath, savePath);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new IOException("Save backup could not be restored.");
            }
        }
        #endregion
    }
}
