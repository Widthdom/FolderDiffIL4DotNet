using System;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Handles pre-run validation: argument checks, directory verification, and disk-space confirmation.
    /// 実行前バリデーション（引数検証・ディレクトリ検証・ディスク容量確認）を担当する静的クラス。
    /// </summary>
    internal static class RunPreflightValidator
    {
        private const string REPORTS_ROOT_DIR_NAME = "Reports";
        private const long PREFLIGHT_MIN_FREE_DISK_MEGABYTES = 100L;
        private const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";
        private const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + " <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [options]";

        /// <summary>
        /// Validates minimum argument requirements (at least 3 non-empty arguments).
        /// コマンドライン引数の最低要件（3 引数・非空）を検証する。
        /// </summary>
        internal static void ValidateRequiredArguments(string[] args)
        {
            try
            {
                if (args == null || args.Length < 3)
                {
                    throw new ArgumentException(ERROR_INSUFFICIENT_ARGUMENTS);
                }

                if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                {
                    throw new ArgumentException(ERROR_ARGUMENTS_NULL_OR_EMPTY);
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ERROR_INVALID_ARGUMENTS_USAGE, ex);
            }
        }

        /// <summary>
        /// Validates that the report label is a legal folder name.
        /// レポートラベルをフォルダ名として検証する。
        /// </summary>
        internal static void ValidateReportLabel(ILoggerService logger, string reportLabel)
        {
            try
            {
                PathValidator.ValidateFolderNameOrThrow(reportLabel, nameof(reportLabel));
            }
            catch (ArgumentException)
            {
                logger.LogMessage(AppLogLevel.Error, $"The value '{reportLabel}', provided as the third argument (reportLabel), is invalid as a folder name.", shouldOutputMessageToConsole: true);
                throw;
            }
        }

        /// <summary>
        /// Builds the absolute path to the report folder from the given report label.
        /// レポートラベルからレポートフォルダの絶対パスを構築する。
        /// </summary>
        internal static string GetReportsFolderAbsolutePath(string reportLabel)
        {
            string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
            Directory.CreateDirectory(reportsRootDirAbsolutePath);
            return Path.Combine(reportsRootDirAbsolutePath, reportLabel);
        }

        /// <summary>
        /// Validates the old-folder, new-folder, and report-folder paths.
        /// 旧フォルダ・新フォルダ・レポートフォルダのパスを検証する。
        /// </summary>
        internal static void ValidateRunDirectories(ILoggerService logger, string oldFolderAbsolutePath, string newFolderAbsolutePath, string reportsFolderAbsolutePath)
        {
            // 1. Path-length check (must not exceed OS limit)
            // パス長チェック（OS 制限を超えていないか）
            PathValidator.ValidateAbsolutePathLengthOrThrow(reportsFolderAbsolutePath, nameof(reportsFolderAbsolutePath));

            // 2. Verify old/new folders exist
            // 旧/新フォルダの存在確認
            if (!Directory.Exists(oldFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The old folder path does not exist: {oldFolderAbsolutePath}");
            }

            if (!Directory.Exists(newFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The new folder path does not exist: {newFolderAbsolutePath}");
            }

            // 3. Ensure report folder does not already exist
            // レポートフォルダが既に存在しないことの確認
            if (Directory.Exists(reportsFolderAbsolutePath))
            {
                throw new ArgumentException($"The report folder already exists: {reportsFolderAbsolutePath}. Provide a different report label.");
            }

            // 4. Disk free-space check (best-effort)
            // ディスク空き容量チェック（best-effort）
            CheckDiskSpaceOrThrow(reportsFolderAbsolutePath);

            // 5. Write-permission check on the reports parent directory
            // レポート親ディレクトリへの書き込み権限チェック
            CheckReportsParentWritableOrThrow(logger, reportsFolderAbsolutePath);
        }

        /// <summary>
        /// Verifies sufficient free space on the drive where the report folder will be created.
        /// Skipped on a best-effort basis when drive info is unavailable (e.g. network shares, virtual drives).
        /// レポートフォルダを作成するドライブに十分な空き容量があることを確認する。
        /// ドライブ情報を取得できない場合（ネットワーク共有や仮想ドライブ等）は best-effort でスキップする。
        /// </summary>
        internal static void CheckDiskSpaceOrThrow(string reportsFolderAbsolutePath)
        {
            DriveInfo drive;
            try
            {
                var root = Path.GetPathRoot(reportsFolderAbsolutePath);
                if (string.IsNullOrEmpty(root))
                {
                    return;
                }

                drive = new DriveInfo(root);
            }
            catch (ArgumentException)
            {
                return; // Skip when drive info is unavailable / ドライブ情報を取得できない場合はスキップ
            }
            catch (IOException)
            {
                return; // Skip when drive is unavailable / ドライブが利用不可の場合はスキップ
            }

            long availableMb = drive.AvailableFreeSpace / (1024L * 1024L);
            if (availableMb < PREFLIGHT_MIN_FREE_DISK_MEGABYTES)
            {
                throw new IOException(
                    $"Insufficient disk space on '{drive.RootDirectory.FullName}': {availableMb} MB available, at least {PREFLIGHT_MIN_FREE_DISK_MEGABYTES} MB required.");
            }
        }

        /// <summary>
        /// Checks write permission on the reports parent directory by creating a temporary probe file.
        /// Skipped if the parent directory does not exist.
        /// Logs and re-throws all I/O errors with cause-specific messages to enable fail-fast diagnostics.
        /// レポートフォルダの親ディレクトリへの書き込み権限を一時プローブファイルで確認する。
        /// 親ディレクトリが存在しない場合は確認をスキップする。
        /// すべての I/O エラーを原因別メッセージでログ出力し、fail-fast のため再スローする。
        /// </summary>
        internal static void CheckReportsParentWritableOrThrow(ILoggerService logger, string reportsFolderAbsolutePath)
        {
            var parent = Path.GetDirectoryName(reportsFolderAbsolutePath);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                return;
            }

            var probePath = Path.Combine(parent, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(probePath, Array.Empty<byte>());
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException(
                    $"The reports parent directory is not writable: '{parent}'. Ensure the process has write permission.");
            }
            catch (IOException ex)
            {
                // Log the original cause and fail fast instead of silently returning.
                // 原因を記録して、サイレントリターンではなく fail-fast する。
                logger.LogMessage(
                    AppLogLevel.Error,
                    $"Write-permission probe failed on '{parent}': {ex.GetType().Name}: {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    exception: ex);
                throw new IOException(
                    $"Cannot write to the reports parent directory '{parent}': {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    File.Delete(probePath);
                }
                catch (IOException)
                {
                    // Probe file deletion is best-effort / プローブファイルの削除失敗は best-effort
                }
                catch (UnauthorizedAccessException)
                {
                    // Probe file deletion is best-effort / プローブファイルの削除失敗は best-effort
                }
            }
        }
    }
}
