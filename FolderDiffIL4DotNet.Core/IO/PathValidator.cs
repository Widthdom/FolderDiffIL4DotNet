using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// Validates paths and folder names against OS-specific constraints.
    /// パスおよびフォルダ名の妥当性検証を提供するクラス。
    /// </summary>
    /// <remarks>
    /// <b>Security note:</b> <see cref="ValidateFolderNameOrThrow"/> is the primary defence against
    /// path-traversal attacks when a user-supplied string (such as a report label) is used to
    /// construct a file-system path.  The method rejects every character that could be used to
    /// escape a known base directory: directory separators (<c>/</c>, <c>\</c>), the dot-only
    /// names <c>.</c> and <c>..</c>, and control characters including the null byte.
    /// </remarks>
    public static class PathValidator
    {
        /// <summary>
        /// Windows invalid file-name characters applied as a superset for all OSes. Control characters (0x00-0x1F) are checked separately.
        /// Windows の禁止記号群を上限集合として全 OS に適用。制御文字チェックは別途実装。
        /// </summary>
        private static readonly char[] s_windowsInvalidFileNameChars = ['"', '<', '>', '|', '\\', '/', ':', '*', '?'];

        /// <summary>
        /// Windows reserved names (rejected regardless of extension).
        /// Windows の予約名（拡張子の有無を問わず NG）。
        /// </summary>
        private static readonly string[] s_windowsReservedNames =
        [
            "CON","PRN","AUX","NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        ];
        private const int MAX_CONTROL_CODE_POINT = 0x1F;
        private const int WINDOWS_DEFAULT_PATH_LIMIT = 260;
        private const int WINDOWS_EXTENDED_PATH_LIMIT = 32767;
        private const int MACOS_PATH_LIMIT = 1024;
        private const int POSIX_PATH_LIMIT = 4096;

        private const string ERROR_FOLDER_NAME_EMPTY = "Folder name cannot be empty or whitespace.";
        private const string ERROR_FOLDER_NAME_DOT = "Folder name cannot be '.' or '..'.";
        private const string ERROR_FOLDER_NAME_END_SPACE = "Folder name cannot end with a space or a dot.";
        private const string ERROR_ABSOLUTE_PATH_NULL = "Absolute path cannot be null or whitespace.";
        /// <summary>
        /// Validates a folder name against cross-platform rules (macOS/Linux/Windows) and throws on violation.
        /// Rejects: empty/whitespace, "."/"..," control chars, invalid filename chars, trailing space/dot, Windows reserved names.
        /// フォルダ名を全 OS 共通ルールで検証し、問題があれば例外を投げます。
        /// </summary>
        /// <exception cref="ArgumentException">フォルダ名が不正な場合</exception>
        public static void ValidateFolderNameOrThrow(string folderName, string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_EMPTY, paramName ?? nameof(folderName));
            }

            // Reject "." and ".."
            // "." と ".." は不可
            if (folderName == "." || folderName == "..")
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_DOT, paramName ?? nameof(folderName));
            }

            // Check for control characters and forbidden characters
            // 制御文字 / 禁則文字のチェック
            foreach (var ch in folderName)
            {
                if (ch <= MAX_CONTROL_CODE_POINT || s_windowsInvalidFileNameChars.Contains(ch))
                {
                    throw new ArgumentException($"Folder name contains invalid character: '{ch}'.", paramName ?? nameof(folderName));
                }
            }

            // Trailing space or dot is forbidden (strict Windows rules applied everywhere)
            // 末尾スペース/ドット不可（Windows 仕様に合わせて厳格化）
            if (folderName.EndsWith(" ") || folderName.EndsWith("."))
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_END_SPACE, paramName ?? nameof(folderName));
            }

            // Windows reserved-name check (rejected even with an extension)
            // Windows 予約名チェック（拡張子が付いていても NG）
            var trimmed = folderName.TrimEnd(' ', '.');
            var basePart = trimmed.Split('.')[0];
            var upper = basePart.ToUpperInvariant();
            if (s_windowsReservedNames.Contains(upper))
            {
                throw new ArgumentException($"Folder name '{folderName}' is a reserved name on Windows.", paramName ?? nameof(folderName));
            }
        }

        /// <summary>
        /// Validates that the absolute path length does not exceed the OS-specific limit; throws on violation.
        /// Note: does not verify whether the string is actually an absolute path -- only checks length.
        /// 絶対パスの長さが OS の上限を超えていないかを検証し、超過時は例外を投げます。
        /// 注意: パスが絶対かどうかの検証は行いません（長さのみ検査）。
        /// </summary>
        /// <exception cref="ArgumentException">絶対パスが空、または上限超過の場合</exception>
        public static void ValidateAbsolutePathLengthOrThrow(string absolutePath, string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException(ERROR_ABSOLUTE_PATH_NULL, paramName ?? nameof(absolutePath));
            }

            int limit;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Extended-length paths (\\?\ or \\?\UNC\) are allowed up to WINDOWS_EXTENDED_PATH_LIMIT
                // 拡張長パスは WINDOWS_EXTENDED_PATH_LIMIT 文字まで許容
                bool isExtended = absolutePath.StartsWith(@"\\?\", StringComparison.Ordinal);
                limit = isExtended ? WINDOWS_EXTENDED_PATH_LIMIT : WINDOWS_DEFAULT_PATH_LIMIT;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                limit = MACOS_PATH_LIMIT;
            }
            else // Linux その他POSIX想定
            {
                limit = POSIX_PATH_LIMIT;
            }

            if (absolutePath.Length > limit)
            {
                throw new ArgumentException($"Absolute path is too long for this OS (length {absolutePath.Length} > limit {limit}).", paramName ?? nameof(absolutePath));
            }
        }
    }
}
