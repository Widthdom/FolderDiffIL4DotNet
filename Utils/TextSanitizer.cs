using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// 文字列サニタイズおよびファイル名安全化を提供するクラス
    /// </summary>
    public static class TextSanitizer
    {
        private const int MAX_ASCII_CODE_POINT = 0x7F;
        private const int SAFE_FILENAME_HEAD_LENGTH = 40;
        private const int SAFE_FILENAME_TAIL_LENGTH = 8;
        private const int SAFE_FILENAME_HASH_BYTES = 6;
        private const int SAFE_FILENAME_DEFAULT_MAX_LENGTH = 180;
        /// <summary>
        /// サニタイズ(\\, /, :, ..を.に置換)返します。
        /// </summary>
        public static string Sanitize(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            var s = str.Replace('\\', '.').Replace('/', '.').Replace(':', '.');
            while (s.Contains(".."))
            {
                s = s.Replace("..", ".");
            }
            return s.Trim('.');
        }

        /// <summary>
        /// 任意の文字列を「ファイル名として安全に使える文字列」へ変換します。
        /// 無効文字およびコロン(:)は '_' に置換し、長すぎる場合は
        /// 「先頭<see cref="SAFE_FILENAME_HEAD_LENGTH"/> + "_.._" + 末尾<see cref="SAFE_FILENAME_TAIL_LENGTH"/> + '_' + 短縮ハッシュ(SHA1の先頭<see cref="SAFE_FILENAME_HASH_BYTES"/>バイト)」で短縮します。
        /// </summary>
        public static string ToSafeFileName(string fileNameExcludeExtention, int maxLength = SAFE_FILENAME_DEFAULT_MAX_LENGTH)
        {
            if (string.IsNullOrEmpty(fileNameExcludeExtention))
            {
                return fileNameExcludeExtention ?? string.Empty;
            }

            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var stringBuilder = new StringBuilder(fileNameExcludeExtention.Length);
            foreach (var ch in fileNameExcludeExtention)
            {
                if (ch == ':' || Array.IndexOf(invalidFileNameChars, ch) >= 0)
                {
                    stringBuilder.Append('_');
                }
                else
                {
                    stringBuilder.Append(ch);
                }
            }
            var sanitizedFileNameExcludeExtention = stringBuilder.ToString();

            if (sanitizedFileNameExcludeExtention.Length >= maxLength)
            {
                var hash = SHA1.HashData(Encoding.UTF8.GetBytes(sanitizedFileNameExcludeExtention));
                var hashHex = BitConverter.ToString(hash, 0, SAFE_FILENAME_HASH_BYTES).Replace("-", "").ToLowerInvariant();
                var head = sanitizedFileNameExcludeExtention[..Math.Min(SAFE_FILENAME_HEAD_LENGTH, sanitizedFileNameExcludeExtention.Length)];
                var tail = sanitizedFileNameExcludeExtention[^Math.Min(SAFE_FILENAME_TAIL_LENGTH, sanitizedFileNameExcludeExtention.Length)..];
                sanitizedFileNameExcludeExtention = head + "_.._" + tail + "_" + hashHex;
            }
            return sanitizedFileNameExcludeExtention;
        }

        /// <summary>
        /// 与えられた文字列に非ASCII文字（MAX_ASCII_CODE_POINT より大きいコードポイント）が含まれているかどうかを判定します。
        /// null/空文字列は false を返します。
        /// </summary>
        public static bool ContainsNonAscii(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }
            foreach (var ch in str)
            {
                if (ch > MAX_ASCII_CODE_POINT)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
