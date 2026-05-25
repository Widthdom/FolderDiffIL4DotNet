using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Hook executed before and/or after each file comparison.
    /// Allows plugins to inject custom logic into the comparison pipeline
    /// (e.g. semantic XML comparison for .csproj, custom diff for .resx).
    /// <para>
    /// 各ファイル比較の前後に実行されるフック。
    /// プラグインが比較パイプラインにカスタムロジックを注入可能にします
    /// （例: .csproj のセマンティック XML 比較、.resx のカスタム差分）。
    /// </para>
    /// </summary>
    public interface IFileComparisonHook
    {
        /// <summary>
        /// Execution order among hooks. Lower values execute first.
        /// フック間の実行順序。値が小さいほど先に実行。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Called before the standard comparison for a file pair.
        /// Return a non-null <see cref="FileComparisonHookResult"/> to override the built-in comparison;
        /// return <see langword="null"/> to let the built-in pipeline proceed.
        /// ファイルペアの標準比較前に呼ばれます。
        /// 非null の <see cref="FileComparisonHookResult"/> を返すと組み込み比較をオーバーライドします。
        /// <see langword="null"/> を返すと組み込みパイプラインが続行します。
        /// </summary>
        /// <param name="context">File comparison context. / ファイル比較コンテキスト。</param>
        /// <param name="cancellationToken">Cancellation token. / キャンセルトークン。</param>
        /// <returns>
        /// A hook result to override comparison, or <see langword="null"/> to defer to built-in logic.
        /// 比較をオーバーライドするフック結果、または組み込みロジックに委譲する場合は <see langword="null"/>。
        /// </returns>
        Task<FileComparisonHookResult?> BeforeCompareAsync(FileComparisonHookContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Called after the comparison for a file pair completes.
        /// Allows plugins to enrich results, record metrics, or trigger side effects.
        /// ファイルペアの比較完了後に呼ばれます。
        /// プラグインが結果を拡充したり、メトリクスを記録したり、副作用を起動できます。
        /// </summary>
        /// <param name="context">File comparison context. / ファイル比較コンテキスト。</param>
        /// <param name="areEqual">Whether the files were determined to be equal. / ファイルが同一と判定されたかどうか。</param>
        /// <param name="cancellationToken">Cancellation token. / キャンセルトークン。</param>
        Task AfterCompareAsync(FileComparisonHookContext context, bool areEqual, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Context passed to <see cref="IFileComparisonHook"/> methods.
    /// <see cref="IFileComparisonHook"/> メソッドに渡されるコンテキスト。
    /// </summary>
    public sealed class FileComparisonHookContext
    {
        /// <summary>Relative path of the file being compared. / 比較中のファイルの相対パス。</summary>
        public required string FileRelativePath { get; init; }

        /// <summary>Absolute path to the old folder root. / 旧フォルダルートの絶対パス。</summary>
        public required string OldFolderAbsolutePath { get; init; }

        /// <summary>Absolute path to the new folder root. / 新フォルダルートの絶対パス。</summary>
        public required string NewFolderAbsolutePath { get; init; }
    }

    /// <summary>
    /// Result returned by <see cref="IFileComparisonHook.BeforeCompareAsync"/> to override built-in comparison.
    /// <see cref="IFileComparisonHook.BeforeCompareAsync"/> が組み込み比較をオーバーライドするために返す結果。
    /// </summary>
    public sealed class FileComparisonHookResult
    {
        /// <summary>Whether the hook determined the files to be equal. / フックがファイルを同一と判定したかどうか。</summary>
        public required bool AreEqual { get; init; }

        /// <summary>
        /// Optional diff detail label (e.g. "XMLSemanticMatch"). Logged and included in reports.
        /// 差分詳細ラベル（例: "XMLSemanticMatch"）。ログとレポートに含まれます。省略可。
        /// </summary>
        public string? DiffDetailLabel { get; init; }
    }
}
