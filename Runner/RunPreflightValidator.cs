using System;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// 実行前バリデーション（引数検証・ディレクトリ検証・ディスク容量確認）を担当する静的クラスです。
    /// </summary>
    internal static class RunPreflightValidator
    {
        private const string REPORTS_ROOT_DIR_NAME = "Reports";
        private const long PREFLIGHT_MIN_FREE_DISK_MEGABYTES = 100L;
        private const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";
        private const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + " <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [options]";

        /// <summary>
        /// コマンドライン引数の最低要件（3 引数・非空）を検証します。
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
        /// レポートラベルをフォルダ名として検証します。
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
        /// レポートラベルからレポートフォルダの絶対パスを構築します。
        /// </summary>
        internal static string GetReportsFolderAbsolutePath(string reportLabel)
        {
            string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
            Directory.CreateDirectory(reportsRootDirAbsolutePath);
            return Path.Combine(reportsRootDirAbsolutePath, reportLabel);
        }

        /// <summary>
        /// 旧フォルダ・新フォルダ・レポートフォルダのパスを検証します。
        /// </summary>
        internal static void ValidateRunDirectories(string oldFolderAbsolutePath, string newFolderAbsolutePath, string reportsFolderAbsolutePath)
        {
            // 1. パス長チェック（OS 制限を超えていないか）
            PathValidator.ValidateAbsolutePathLengthOrThrow(reportsFolderAbsolutePath, nameof(reportsFolderAbsolutePath));

            // 2. 旧/新フォルダの存在確認
            if (!Directory.Exists(oldFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The old folder path does not exist: {oldFolderAbsolutePath}");
            }

            if (!Directory.Exists(newFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The new folder path does not exist: {newFolderAbsolutePath}");
            }

            // 3. レポートフォルダが既に存在しないことの確認
            if (Directory.Exists(reportsFolderAbsolutePath))
            {
                throw new ArgumentException($"The report folder already exists: {reportsFolderAbsolutePath}. Provide a different report label.");
            }

            // 4. ディスク空き容量チェック（best-effort）
            CheckDiskSpaceOrThrow(reportsFolderAbsolutePath);

            // 5. レポート親ディレクトリへの書き込み権限チェック
            CheckReportsParentWritableOrThrow(reportsFolderAbsolutePath);
        }

        /// <summary>
        /// レポートフォルダを作成するドライブに十分な空き容量があることを確認します。
        /// ドライブ情報を取得できない場合（ネットワーク共有や仮想ドライブ等）は best-effort でスキップします。
        /// </summary>
        /// <param name="reportsFolderAbsolutePath">レポートフォルダの絶対パス。</param>
        /// <exception cref="IOException">空き容量が <see cref="PREFLIGHT_MIN_FREE_DISK_MEGABYTES"/> MB 未満の場合。</exception>
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
                return; // ドライブ情報を取得できない場合はスキップ
            }
            catch (IOException)
            {
                return; // ドライブが利用不可の場合はスキップ
            }

            long availableMb = drive.AvailableFreeSpace / (1024L * 1024L);
            if (availableMb < PREFLIGHT_MIN_FREE_DISK_MEGABYTES)
            {
                throw new IOException(
                    $"Insufficient disk space on '{drive.RootDirectory.FullName}': {availableMb} MB available, at least {PREFLIGHT_MIN_FREE_DISK_MEGABYTES} MB required.");
            }
        }

        /// <summary>
        /// レポートフォルダの親ディレクトリへの書き込み権限を一時プローブファイルで確認します。
        /// 親ディレクトリが存在しない場合は確認をスキップします。
        /// </summary>
        /// <param name="reportsFolderAbsolutePath">レポートフォルダの絶対パス。</param>
        /// <exception cref="UnauthorizedAccessException">書き込み権限がない場合。</exception>
        internal static void CheckReportsParentWritableOrThrow(string reportsFolderAbsolutePath)
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
            catch (IOException)
            {
                return; // I/O エラーはディスク容量チェック側で捕捉済みのためスキップ
            }
            finally
            {
                try
                {
                    File.Delete(probePath);
                }
                catch (IOException)
                {
                    // プローブファイルの削除失敗は best-effort
                }
                catch (UnauthorizedAccessException)
                {
                    // プローブファイルの削除失敗は best-effort
                }
            }
        }
    }
}
