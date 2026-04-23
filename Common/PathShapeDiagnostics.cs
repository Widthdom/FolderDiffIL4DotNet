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
        /// Returns the rooted-state string used in diagnostic messages.
        /// Invalid path characters are treated as unknown so diagnostic formatting never throws.
        /// 診断メッセージで使う rooted 状態文字列を返します。
        /// 無効なパス文字は unknown として扱い、診断メッセージ整形で例外を起こさないようにします。
        /// </summary>
        internal static string DescribeRootedState(string? pathOrCommand)
        {
            if (string.IsNullOrWhiteSpace(pathOrCommand))
            {
                return "Unknown";
            }

            if (pathOrCommand.IndexOf('\0') >= 0)
            {
                return "Unknown";
            }

            try
            {
                return Path.IsPathRooted(pathOrCommand).ToString();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return "Unknown";
            }
        }

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

            if (pathOrCommand.IndexOf('\0') >= 0)
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

        /// <summary>
        /// Builds a reusable diagnostic fragment that reports rooted and path-like state.
        /// rooted/path-like 状態をまとめて報告する再利用可能な診断フラグメントを構築します。
        /// </summary>
        internal static string DescribeState(string label, string? pathOrCommand)
            => $"{label}IsPathRooted={DescribeRootedState(pathOrCommand)}, {label}LooksPathLike={LooksLikePath(pathOrCommand)}";
    }
}
