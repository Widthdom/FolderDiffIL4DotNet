using System;
using System.IO;

namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// Shared helpers for path-shape diagnostics used in warning/error messages.
    /// 警告/エラーメッセージで使うパス形状診断の共通 helper です。
    /// </summary>
    internal static class PathShapeDiagnostics
    {
        /// <summary>
        /// Returns true when the string looks like a path (rooted or contains a directory separator).
        /// Invalid path characters are treated as "not path-like" so diagnostic formatting never throws.
        /// 文字列が rooted、またはディレクトリ区切り文字を含み、パスらしく見える場合に true を返します。
        /// 無効なパス文字は「path-like ではない」として扱い、診断メッセージ整形で例外を起こさないようにします。
        /// </summary>
        internal static bool LooksLikePath(string? pathOrCommand)
        {
            if (string.IsNullOrWhiteSpace(pathOrCommand))
            {
                return false;
            }

            try
            {
                return Path.IsPathRooted(pathOrCommand)
                    || pathOrCommand.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || pathOrCommand.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
