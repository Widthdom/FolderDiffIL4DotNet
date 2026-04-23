using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet
{
    // Open-folder partial: handles --open-reports, --open-config, --open-logs commands.
    // フォルダ開放部分: --open-reports, --open-config, --open-logs コマンドを処理する。
    public sealed partial class ProgramRunner
    {
        private const string LOG_OPENING_FOLDER = "Opening folder: {0}";
        private const string ERROR_OPEN_FOLDER_FAILED = "Failed to open folder '{0}' during stage '{1}' ({2}): {3}";
        private const string ERROR_LOGGER_INIT_FOR_OPEN_FOLDER_FAILED = "Failed to initialize logger for folder-open command ({0}): {1}";

        /// <summary>
        /// Best-effort logger initialization for --open-* failure handling.
        /// These commands normally exit without touching the log directory, so bootstrap logging only runs after an open-folder failure.
        /// --open-* 失敗処理向けにロガーをベストエフォートで初期化します。
        /// これらのコマンドは通常、ログディレクトリに触れずに終了するため、bootstrap ログはフォルダオープン失敗後にのみ実行されます。
        /// </summary>
        #pragma warning disable CA1031 // Best-effort bootstrap must never leak and override the original open-folder failure.
        private bool TryInitializeLoggerForFolderOpen()
        {
            try
            {
                _logger.Initialize();
                return true;
            }
            catch (Exception ex)
            {
                WriteOpenFolderBootstrapError(ex);
                return false;
            }
        }
        #pragma warning restore CA1031

        private void WriteOpenFolderBootstrapError(Exception ex)
        {
            string message = string.Format(
                CultureInfo.InvariantCulture,
                ERROR_LOGGER_INIT_FOR_OPEN_FOLDER_FAILED,
                ex.GetType().Name,
                ex.Message);

            if (_logger.Format == LogFormat.Json)
            {
                Console.Error.WriteLine(JsonSerializer.Serialize(new
                {
                    level = "ERROR",
                    message,
                    exceptionType = ex.GetType().FullName,
                }));
                return;
            }

            Console.Error.WriteLine(message);
        }

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
                    : AppDataPaths.GetDefaultReportsRootDirectoryAbsolutePath());
                if (result != 0) return result;
            }

            if (opts.OpenConfig)
            {
                result = OpenFolder(() => !string.IsNullOrWhiteSpace(opts.ConfigPath)
                    ? Path.GetDirectoryName(Path.GetFullPath(opts.ConfigPath)) ?? AppDataPaths.GetDefaultConfigDirectoryAbsolutePath()
                    : AppDataPaths.GetDefaultConfigDirectoryAbsolutePath());
                if (result != 0) return result;
            }

            if (opts.OpenLogs)
            {
                result = OpenFolder(AppDataPaths.GetDefaultLogsDirectoryAbsolutePath);
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
            string openStage = "resolving target path";

            try
            {
                string rawFolderPath = resolveFolderPath();
                if (string.IsNullOrWhiteSpace(rawFolderPath))
                {
                    throw new InvalidOperationException("The target folder path resolved to an empty value.");
                }

                folderPath = Path.GetFullPath(rawFolderPath);
                PathValidator.ValidateAbsolutePathLengthOrThrow(folderPath, nameof(resolveFolderPath));

                openStage = "validating target path";
                if (File.Exists(folderPath))
                {
                    throw new IOException($"The target path exists as a file, not a directory: {folderPath}");
                }

                openStage = "creating target directory";
                Directory.CreateDirectory(folderPath);

                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_OPENING_FOLDER, folderPath));
                openStage = "launching file manager";
                _openFolderAction(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                });
                return 0;
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex)
                || ex is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
            {
                if (TryInitializeLoggerForFolderOpen())
                {
                    _logger.LogMessage(AppLogLevel.Error, string.Format(System.Globalization.CultureInfo.InvariantCulture, ERROR_OPEN_FOLDER_FAILED, folderPath, openStage, ex.GetType().Name, ex.Message), shouldOutputMessageToConsole: false, exception: ex);
                }

                Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, ERROR_OPEN_FOLDER_FAILED, folderPath, openStage, ex.GetType().Name, ex.Message));
                return (int)ProgramExitCode.ExecutionFailed;
            }
            #pragma warning disable CA1031 // Application boundary: catch-all for platform-specific process launch failures
            catch (Exception ex)
            {
                if (TryInitializeLoggerForFolderOpen())
                {
                    _logger.LogMessage(AppLogLevel.Error, string.Format(System.Globalization.CultureInfo.InvariantCulture, ERROR_OPEN_FOLDER_FAILED, folderPath, openStage, ex.GetType().Name, ex.Message), shouldOutputMessageToConsole: false, exception: ex);
                }

                Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, ERROR_OPEN_FOLDER_FAILED, folderPath, openStage, ex.GetType().Name, ex.Message));
                return (int)ProgramExitCode.ExecutionFailed;
            }
            #pragma warning restore CA1031
        }
    }
}
