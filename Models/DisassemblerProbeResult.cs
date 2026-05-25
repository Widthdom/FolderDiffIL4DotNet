namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Records the availability of a single disassembler tool as detected at startup.
    /// 起動時に検出された個々の逆アセンブラツールの利用可否を記録します。
    /// </summary>
    /// <param name="ToolName">Normalised display name (e.g. <c>dotnet-ildasm</c>, <c>ilspycmd</c>). / 正規化された表示名。</param>
    /// <param name="Available">Whether the tool was found and responded to <c>--version</c>. / ツールが見つかり <c>--version</c> に応答したか。</param>
    /// <param name="Version">Version string returned by the tool, or <see langword="null"/> when unavailable. / ツールが返したバージョン文字列。利用不可時は <see langword="null"/>。</param>
    /// <param name="Path">Resolved absolute path of the executable, or <see langword="null"/> when not found. / 解決された実行ファイルの絶対パス。見つからない場合は <see langword="null"/>。</param>
    public sealed record DisassemblerProbeResult(
        string ToolName,
        bool Available,
        string? Version,
        string? Path);
}
