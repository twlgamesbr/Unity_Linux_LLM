using System;
using System.Text.RegularExpressions;

namespace Unity.PlatformToolkit
{
    internal static class SaveNameValidator
    {
        internal const string kValidSaveNameRegexString = "^[\\-a-z0-9]+$";
        internal const string kValidFileNameRegexString = "^[\\-a-z0-9]+$";
        internal const string kMacOSFolder = "__MACOSX";

        public static bool IsValidFileName(string filename)
        {
            Regex rx = new(kValidFileNameRegexString);
            return rx.IsMatch(filename);
        }

        public static void CheckForMacOSFolderName(string filename)
        {
            if (filename.StartsWith(kMacOSFolder))
            {
                throw new ArgumentException(
                    $"When you import a save archive on macOS, ensure the archive doesn't contain meta files, such as those in the `__MACOSX` folder, "
                        + $"as these will cause the import to fail. To create a valid archive for import, run the `zip -r <filename>.zip .` command in the terminal, replacing `<filename>` with your desired archive name."
                );
            }
        }

        public static void CheckForFolderName(string filename)
        {
            // Check for a path seperator in the path, but allow one at the path root.
            if (filename.IndexOf('/', 1) != -1 || filename.IndexOf('\\', 1) != -1)
            {
                throw new ArgumentException(
                    $"{filename} is not a valid name for a file in a save game as it contains a directory path."
                );
            }
        }

        public static void CheckFileName(string filename)
        {
            if (filename == null)
                throw new ArgumentNullException(nameof(filename));

            if (!IsValidFileName(filename))
            {
                throw new ArgumentException(
                    $"{filename} is not a valid name for a file in a save game: it must match {kValidFileNameRegexString}."
                );
            }
        }

        public static bool IsValidSaveName(string savename)
        {
            Regex rx = new(kValidSaveNameRegexString);
            return rx.IsMatch(savename);
        }

        public static void CheckSaveName(string savename)
        {
            if (savename == null)
                throw new ArgumentNullException(nameof(savename));

            if (!IsValidSaveName(savename))
            {
                throw new ArgumentException(
                    $"{savename} is not a valid name for a save game: it must match {kValidSaveNameRegexString}."
                );
            }
        }
    }
}
