// NOTE: generic code, do not use UnityEngine here
#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// A class that is used to execute a command.
    /// </summary>
    internal class CommandLineUtils
    {
        /// <summary>
        /// Exception that is thrown if wasm-opt command fails.
        /// </summary>
        public class CommandException : System.Exception
        {
            /// <summary>
            /// The name of the command
            /// </summary>
            public string Command { get; internal set; }

            /// <summary>
            /// Command line arguments passed to the command.
            /// </summary>
            public string Arguments { get; internal set; }

            /// <summary>
            /// Exit code of the command
            /// </summary>
            public int ExitCode { get; internal set; }

            /// <summary>
            /// Output of stderr.
            /// </summary>
            public string ErrorMessage { get; internal set; }

            /// <summary>
            /// Output of stdout.
            /// </summary>
            public string Output { get; internal set; }

            public CommandException(string command, string arguments, int exitCode, string errorMessage, string output)
                : base(
                    $"An error occurred when running '{command} {arguments}'.\nExit code: {exitCode}.\nError message: {errorMessage}.\nOutput: {output}"
                )
            {
                this.Command = command;
                this.Arguments = arguments;
                this.ExitCode = exitCode;
                this.ErrorMessage = errorMessage;
                this.Output = output;
            }
        }

        /// <summary>
        /// Choose one of the values depending on the current OS and architecture.
        /// </summary>
        /// <param name="linux">This value is returned when the code is run on Linux.</param>
        /// <param name="macosx64">This value is returned when the code is run on an Intel-based Mac.</param>
        /// <param name="macosarm64">This value is returned when the code is run on an ARM-based Mac.</param>
        /// <param name="windows">This value is returned when the code is run on Windows.</param>
        /// <returns>One of the input value depending on the current OS and architecture.</returns>
        public static T HostPlatformPick<T>(T linux, T macosx64, T macosarm64, T windows)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X86:
                    case Architecture.X64:
                        return macosx64;
                    case Architecture.Arm:
                    case Architecture.Arm64:
                        return macosarm64;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return linux;
            }

            // Unity Editor should only run on Windows, MacOS or Linux
            throw new PlatformNotSupportedException(
                $"Platform \"{RuntimeInformation.OSDescription}\" is not supported."
            );
        }

        /// <summary>
        /// Get extension of executable depending on platform.
        /// </summary>
        public static string HostPlatformExe
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ".exe";
                }

                return "";
            }
        }

        /// <summary>
        /// Options object for running commands.
        /// </summary>
        public class CommandOptions
        {
            public delegate void LogCallback(string text);

            /// <summary>
            /// A log callback that is called each time a new line is written to stdout.
            /// </summary>
            public LogCallback? Log = default;

            /// <summary>
            /// A log callback that is called each time a new line is written to stderr.
            /// </summary>
            public LogCallback? ErrorLog = default;
        }

        /// <summary>
        /// Execute a command and wait for it's completion.
        /// </summary>
        /// <param name="command">Name of path to the command to run.</param>
        /// <param name="arguments">Command line arguments for the command.</param>
        /// <param name="options">Optional: Additional options for configuring log output.</param>
        /// <exception cref="CommandException">
        /// Thrown if the command execution fails.
        /// </exception>
        public static void Execute(string command, string arguments, CommandOptions? options = null)
        {
            string stdout = "";
            string stderror = "";
            Execute(command, arguments, out stdout, out stderror, options);
        }

        /// <summary>
        /// Execute a command and wait for it's completion.
        /// The outputs stdout and stderr will be written to <paramref name="stdout"/> and <paramref name="stderror"/>.
        /// </summary>
        /// <param name="command">Name of path to the command to run.</param>
        /// <param name="arguments">Command line arguments for the command.</param>
        /// <param name="stdout">Output: The stdout of the command.</param>
        /// <param name="stderror">Output: The stderr of the command.</param>
        /// <param name="options">Optional: Additional options for configuring log output.</param>
        /// <exception cref="CommandException">
        /// Thrown if the command execution fails.
        /// </exception>
        public static void Execute(
            string command,
            string arguments,
            out string stdout,
            out string stderror,
            CommandOptions? options = null
        )
        {
            if (options == null)
                options = new CommandOptions();

            if (options.Log != null)
                options.Log($"{command} {arguments}");

            var proc = new Process();
            proc.StartInfo.FileName = command;
            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            var output = new StringBuilder();
            var errorMessage = new StringBuilder();
            proc.OutputDataReceived += (sender, data) =>
            {
                if (options.Log != null && data.Data != null)
                    options.Log(data.Data);
                output.AppendLine(data.Data);
            };
            proc.ErrorDataReceived += (sender, data) =>
            {
                if (options.ErrorLog != null && data.Data != null)
                    options.ErrorLog(data.Data);
                errorMessage.AppendLine(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            var exitCode = proc.ExitCode;
            proc.Close();

            if (exitCode != 0)
            {
                throw new CommandException(command, arguments, exitCode, errorMessage.ToString(), output.ToString());
            }

            stdout = output.ToString();
            stderror = errorMessage.ToString();
        }
    }
}
