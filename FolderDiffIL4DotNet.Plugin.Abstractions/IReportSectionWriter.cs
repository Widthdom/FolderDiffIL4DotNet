using System.IO;

namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Represents the responsibility of writing a single section to a Markdown diff report.
    /// Implementations may be provided by the host application or by external plugins.
    /// <para>
    /// Markdown 差分レポートの 1 セクションを書き込む責務を表します。
    /// 実装はホストアプリケーションまたは外部プラグインから提供できます。
    /// </para>
    /// </summary>
    public interface IPluginReportSectionWriter
    {
        /// <summary>
        /// Section display order. Sections with lower values are emitted first.
        /// Built-in sections use orders 100-1000 in increments of 100.
        /// セクションの表示順序。値が小さいセクションが先に出力されます。
        /// 組み込みセクションは 100 から 1000 まで 100 刻みで使用します。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns whether this section should be included for the current run.
        /// このセクションを現在の実行に含めるかどうかを返します。
        /// </summary>
        /// <param name="context">Plugin-visible report write context. / プラグインから参照可能なレポート書き込みコンテキスト。</param>
        /// <returns><see langword="true"/> if the section should be written.</returns>
        bool IsEnabled(IPluginReportWriteContext context);

        /// <summary>
        /// Writes the section content to the stream writer.
        /// セクションの内容をストリームライターへ書き込みます。
        /// </summary>
        /// <param name="writer">Target stream writer. / 出力先ストリームライター。</param>
        /// <param name="context">Plugin-visible report write context. / プラグインから参照可能なレポート書き込みコンテキスト。</param>
        void Write(StreamWriter writer, IPluginReportWriteContext context);
    }
}
