using System;
using System.Diagnostics;
using System.IO;
using FolderDiffIL4DotNet.Runner;

namespace FolderDiffIL4DotNet
{
    // Open-folder partial: handles --open-reports, --open-config, --open-logs commands.
    // フォルダ開放部分: --open-reports, --open-config, --open-logs コマンドを処理する。
    public sealed partial class ProgramRunner
    {
        private const string LOG_OPENING_FOLDER = "Opening folder: {0}";
        private const string ERROR_OPEN_FOLDER_FAILED = "Failed to open folder '{0}' ({1}): {2}";

        /// <summary>
        /// Handles the --open-reports, --open-config, and --open-logs commands.
        /// Returns the process exit code.
        /// --open-reports、--open-config、--open-logs コマンドを処理し、終了コードを返す。
        /// </summary>
        private int HandleOpenFolderCommands(CliOptions opts)
        {
            // Process each open-folder flag; multiple can be specified at once.
            // 各フォルダ開放フラグを処理する。複数同時指定可能。
            int result = 0;

            if (opts.OpenReports)
            {
                result = OpenFolder(() => !string.IsNullOrWhiteSpace(opts.OutputDirectory)
                    ? opts.OutputDirectory
                    : Path.Combine(AppContext.BaseDirectory, "Reports"));
                if (result != 0) return result;
            }

            if (opts.OpenConfig)
            {
                result = OpenFolder(() => !string.IsNullOrWhiteSpace(opts.ConfigPath)
                    ? Path.GetDirectoryName(Path.GetFullPath(opts.ConfigPath)) ?? AppContext.BaseDirectory
                    : AppContext.BaseDirectory);
                if (result != 0) return result;
            }

            if (opts.OpenLogs)
            {
                result = OpenFolder(() => Path.Combine(AppContext.BaseDirectory, "Logs"));
                if (result != 0) return result;
            }

            return result;
        }

        /// <summary>
        /// Opens the specified folder in the platform's default file manager.
        /// Creates the folder if it does not exist.
        /// 指定フォルダをプラットフォームのデフォルトファイルマネージャで開く。
        /// フォルダが存在しない場合は作成する。
        /// </summary>
        private int OpenFolder(Func<string> resolveFolderPath)
        {
            string folderPath = "(unresolved)";

            try
            {
                folderPath = Path.GetFullPath(resolveFolderPath());
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_OPENING_FOLDER, folderPath));
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                });
                return 0;
            }
            #pragma warning disable CA1031 // Application boundary: catch-all for platform-specific process launch failures
            catch (Exception ex)
            {
                Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, ERROR_OPEN_FOLDER_FAILED, folderPath, ex.GetType().Name, ex.Message));
                return (int)ProgramExitCode.ExecutionFailed;
            }
            #pragma warning restore CA1031
        }
    }
}
