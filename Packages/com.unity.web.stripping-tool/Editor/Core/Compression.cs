// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// An enum containing different compression types.
    /// This enum is used define what type of compression is used for resources.
    /// </summary>
    enum CompressionType
    {
        Disabled = 0,
        Gzip = 1,
        Brotli = 2,
    }

    /// <summary>
    /// Class <b>Compression</b> runs command line tools to compress and decompress files.
    /// </summary>
    class Compression
    {
        public class GzipException : System.Exception
        {
            public GzipException(string message)
                : base(message) { }
        }

        /// <summary>
        /// Returns the used build compression type based on the file extension, or, if the file is .unityweb,
        /// based on the Unity specific compression marker in the file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>Compression type</returns>
        public static CompressionType GetCompressionType(string filename)
        {
            if (IsBrotliCompressed(filename))
                return CompressionType.Brotli;
            if (IsGzipCompressed(filename))
                return CompressionType.Gzip;
            return CompressionType.Disabled;
        }

        /// <summary>
        /// Path to Brotli
        /// </summary>
        public string BrotliPath { get; set; } = "";

        /// <summary>
        /// Path to 7zip.
        /// </summary>
        public string SevenZipPath { get; set; } = "";

        public delegate void LogCallback(string text);

        /// <summary>
        /// A log callback that is called each time a new line is written to stdout.
        /// </summary>
        public LogCallback? Log = default;

        /// <summary>
        /// A log callback that is called each time a new line is written to stderr.
        /// </summary>
        public LogCallback? ErrorLog = default;

        internal const string k_CompressionMarkerBrotli = "UnityWeb Compressed Content (brotli)";
        internal const string k_CompressionMarkerGzip = "UnityWeb Compressed Content (gzip)";

        static readonly Regex k_SevenZipListRegex = new(
            @"(\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d)\s+([.]+)\s+(\d+)\s+(\d+)\s+([\w\d.]+)"
        );

        string BrotliExePath
        {
            get
            {
                return Path.Combine(
                    BrotliPath,
                    CommandLineUtils.HostPlatformPick("linux_x86_64", "macos", "macos", "win_x86_64"),
                    $"brotli{CommandLineUtils.HostPlatformExe}"
                );
            }
        }

        internal static bool IsCompressed(string file) => IsBrotliCompressed(file) || IsGzipCompressed(file);

        internal static bool IsBrotliCompressed(string file) =>
            HasFileExtension(file, ".br") || HasFileExtension(file, ".unityweb") && HasBrotliUnityMarker(file);

        internal static bool IsGzipCompressed(string file) =>
            HasFileExtension(file, ".gz") || HasFileExtension(file, ".unityweb") && HasGzipUnityMarker(file);

        static bool HasFileExtension(string filename, string ext) =>
            Path.GetFileName(filename).EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(filename)
                .EndsWith($"{ext}{FileBackup.BackupFileExtension}", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Compress a file with brotli compression.
        /// This will also set the Unity specific compression marker to allow "Decompression Fallback".
        /// </summary>
        /// <param name="inputFile">Path to the file to compress.</param>
        /// <param name="outputFile">Path to the compressed file.</param>
        public void CompressBrotli(string inputFile, string outputFile)
        {
            CommandLineUtils.Execute(
                BrotliExePath,
                $"-i \"{inputFile}\" -o \"{outputFile}\" --comment \"{k_CompressionMarkerBrotli}\"",
                GetCommandOptions()
            );
        }

        /// <summary>
        /// Decompress a brotli compressed file.
        /// </summary>
        /// <param name="inputFile">Path to the compressed file.</param>
        /// <param name="outputFile">Path to the decompressed file.</param>
        public void DecompressBrotli(string inputFile, string outputFile)
        {
            CommandLineUtils.Execute(
                BrotliExePath,
                $"--decompress -i \"{inputFile}\" -o \"{outputFile}\"",
                GetCommandOptions()
            );
        }

        /// <summary>
        /// Get list of files inside a gzip file.
        /// </summary>
        /// <param name="inputFile">Path to a gzip compressed file.</param>
        /// <returns>A list of files names inside gzip file.</returns>
        public List<string> GetFilesInGzip(string inputFile)
        {
            string stdout = "";
            string stderr = "";
            CommandLineUtils.Execute(
                SevenZipPath,
                $"l -ba \"{inputFile}\"",
                out stdout,
                out stderr,
                GetCommandOptions()
            );

            var files = new List<string>();
            var lines = stdout.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var columns = k_SevenZipListRegex.Match(line);
                if (!columns.Success)
                    continue;

                files.Add(columns.Groups[columns.Groups.Count - 1].Value);
            }

            return files;
        }

        /// <summary>
        /// Compress a single file with gzip compression.
        /// </summary>
        /// <param name="inputFile">Path to the file to compress.</param>
        /// <param name="outputFile">Path to the compressed file.</param>
        public void CompressGzip(string inputFile, string outputFile)
        {
            CommandLineUtils.Execute(SevenZipPath, $"a -tgzip \"{outputFile}\" \"{inputFile}\"", GetCommandOptions());
            AddGzipUnityMarker(outputFile);
        }

        /// <summary>
        /// Decompress a gzip file.
        /// </summary>
        /// <param name="inputFile">Path to the gzip compressed file.</param>
        /// <param name="outputFile">Path to the decompressed file.</param>
        /// <exception cref="GzipException">Is thrown when the file contains no file.</exception>
        public void DecompressGzip(string inputFile, string outputFile)
        {
            // Get file names in gzip file.
            // A Gzip should contain a single file.
            var files = GetFilesInGzip(inputFile);
            if (files.Count != 1)
                throw new GzipException("Invalid gzip file: no file found in archive.");

            // Unpack file to temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                CommandLineUtils.Execute(
                    SevenZipPath,
                    $"e -tgzip \"{inputFile}\" {files[0]} -o\"{tempDir}\"",
                    GetCommandOptions()
                );

                // Move file to final directory and rename it
                File.Move(Path.Combine(tempDir, files[0]), outputFile);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        internal static void AddGzipUnityMarker(string file)
        {
            var errorMessage = $"Cannot add gzip comment to {file}. Unexpected gzip file format.";
            var data = ReadFile(file);
            if (data.Length == 0)
                throw new GzipException($"Cannot add gzip comment to {file}. The file does not exist or it is empty.");

            int commentOffset = 10,
                commentLength = 0;
            if (commentOffset > data.Length)
                throw new GzipException(errorMessage);

            var flags = data[3];
            if ((flags & 0x04) != 0)
            {
                if (commentOffset + 2 > data.Length)
                    throw new GzipException(errorMessage);
                commentOffset += 2 + data[commentOffset] + (data[commentOffset + 1] << 8);
                if (commentOffset > data.Length)
                    throw new GzipException(errorMessage);
            }
            if ((flags & 0x08) != 0)
            {
                while (commentOffset < data.Length && data[commentOffset] != 0)
                    commentOffset++;
                if (commentOffset + 1 > data.Length)
                    throw new GzipException(errorMessage);
                commentOffset++;
            }
            if ((flags & 0x10) != 0)
            {
                while (commentOffset + commentLength < data.Length && data[commentOffset + commentLength] != 0)
                    commentLength++;
                if (commentOffset + commentLength + 1 > data.Length)
                    throw new GzipException(errorMessage);
                commentLength++;
            }
            using (var writer = new BinaryWriter(File.Open(file, FileMode.OpenOrCreate)))
            {
                data[3] |= 0x10;
                writer.Write(data, 0, commentOffset);
                writer.Write(Encoding.UTF8.GetBytes(k_CompressionMarkerGzip + '\0'));
                writer.Write(data, commentOffset + commentLength, data.Length - commentOffset - commentLength);
            }
        }

        private CommandLineUtils.CommandOptions GetCommandOptions()
        {
            var options = new CommandLineUtils.CommandOptions();
            if (Log != null)
            {
                options.Log += (text) => Log(text);
            }
            if (ErrorLog != null)
            {
                options.ErrorLog += (text) => ErrorLog(text);
            }

            return options;
        }

        // Converted from the code in UnityLoader.js
        internal static bool HasBrotliUnityMarker(string filename) => HasBrotliUnityMarker(ReadFile(filename));

        internal static bool HasBrotliUnityMarker(byte[] data)
        {
            if (data.Length == 0)
                return false;

            int wbitsLength = (data[0] & 0x01) != 0 ? ((data[0] & 0x0E) != 0 ? 4 : 7) : 1;
            int wbits = data[0] & ((1 << wbitsLength) - 1);
            int mskipbytes = 1 + (int)((Math.Log(k_CompressionMarkerBrotli.Length - 1) / Math.Log(2)) / 8);
            int commentOffset = (wbitsLength + 1 + 2 + 1 + 2 + (mskipbytes << 3) + 7) >> 3;
            if (wbits == 0x11 || commentOffset > data.Length)
                return false;

            int expectedCommentPrefix =
                wbits + ((3 << 1) + (mskipbytes << 4) + ((k_CompressionMarkerBrotli.Length - 1) << 6) << wbitsLength);
            for (int i = 0; i < commentOffset; i++, expectedCommentPrefix >>= 8)
            {
                if (data[i] != (expectedCommentPrefix & 0xFF))
                    return false;
            }

            var extractedComment = Encoding.UTF8.GetString(
                data.AsSpan(commentOffset, k_CompressionMarkerBrotli.Length)
            );
            return extractedComment == k_CompressionMarkerBrotli;
        }

        // Converted from the code in UnityLoader.js
        internal static bool HasGzipUnityMarker(string filename) => HasGzipUnityMarker(ReadFile(filename));

        internal static bool HasGzipUnityMarker(byte[] data)
        {
            if (data.Length == 0)
                return false;

            int commentOffset = 10;
            if (commentOffset > data.Length || data[0] != 0x1F || data[1] != 0x8B)
                return false;

            byte flags = data[3];
            if ((flags & 0x04) != 0)
            {
                if (commentOffset + 2 > data.Length)
                    return false;
                commentOffset += 2 + data[commentOffset] + (data[commentOffset + 1] << 8);
                if (commentOffset > data.Length)
                    return false;
            }

            if ((flags & 0x08) != 0)
            {
                while (commentOffset < data.Length && data[commentOffset] != 0)
                    commentOffset++;
                if (commentOffset + 1 > data.Length)
                    return false;
                commentOffset++;
            }

            if ((flags & 0x10) != 0)
            {
                var extractedComment = Encoding.UTF8.GetString(
                    data.AsSpan(commentOffset, k_CompressionMarkerGzip.Length + 1)
                );
                return extractedComment == k_CompressionMarkerGzip + "\0";
            }

            return false;
        }

        static byte[] ReadFile(string filename)
        {
            try
            {
                return File.ReadAllBytes(filename);
            }
            catch (Exception)
            {
                return Array.Empty<byte>();
            }
        }
    }
}
