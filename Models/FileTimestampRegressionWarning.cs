namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Modified と判定されたファイルについて、new 側の更新日時が old 側より古い場合の警告情報。
    /// </summary>
    /// <param name="FileRelativePath">ファイルの相対パス。</param>
    /// <param name="OldTimestamp">old 側の更新日時文字列。</param>
    /// <param name="NewTimestamp">new 側の更新日時文字列。</param>
    public sealed record FileTimestampRegressionWarning(string FileRelativePath, string OldTimestamp, string NewTimestamp);
}
