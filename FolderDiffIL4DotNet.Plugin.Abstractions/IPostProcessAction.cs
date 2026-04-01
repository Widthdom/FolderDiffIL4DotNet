using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Action executed after all reports have been generated.
    /// Useful for notifications (Slack, Teams, email), uploading to external systems,
    /// or triggering downstream pipelines.
    /// <para>
    /// 全レポート生成後に実行されるアクション。
    /// 通知（Slack、Teams、メール）、外部システムへのアップロード、
    /// 下流パイプラインの起動などに使用します。
    /// </para>
    /// </summary>
    public interface IPostProcessAction
    {
        /// <summary>
        /// Execution order among post-process actions. Lower values execute first.
        /// ポストプロセスアクション間の実行順序。値が小さいほど先に実行。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Executes the post-process action.
        /// ポストプロセスアクションを実行します。
        /// </summary>
        /// <param name="context">Post-process context with run results. / 実行結果を含むポストプロセスコンテキスト。</param>
        /// <param name="cancellationToken">Cancellation token. / キャンセルトークン。</param>
        Task ExecuteAsync(PostProcessContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Context passed to <see cref="IPostProcessAction.ExecuteAsync"/>.
    /// <see cref="IPostProcessAction.ExecuteAsync"/> に渡されるコンテキスト。
    /// </summary>
    public sealed class PostProcessContext
    {
        /// <summary>Absolute path to the report output folder. / レポート出力先フォルダの絶対パス。</summary>
        public required string ReportsFolderAbsolutePath { get; init; }

        /// <summary>Absolute path to the baseline (old) folder. / 旧フォルダの絶対パス。</summary>
        public required string OldFolderAbsolutePath { get; init; }

        /// <summary>Absolute path to the comparison (new) folder. / 新フォルダの絶対パス。</summary>
        public required string NewFolderAbsolutePath { get; init; }

        /// <summary>Application version string. / アプリケーションバージョン文字列。</summary>
        public required string AppVersion { get; init; }

        /// <summary>Number of added files. / 追加されたファイル数。</summary>
        public int AddedCount { get; init; }

        /// <summary>Number of removed files. / 削除されたファイル数。</summary>
        public int RemovedCount { get; init; }

        /// <summary>Number of modified files. / 変更されたファイル数。</summary>
        public int ModifiedCount { get; init; }

        /// <summary>Number of unchanged files. / 未変更のファイル数。</summary>
        public int UnchangedCount { get; init; }

        /// <summary>Whether any SHA256 mismatch warnings exist. / SHA256 不一致警告が存在するかどうか。</summary>
        public bool HasSha256MismatchWarnings { get; init; }
    }
}
