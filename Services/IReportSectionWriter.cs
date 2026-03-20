using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Represents the responsibility of writing a single report section to a <see cref="System.IO.StreamWriter"/>.
    /// Each implementation encapsulates section-specific writing logic
    /// and serves as an extension point when <see cref="ReportGenerateService"/> assembles sections.
    /// <para>
    /// レポートの 1 セクションを <see cref="System.IO.StreamWriter"/> へ書き込む責務を表します。
    /// 各実装はセクション固有の書き込みロジックをカプセル化し、
    /// <see cref="ReportGenerateService"/> がセクションを組み合わせる際の拡張点となります。
    /// </para>
    /// </summary>
    internal interface IReportSectionWriter
    {
        /// <summary>
        /// Writes the section content to <paramref name="writer"/>.
        /// セクションの内容を <paramref name="writer"/> へ書き込みます。
        /// </summary>
        /// <param name="writer">Target stream writer. / 出力先ストリームライター。</param>
        /// <param name="context">Context holding all parameters needed for report generation. / レポート生成に必要なすべてのパラメータを保持するコンテキスト。</param>
        void Write(StreamWriter writer, ReportWriteContext context);
    }
}
