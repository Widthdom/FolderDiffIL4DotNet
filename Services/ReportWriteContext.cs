using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Context object passed to <see cref="IReportSectionWriter.Write"/>,
    /// aggregating all parameters needed for section-level report writing.
    /// <see cref="IReportSectionWriter"/> の <c>Write</c> メソッドに渡すレポート生成コンテキスト。
    /// セクション単位の書き込みに必要なすべてのパラメータを 1 か所に集約します。
    /// </summary>
    internal sealed class ReportWriteContext
    {
        public string OldFolderAbsolutePath { get; init; } = null!;
        public string NewFolderAbsolutePath { get; init; } = null!;
        public string AppVersion { get; init; } = null!;
        public string ElapsedTimeString { get; init; } = null!;
        public string ComputerName { get; init; } = null!;
        public ConfigSettings Config { get; init; } = null!;
        public bool HasMd5Mismatch { get; init; }
        public bool HasTimestampRegressionWarning { get; init; }
        public ILCache? IlCache { get; init; }
        public FileDiffResultLists FileDiffResultLists { get; init; } = null!;
    }
}
