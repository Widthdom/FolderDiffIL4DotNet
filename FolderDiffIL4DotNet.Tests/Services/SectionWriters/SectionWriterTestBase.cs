using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Base helper for IReportSectionWriter tests.
    /// Provides factory methods to create contexts and locate writers by Order value.
    /// IReportSectionWriter テスト用のベースヘルパー。
    /// コンテキスト生成や Order 値によるライター取得のファクトリメソッドを提供します。
    /// </summary>
    internal static class SectionWriterTestBase
    {
        /// <summary>
        /// Gets a section writer by its Order value from the built-in writers list.
        /// Order 値で組み込みライターリストからセクションライターを取得します。
        /// </summary>
        internal static IReportSectionWriter GetWriterByOrder(int order)
        {
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            return writers.First(w => w.Order == order);
        }

        /// <summary>
        /// Creates a minimal <see cref="ReportWriteContext"/> with default values.
        /// デフォルト値で最小限の <see cref="ReportWriteContext"/> を作成します。
        /// </summary>
        internal static ReportWriteContext CreateMinimalContext(
            bool hasSha256Mismatch = false,
            bool hasTimestampRegressionWarning = false,
            bool shouldIncludeIgnoredFiles = false,
            bool shouldIncludeUnchangedFiles = false,
            bool shouldIncludeILCacheStats = false)
        {
            var builder = new ConfigSettingsBuilder
            {
                ShouldIncludeIgnoredFiles = shouldIncludeIgnoredFiles,
                ShouldIncludeUnchangedFiles = shouldIncludeUnchangedFiles,
                ShouldIncludeILCacheStatsInReport = shouldIncludeILCacheStats
            };

            return new ReportWriteContext
            {
                OldFolderAbsolutePath = "/old",
                NewFolderAbsolutePath = "/new",
                AppVersion = "1.0.0-test",
                ElapsedTimeString = "0h 0m 1.0s",
                ComputerName = "TESTPC",
                Config = builder.Build(),
                HasSha256Mismatch = hasSha256Mismatch,
                HasTimestampRegressionWarning = hasTimestampRegressionWarning,
                IlCache = null,
                FileDiffResultLists = new FileDiffResultLists()
            };
        }

        /// <summary>
        /// Executes a section writer's Write method and returns the output as a string.
        /// セクションライターの Write メソッドを実行し、出力を文字列として返します。
        /// </summary>
        internal static string WriteToString(IReportSectionWriter writer, ReportWriteContext context)
        {
            using var ms = new MemoryStream();
            using var streamWriter = new StreamWriter(ms);
            writer.Write(streamWriter, context);
            streamWriter.Flush();
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            return reader.ReadToEnd();
        }
    }
}
