namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Warning info for a Modified file whose new-side timestamp is older than its old-side timestamp.
    /// Modified と判定されたファイルについて、new 側の更新日時が old 側より古い場合の警告情報。
    /// </summary>
    public sealed record FileTimestampRegressionWarning(string FileRelativePath, string OldTimestamp, string NewTimestamp);
}
