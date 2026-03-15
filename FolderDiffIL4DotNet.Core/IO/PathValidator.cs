using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// パスおよびフォルダ名の妥当性検証を提供するクラス
    /// </summary>
    public static class PathValidator
    {
        /// <summary>
        /// Windows の禁止記号群（\\ / : * ? " &lt; &gt; |）。これを上限集合として全OSに適用。
        /// 制御文字(0x00-0x1F)のチェックは別途実装側で行います。
        /// </summary>
        private static readonly char[] s_windowsInvalidFileNameChars = ['"', '<', '>', '|', '\\', '/', ':', '*', '?'];

        /// <summary>
        /// Windows の予約名（拡張子の有無を問わずNG）。
        /// </summary>
        private static readonly string[] s_windowsReservedNames =
        [
            "CON","PRN","AUX","NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        ];
        /// <summary>
        /// 許容される制御文字上限コードポイント。
        /// </summary>
        private const int MAX_CONTROL_CODE_POINT = 0x1F;

        /// <summary>
        /// Windows の通常パス長上限。
        /// </summary>
        private const int WINDOWS_DEFAULT_PATH_LIMIT = 260;

        /// <summary>
        /// Windows 拡張パス (\\?\) 使用時の最大長。
        /// </summary>
        private const int WINDOWS_EXTENDED_PATH_LIMIT = 32767;

        /// <summary>
        /// macOS の推奨最大パス長。
        /// </summary>
        private const int MACOS_PATH_LIMIT = 1024;

        /// <summary>
        /// Linux/Unix 系の一般的なパス長上限。
        /// </summary>
        private const int POSIX_PATH_LIMIT = 4096;

        private const string ERROR_FOLDER_NAME_EMPTY = "Folder name cannot be empty or whitespace.";
        private const string ERROR_FOLDER_NAME_DOT = "Folder name cannot be '.' or '..'.";
        private const string ERROR_FOLDER_NAME_END_SPACE = "Folder name cannot end with a space or a dot.";
        private const string ERROR_ABSOLUTE_PATH_NULL = "Absolute path cannot be null or whitespace.";
        /// <summary>
        /// フォルダ名として妥当か（macOS/Linux/Windowsの禁止事項を包括）検証し、問題があれば例外を投げます。
        /// - 空白や空、"."/".." は不可
        /// - 制御文字(0x00～0x1F、<see cref="MAX_CONTROL_CODE_POINT"/> 以下) や \ / : * ? " &lt; &gt; | を含むと不可
        /// - 末尾のスペースやドット不可
        /// - Windows予約名（<see cref="s_windowsReservedNames"/>: CON, PRN, AUX, NUL, COM1～COM9, LPT1～LPT9）は拡張子の有無に関わらず不可
        /// </summary>
        /// <param name="folderName">検証するフォルダ名</param>
        /// <param name="paramName">例外に含めるパラメータ名（省略可）</param>
        /// <exception cref="ArgumentException">フォルダ名が不正な場合</exception>
        public static void ValidateFolderNameOrThrow(string folderName, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_EMPTY, paramName ?? nameof(folderName));
            }

            // "." と ".." は不可
            if (folderName == "." || folderName == "..")
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_DOT, paramName ?? nameof(folderName));
            }

            // 制御文字 / 禁則文字のチェック
            foreach (var ch in folderName)
            {
                if (ch <= MAX_CONTROL_CODE_POINT || s_windowsInvalidFileNameChars.Contains(ch))
                {
                    throw new ArgumentException($"Folder name contains invalid character: '{ch}'.", paramName ?? nameof(folderName));
                }
            }

            // 末尾スペース/ドット不可（Windows仕様に合わせて厳格化）
            if (folderName.EndsWith(" ") || folderName.EndsWith("."))
            {
                throw new ArgumentException(ERROR_FOLDER_NAME_END_SPACE, paramName ?? nameof(folderName));
            }

            // Windows予約名チェック（拡張子が付いていてもNG）
            var trimmed = folderName.TrimEnd(' ', '.');
            var basePart = trimmed.Split('.')[0];
            var upper = basePart.ToUpperInvariant();
            if (s_windowsReservedNames.Contains(upper))
            {
                throw new ArgumentException($"Folder name '{folderName}' is a reserved name on Windows.", paramName ?? nameof(folderName));
            }
        }

        /// <summary>
        /// 絶対パスの長さがOSの上限を超えていないかを検証し、超過時は例外を投げます。
        /// 目安の上限値:
        /// - Windows: <see cref="WINDOWS_DEFAULT_PATH_LIMIT"/>（標準）/<see cref="WINDOWS_EXTENDED_PATH_LIMIT"/>（\\?\ プレフィックス利用時）
        /// - macOS:   <see cref="MACOS_PATH_LIMIT"/>
        /// - Linux:   <see cref="POSIX_PATH_LIMIT"/>
        /// 注意: 実際の制限は環境やAPIによって差異があります。本メソッドは一般的な安全域で検証します。
        /// また、与えられた文字列が「絶対パスかどうか」の検証は行いません（長さのみを検査）。
        /// </summary>
        /// <param name="absolutePath">検証対象の絶対パス</param>
        /// <param name="paramName">例外に含めるパラメータ名（省略可）</param>
        /// <exception cref="ArgumentException">絶対パスが空、または上限超過の場合</exception>
        public static void ValidateAbsolutePathLengthOrThrow(string absolutePath, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException(ERROR_ABSOLUTE_PATH_NULL, paramName ?? nameof(absolutePath));
            }

            int limit;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 拡張長パス（\\?\ または \\?\UNC\）は WINDOWS_EXTENDED_PATH_LIMIT 文字まで許容される
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
