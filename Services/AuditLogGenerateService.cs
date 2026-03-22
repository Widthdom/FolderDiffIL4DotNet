using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Generates a structured JSON audit log (<c>audit_log.json</c>) alongside the diff reports.
    /// The audit log records every file comparison result, run metadata, summary statistics,
    /// and SHA256 integrity hashes of the generated reports for tamper detection.
    /// 差分レポートと合わせて構造化 JSON 監査ログ (<c>audit_log.json</c>) を生成するサービス。
    /// 監査ログにはファイルごとの比較結果、実行メタデータ、サマリー統計、
    /// および改竄検知用のレポート SHA256 ハッシュを記録します。
    /// </summary>
    public sealed class AuditLogGenerateService
    {
        internal const string AUDIT_LOG_FILE_NAME = "audit_log.json";

        private const string CATEGORY_ADDED = "Added";
        private const string CATEGORY_REMOVED = "Removed";
        private const string CATEGORY_MODIFIED = "Modified";
        private const string CATEGORY_UNCHANGED = "Unchanged";
        private const string CATEGORY_IGNORED = "Ignored";

        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;

        public AuditLogGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Generates audit_log.json using the specified <paramref name="context"/>.
        /// No-op when <see cref="IReadOnlyConfigSettings.ShouldGenerateAuditLog"/> is <see langword="false"/>.
        /// 指定された <paramref name="context"/> を使って audit_log.json を生成します。
        /// <see cref="IReadOnlyConfigSettings.ShouldGenerateAuditLog"/> が <see langword="false"/> の場合は何もしません。
        /// </summary>
        public void GenerateAuditLog(ReportGenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.Config.ShouldGenerateAuditLog) return;

            var auditLogPath = Path.Combine(context.ReportsFolderAbsolutePath, AUDIT_LOG_FILE_NAME);

            try
            {
                var record = BuildAuditLogRecord(
                    context.OldFolderAbsolutePath,
                    context.NewFolderAbsolutePath,
                    context.AppVersion,
                    context.ElapsedTimeString,
                    context.ComputerName,
                    context.ReportsFolderAbsolutePath);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(record, jsonOptions);
                PathValidator.ValidateAbsolutePathLengthOrThrow(auditLogPath);
                File.WriteAllText(auditLogPath, json, Encoding.UTF8);
                FileSystemUtility.TrySetReadOnly(auditLogPath);

                _logger.LogMessage(AppLogLevel.Info,
                    $"Audit log generated: {auditLogPath}",
                    shouldOutputMessageToConsole: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to write audit log to '{auditLogPath}': {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
        }

        internal AuditLogRecord BuildAuditLogRecord(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            string reportsFolderAbsolutePath)
        {
            var stats = _fileDiffResultLists.SummaryStatistics;
            var files = BuildFileEntries();

            var mdReportHash = ComputeFileHash(
                Path.Combine(reportsFolderAbsolutePath, "diff_report.md"));
            var htmlReportHash = ComputeFileHash(
                Path.Combine(reportsFolderAbsolutePath, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            var disassemblerAvailability = _fileDiffResultLists.DisassemblerAvailability?
                .Select(p => new AuditLogDisassemblerAvailability
                {
                    ToolName = p.ToolName,
                    Available = p.Available,
                    Version = p.Available && !string.IsNullOrWhiteSpace(p.Version) ? p.Version : string.Empty,
                })
                .ToList();

            return new AuditLogRecord
            {
                AppVersion = appVersion,
                ComputerName = computerName,
                OldFolderPath = oldFolderAbsolutePath,
                NewFolderPath = newFolderAbsolutePath,
                Timestamp = DateTimeOffset.Now.ToString("o"),
                ElapsedTime = elapsedTimeString,
                Summary = new AuditLogSummary
                {
                    Added = stats.AddedCount,
                    Removed = stats.RemovedCount,
                    Modified = stats.ModifiedCount,
                    Unchanged = stats.UnchangedCount,
                    Ignored = stats.IgnoredCount
                },
                DisassemblerAvailability = disassemblerAvailability,
                ReportSha256 = mdReportHash,
                HtmlReportSha256 = htmlReportHash,
                Files = files
            };
        }

        private List<AuditLogFileEntry> BuildFileEntries()
        {
            var entries = new List<AuditLogFileEntry>();

            // Added files
            foreach (var absPath in _fileDiffResultLists.AddedFilesAbsolutePath)
            {
                entries.Add(new AuditLogFileEntry
                {
                    RelativePath = Path.GetFileName(absPath),
                    Category = CATEGORY_ADDED
                });
            }

            // Removed files
            foreach (var absPath in _fileDiffResultLists.RemovedFilesAbsolutePath)
            {
                entries.Add(new AuditLogFileEntry
                {
                    RelativePath = Path.GetFileName(absPath),
                    Category = CATEGORY_REMOVED
                });
            }

            // Modified files
            foreach (var relPath in _fileDiffResultLists.ModifiedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .TryGetValue(relPath, out var detail) ? detail.ToString() : string.Empty;
                var disassembler = _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary
                    .TryGetValue(relPath, out var label) ? label : string.Empty;

                entries.Add(new AuditLogFileEntry
                {
                    RelativePath = relPath,
                    Category = CATEGORY_MODIFIED,
                    DiffDetail = diffDetail,
                    Disassembler = disassembler
                });
            }

            // Unchanged files
            foreach (var relPath in _fileDiffResultLists.UnchangedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .TryGetValue(relPath, out var detail) ? detail.ToString() : string.Empty;
                var disassembler = _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary
                    .TryGetValue(relPath, out var label) ? label : string.Empty;

                entries.Add(new AuditLogFileEntry
                {
                    RelativePath = relPath,
                    Category = CATEGORY_UNCHANGED,
                    DiffDetail = diffDetail,
                    Disassembler = disassembler
                });
            }

            // Ignored files
            foreach (var kvp in _fileDiffResultLists.IgnoredFilesRelativePathToLocation)
            {
                entries.Add(new AuditLogFileEntry
                {
                    RelativePath = kvp.Key,
                    Category = CATEGORY_IGNORED
                });
            }

            return entries.OrderBy(e => e.Category, StringComparer.Ordinal)
                          .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        }

        /// <summary>
        /// Computes the SHA256 hash of a file. Returns an empty string if the file does not exist.
        /// ファイルの SHA256 ハッシュを計算します。ファイルが存在しない場合は空文字を返します。
        /// </summary>
        internal static string ComputeFileHash(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
