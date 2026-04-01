using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Provides a custom disassembler for specific file types.
    /// Enables IL-level (or equivalent) comparison for non-.NET file types
    /// (e.g. Java .class via javap, Rust via LLVM IR, Python .pyc via dis).
    /// The built-in .NET disassembler (dotnet-ildasm/ilspycmd) is also registered
    /// through this interface.
    /// <para>
    /// 特定のファイル種別向けのカスタム逆アセンブラを提供します。
    /// .NET 以外のファイル種別に対する IL レベル（または同等の）比較を可能にします
    /// （例: javap による Java .class、LLVM IR による Rust、dis による Python .pyc）。
    /// 組み込みの .NET 逆アセンブラ（dotnet-ildasm/ilspycmd）もこのインターフェース経由で登録されます。
    /// </para>
    /// </summary>
    public interface IDisassemblerProvider
    {
        /// <summary>
        /// Priority for this provider. Lower values are tried first.
        /// If multiple providers can handle the same file, the lowest-priority one wins.
        /// このプロバイダの優先度。値が小さいほど先に試行されます。
        /// 同一ファイルを複数プロバイダが処理可能な場合、最も優先度の低いものが使用されます。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Display name of this disassembler (e.g. "dotnet-ildasm", "javap").
        /// Included in reports for traceability.
        /// この逆アセンブラの表示名（例: "dotnet-ildasm", "javap"）。
        /// 追跡のためレポートに含まれます。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Returns whether this provider can handle the given file.
        /// The file path is absolute.
        /// このプロバイダが指定ファイルを処理できるかどうかを返します。
        /// ファイルパスは絶対パスです。
        /// </summary>
        /// <param name="filePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <returns><see langword="true"/> if this provider can disassemble the file.</returns>
        bool CanHandle(string filePath);

        /// <summary>
        /// Disassembles the specified file and returns the textual representation.
        /// The output is used for line-by-line comparison with the counterpart file.
        /// 指定ファイルを逆アセンブルし、テキスト表現を返します。
        /// 出力は対応ファイルとの行単位比較に使用されます。
        /// </summary>
        /// <param name="filePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <param name="cancellationToken">Cancellation token. / キャンセルトークン。</param>
        /// <returns>Disassembly result. / 逆アセンブル結果。</returns>
        Task<DisassemblyResult> DisassembleAsync(string filePath, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Result of a disassembly operation.
    /// 逆アセンブル操作の結果。
    /// </summary>
    public sealed class DisassemblyResult
    {
        /// <summary>
        /// Whether disassembly succeeded.
        /// 逆アセンブルが成功したかどうか。
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// Disassembled text output (line-separated). Empty on failure.
        /// 逆アセンブルされたテキスト出力（行区切り）。失敗時は空。
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Command string used for disassembly (for logging/audit).
        /// 逆アセンブルに使用したコマンド文字列（ログ/監査用）。
        /// </summary>
        public string CommandString { get; init; } = string.Empty;

        /// <summary>
        /// Version label of the disassembler tool (e.g. "dotnet-ildasm (version: 8.0.0)").
        /// 逆アセンブラツールのバージョンラベル（例: "dotnet-ildasm (version: 8.0.0)"）。
        /// </summary>
        public string VersionLabel { get; init; } = string.Empty;
    }
}
