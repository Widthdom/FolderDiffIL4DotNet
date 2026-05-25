using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Represents the responsibility of writing a single report section to a <see cref="System.IO.StreamWriter"/>.
    /// Each implementation encapsulates section-specific writing logic
    /// and serves as an extension point when <see cref="ReportGenerateService"/> assembles sections.
    /// Implementations may be registered externally (e.g. via plugins) as well as internally.
    /// <para>
    /// レポートの 1 セクションを <see cref="System.IO.StreamWriter"/> へ書き込む責務を表します。
    /// 各実装はセクション固有の書き込みロジックをカプセル化し、
    /// <see cref="ReportGenerateService"/> がセクションを組み合わせる際の拡張点となります。
    /// 内部実装だけでなく、プラグイン等から外部登録することも可能です。
    /// </para>
    /// </summary>
    public interface IReportSectionWriter
    {
        /// <summary>
        /// Section display order. Sections with lower values are emitted first.
        /// セクションの表示順序。値が小さいセクションが先に出力されます。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns whether this section should be included for the current run.
        /// Allows sections to self-disable based on configuration or data availability.
        /// このセクションを現在の実行に含めるかどうかを返します。
        /// 設定やデータの有無に応じてセクション自身が無効化できます。
        /// </summary>
        /// <param name="context">Context holding all parameters needed for report generation. / レポート生成に必要なすべてのパラメータを保持するコンテキスト。</param>
        /// <returns><see langword="true"/> if the section should be written; otherwise <see langword="false"/>.</returns>
        bool IsEnabled(ReportWriteContext context);

        /// <summary>
        /// Writes the section content to <paramref name="writer"/>.
        /// セクションの内容を <paramref name="writer"/> へ書き込みます。
        /// </summary>
        /// <param name="writer">Target stream writer. / 出力先ストリームライター。</param>
        /// <param name="context">Context holding all parameters needed for report generation. / レポート生成に必要なすべてのパラメータを保持するコンテキスト。</param>
        void Write(StreamWriter writer, ReportWriteContext context);
    }
}
