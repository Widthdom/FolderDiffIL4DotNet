using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// <see cref="IReportSectionWriter"/> の <c>Write</c> メソッドに渡すレポート生成コンテキスト。
    /// セクション単位の書き込みに必要なすべてのパラメータを 1 か所に集約します。
    /// </summary>
    internal sealed class ReportWriteContext
    {
        /// <summary>旧フォルダの絶対パス。</summary>
        public string OldFolderAbsolutePath { get; init; }

        /// <summary>新フォルダの絶対パス。</summary>
        public string NewFolderAbsolutePath { get; init; }

        /// <summary>アプリケーションバージョン。</summary>
        public string AppVersion { get; init; }

        /// <summary>経過時間文字列（null 可）。</summary>
        public string ElapsedTimeString { get; init; }

        /// <summary>実行コンピュータ名。</summary>
        public string ComputerName { get; init; }

        /// <summary>設定オブジェクト。</summary>
        public ConfigSettings Config { get; init; }

        /// <summary>MD5 ハッシュ不一致が 1 件以上あるかどうか。</summary>
        public bool HasMd5Mismatch { get; init; }

        /// <summary>new 側のタイムスタンプが old より古いファイルが存在するかどうか。</summary>
        public bool HasTimestampRegressionWarning { get; init; }

        /// <summary>IL キャッシュインスタンス（null の場合は IL Cache Stats セクションをスキップ）。</summary>
        public ILCache IlCache { get; init; }

        /// <summary>差分比較結果を保持するオブジェクト。</summary>
        public FileDiffResultLists FileDiffResultLists { get; init; }
    }
}
