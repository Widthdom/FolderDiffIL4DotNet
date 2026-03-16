using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// レポートの 1 セクションを <see cref="System.IO.StreamWriter"/> へ書き込む責務を表します。
    /// <para>
    /// 各実装はセクション固有の書き込みロジックをカプセル化し、
    /// <see cref="ReportGenerateService"/> がセクションを組み合わせる際の拡張点となります。
    /// </para>
    /// </summary>
    internal interface IReportSectionWriter
    {
        /// <summary>
        /// セクションの内容を <paramref name="writer"/> へ書き込みます。
        /// </summary>
        /// <param name="writer">出力先ストリームライター。</param>
        /// <param name="context">レポート生成に必要なすべてのパラメータを保持するコンテキスト。</param>
        void Write(StreamWriter writer, ReportWriteContext context);
    }
}
