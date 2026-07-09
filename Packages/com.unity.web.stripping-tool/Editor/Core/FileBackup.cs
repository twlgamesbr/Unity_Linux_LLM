// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System.IO;

namespace Unity.Web.Stripping.Editor
{
    internal class FileBackup
    {
        /// <summary>
        /// The file extension used for backup files.
        /// </summary>
        public const string BackupFileExtension = ".bak";

        /// <summary>
        /// Create a backup file by renaming the input file to "{inputFile}.bak"
        /// If "{inputFile}.bak" already exists the existing backup file will be replaced.
        /// </summary>
        /// <param name="inputFile">The path to the file to backup.</param>
        /// <param name="backupFile">Optional. If you don't want the back-up to be placed next to the original,
        /// use this to specify the full file path, with any file extension you might want to use, for the back-up.</param>
        /// <returns>Filepath to the backup file.</returns>
        public static string BackupFile(string inputFile, string? backupFile = null)
        {
            backupFile ??= $"{inputFile}{BackupFileExtension}";
            if (File.Exists(backupFile))
                File.Delete(backupFile);
            File.Move(inputFile, backupFile);

            return backupFile;
        }

        /// <summary>
        /// Restore a file from a backup.
        /// </summary>
        /// <param name="backupFile">Path to the backup file.</param>
        /// <param name="file">Path to the file.</param>
        /// <returns>True if file was restored, false otherwise.</returns>
        public static bool RestoreBackupFile(string backupFile, string file)
        {
            // Don't do anything if no backup file exists
            if (!File.Exists(backupFile))
                return false;

            // Replace file with backup file
            if (File.Exists(file))
                File.Delete(file);

            File.Move(backupFile, file);

            return true;
        }
    }
}
