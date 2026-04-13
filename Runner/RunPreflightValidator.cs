using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
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
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + " <oldFolderAbsolutePath> <newFolderAbsolutePath> [reportLabel] [options]";

        /// <summary>
        /// Validates minimum argument requirements (at least 2 non-empty arguments, plus optional non-empty report label).
        /// コマンドライン引数の最低要件（2 引数・非空、および省略可能だが指定時は非空のレポートラベル）を検証する。
        /// </summary>
        internal static void ValidateRequiredArguments(string[] args)
        {
            try
            {
                if (args == null || args.Length < 2)
                {
                    throw new ArgumentException(ERROR_INSUFFICIENT_ARGUMENTS);
                }

                if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
                {
                    throw new ArgumentException(ERROR_ARGUMENTS_NULL_OR_EMPTY);
                }

                if (args.Length >= 3 && string.IsNullOrWhiteSpace(args[2]))
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
        internal static void ValidateReportLabel(string reportLabel)
        {
            try
            {
                PathValidator.ValidateFolderNameOrThrow(reportLabel, nameof(reportLabel));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(
                    $"The value '{reportLabel}', provided as the third argument (reportLabel), is invalid as a folder name.",
                    nameof(reportLabel),
                    ex);
            }
        }

        /// <summary>
        /// Builds the absolute path to the report folder from the given report label.
        /// When <paramref name="outputDirectory"/> is specified, uses it as the base instead of the default Reports/ directory.
        /// レポートラベルからレポートフォルダの絶対パスを構築する。
        /// <paramref name="outputDirectory"/> が指定された場合、デフォルトの Reports/ ディレクトリの代わりにそのパスをベースとして使用する。
        /// </summary>
        internal static string GetReportsFolderAbsolutePath(string reportLabel, string? outputDirectory = null, ILoggerService? logger = null)
            => Path.Combine(GetReportsRootDirectoryAbsolutePath(outputDirectory, logger), reportLabel);

        /// <summary>
        /// Resolves the absolute Reports root directory and creates it if needed.
        /// Resolves to <c>&lt;exe&gt;/Reports</c> by default or to <paramref name="outputDirectory"/> when specified.
        /// Reports ルートディレクトリの絶対パスを解決し、必要なら作成する。
        /// 既定では <c>&lt;exe&gt;/Reports</c> を使用し、指定時は <paramref name="outputDirectory"/> を使用する。
        /// </summary>
        internal static string GetReportsRootDirectoryAbsolutePath(string? outputDirectory = null, ILoggerService? logger = null)
        {
            string reportsRootDirAbsolutePath;
            try
            {
                reportsRootDirAbsolutePath = !string.IsNullOrWhiteSpace(outputDirectory)
                    ? Path.GetFullPath(outputDirectory)
                    : Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
                PathValidator.ValidateAbsolutePathLengthOrThrow(reportsRootDirAbsolutePath, nameof(outputDirectory));
                Directory.CreateDirectory(reportsRootDirAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                logger?.LogMessage(AppLogLevel.Error,
                    $"Failed to resolve report output directory '{outputDirectory ?? REPORTS_ROOT_DIR_NAME}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                throw;
            }

            // Warn when custom output directory escapes application base directory
            // カスタム出力ディレクトリがアプリケーションベースディレクトリ外の場合に警告
            if (!string.IsNullOrWhiteSpace(outputDirectory) && logger != null)
            {
                WarnIfOutputEscapesAppBase(logger, reportsRootDirAbsolutePath);
                WarnIfSystemDirectory(logger, reportsRootDirAbsolutePath);
            }

            return reportsRootDirAbsolutePath;
        }

        /// <summary>
        /// Generates a timestamp-based report label and adds a numeric suffix when a collision already exists.
        /// Uses local time and keeps the label folder-name safe.
        /// ローカル時刻ベースのレポートラベルを生成し、既存フォルダと衝突する場合は数値サフィックスを付与する。
        /// フォルダ名として安全な形式を維持する。
        /// </summary>
        internal static string GenerateAutomaticReportLabel(string reportsRootDirAbsolutePath, DateTime? now = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reportsRootDirAbsolutePath);

            var timestamp = (now ?? DateTime.Now).ToString("yyyyMMdd_HHmmss_fffffff", CultureInfo.InvariantCulture);
            var candidate = timestamp;
            var suffix = 1;

            while (Directory.Exists(Path.Combine(reportsRootDirAbsolutePath, candidate)))
            {
                candidate = $"{timestamp}_{suffix:D2}";
                suffix++;
            }

            return candidate;
        }

        /// <summary>
        /// Returns the existing report subfolder names under the given Reports root directory.
        /// Existing report folder names are sorted case-insensitively and files are ignored.
        /// 指定した Reports ルートディレクトリ配下の既存レポートサブフォルダ名を返す。
        /// 既存レポートフォルダ名は大文字小文字を区別せずにソートし、通常ファイルは無視する。
        /// </summary>
        internal static string[] GetExistingReportFolderNames(string reportsRootDirAbsolutePath)
        {
            var folderNames = Directory.GetDirectories(reportsRootDirAbsolutePath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToArray();
            Array.Sort(folderNames, StringComparer.OrdinalIgnoreCase);
            return folderNames;
        }

        /// <summary>
        /// Writes the existing report subfolder names to standard output for copy/paste-friendly selection.
        /// Existing names are written one per line under a root-path header.
        /// 既存レポートサブフォルダ名を、コピーしやすいように標準出力へ一覧表示する。
        /// ルートパス見出しの下に既存名を1行ずつ出力する。
        /// </summary>
        internal static void WriteExistingReportFolderNamesToConsole(string reportsRootDirAbsolutePath, ILoggerService? logger = null)
        {
            try
            {
                var folderNames = GetExistingReportFolderNames(reportsRootDirAbsolutePath);
                Console.WriteLine($"Existing report folders under '{reportsRootDirAbsolutePath}':");
                if (folderNames.Length == 0)
                {
                    Console.WriteLine("(none)");
                }
                else
                {
                    foreach (var folderName in folderNames)
                    {
                        Console.WriteLine(folderName);
                    }
                }

                Console.WriteLine();
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                logger?.LogMessage(AppLogLevel.Warning,
                    $"Failed to list existing report folders under '{reportsRootDirAbsolutePath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        /// <summary>
        /// Logs a warning when the resolved output path is outside the application base directory.
        /// 解決された出力パスがアプリケーションベースディレクトリ外にある場合に警告をログ出力する。
        /// </summary>
        internal static void WarnIfOutputEscapesAppBase(ILoggerService logger, string resolvedOutputPath)
        {
            string appBase = Path.GetFullPath(AppContext.BaseDirectory);
            string normalizedOutput;
            try
            {
                normalizedOutput = Path.GetFullPath(resolvedOutputPath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                logger.LogMessage(AppLogLevel.Warning,
                    $"Skipped output-directory escape guardrail for '{resolvedOutputPath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return;
            }

            if (!IsSameOrChildPath(normalizedOutput, appBase))
            {
                logger.LogMessage(AppLogLevel.Warning,
                    $"Output directory '{normalizedOutput}' is outside the application base directory '{appBase}'. Verify this is intentional.",
                    shouldOutputMessageToConsole: true);
            }
        }

        /// <summary>
        /// Logs a warning when the output path targets a sensitive system directory.
        /// 出力パスが機密システムディレクトリを対象としている場合に警告をログ出力する。
        /// </summary>
        internal static void WarnIfSystemDirectory(ILoggerService logger, string resolvedOutputPath)
        {
            string normalizedOutput;
            try
            {
                normalizedOutput = Path.GetFullPath(resolvedOutputPath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                logger.LogMessage(AppLogLevel.Warning,
                    $"Skipped system-directory guardrail for '{resolvedOutputPath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return;
            }

            string[] systemDirs;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                systemDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                };
            }
            else
            {
                systemDirs = new[] { "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/etc", "/boot", "/proc", "/sys" };
            }

            foreach (var sysDir in systemDirs)
            {
                if (string.IsNullOrEmpty(sysDir))
                    continue;

                var normalizedSysDir = Path.GetFullPath(sysDir);
                if (IsSameOrChildPath(normalizedOutput, normalizedSysDir))
                {
                    logger.LogMessage(AppLogLevel.Warning,
                        $"Output directory '{normalizedOutput}' targets a system directory '{normalizedSysDir}'. This may be dangerous.",
                        shouldOutputMessageToConsole: true);
                    return;
                }
            }
        }

        internal static bool IsSameOrChildPath(string candidatePath, string parentPath)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var normalizedCandidate = TrimTrailingDirectorySeparators(Path.GetFullPath(candidatePath));
            var normalizedParent = TrimTrailingDirectorySeparators(Path.GetFullPath(parentPath));

            if (string.Equals(normalizedCandidate, normalizedParent, comparison))
            {
                return true;
            }

            return normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, comparison);
        }

        private static string TrimTrailingDirectorySeparators(string path)
            => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
                var reportsRootDirAbsolutePath = Path.GetDirectoryName(reportsFolderAbsolutePath);
                if (!string.IsNullOrWhiteSpace(reportsRootDirAbsolutePath))
                {
                    WriteExistingReportFolderNamesToConsole(reportsRootDirAbsolutePath, logger);
                }

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
            catch (UnauthorizedAccessException)
            {
                return; // Skip when drive information is inaccessible / ドライブ情報にアクセスできない場合はスキップ
            }

            long availableMb;
            try
            {
                availableMb = drive.AvailableFreeSpace / (1024L * 1024L);
            }
            catch (UnauthorizedAccessException)
            {
                return; // Skip when free-space query is denied / 空き容量照会が拒否された場合はスキップ
            }
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
            catch (UnauthorizedAccessException ex)
            {
                logger.LogMessage(
                    AppLogLevel.Error,
                    $"Write-permission probe failed on '{parent}': {ex.GetType().Name}: {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    exception: ex);
                throw new UnauthorizedAccessException(
                    $"The reports parent directory is not writable: '{parent}'. Ensure the process has write permission.",
                    ex);
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
