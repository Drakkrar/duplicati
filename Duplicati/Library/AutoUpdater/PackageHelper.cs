using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Duplicati.Library.Common;
using Duplicati.Library.Utility;

namespace Duplicati.Library.AutoUpdater
{
    /// <summary>
    /// Helper method for various packaging related settings
    /// </summary>
    public static class PackageHelper
    {
        /// <summary>
        /// The named executables that are installed via packages
        /// </summary>
        public enum NamedExecutable
        {
            /// <summary>
            /// The primary executable, with an embedded GUI
            /// </summary>
            TrayIcon,
            /// <summary>
            /// The primary commandline application
            /// </summary>
            CommandLine,
            /// <summary>
            /// The autoupdater
            /// </summary>
            AutoUpdater,
            /// <summary>
            /// The server runner
            /// </summary>
            Server,
            /// <summary>
            /// The windows service helper wrapping the server
            /// </summary>
            WindowsService,
            /// <summary>
            /// The backend manipulation tool
            /// </summary>
            BackendTool,
            /// <summary>
            /// The recovery tool
            /// </summary>
            RecoveryTool,
            /// <summary>
            /// The backend testing tool
            /// </summary>
            BackendTester,
            /// <summary>
            /// The SharpAESCrypt tool
            /// </summary>
            SharpAESCrypt,
            /// <summary>
            /// The snapshot tool
            /// </summary>
            Snapshots,
            /// <summary>
            /// The configuration importer
            /// </summary>
            ConfigurationImporter

        }

        /// <summary>
        /// Returns the operating system mappings of project executables
        /// </summary>
        /// <param name="exe">The executable to get then name for</param>
        /// <returns>The name of the executable on the current operating system</returns>
        /// <remarks>Note that the values here mirror the values in the ReleaseBuilder tool, so changes should be coordinated between the two</remarks>
        public static string GetExecutableName(NamedExecutable exe)
            => exe switch
            {
                NamedExecutable.TrayIcon => Platform.IsClientWindows ? "Duplicati.GUI.TrayIcon.exe" : "duplicati",
                NamedExecutable.CommandLine => Platform.IsClientWindows ? "Duplicati.CommandLine.exe" : "duplicati-cli",
                NamedExecutable.AutoUpdater => Platform.IsClientWindows ? "Duplicati.CommandLine.AutoUpdater.exe" : "duplicati-autoupdater",
                NamedExecutable.Server => Platform.IsClientWindows ? "Duplicati.Server.exe" : "duplicati-server",
                NamedExecutable.WindowsService => "Duplicati.WindowsServer.exe",
                NamedExecutable.BackendTool => Platform.IsClientWindows ? "Duplicati.CommandLine.BackendTool.exe" : "duplicati-backend-tool",
                NamedExecutable.RecoveryTool => Platform.IsClientWindows ? "Duplicati.CommandLine.RecoveryTool.exe" : "duplicati-recovery-tool",
                NamedExecutable.BackendTester => Platform.IsClientWindows ? "Duplicati.CommandLine.BackendTester.exe" : "duplicati-backend-tester",
                NamedExecutable.SharpAESCrypt => Platform.IsClientWindows ? "Duplicati.CommandLine.SharpAESCrypt.exe" : "duplicati-aescrypt",
                NamedExecutable.Snapshots => Platform.IsClientWindows ? "Duplicati.CommandLine.Snapshots.exe" : "duplicati-snapshots",
                NamedExecutable.ConfigurationImporter => Platform.IsClientWindows ? "Duplicati.CommandLine.ConfigurationImporter.exe" : "duplicati-configuration-importer",
                _ => throw new ArgumentException($"Named executable not known: {exe}", nameof(exe))
            };

    }
}