using System;
using System.IO;
using System.Text;

namespace FolderDiffIL4DotNet.Core.Text
{
    /// <summary>
    /// Detects file encoding by inspecting BOM and validating UTF-8 byte sequences.
    /// Falls back to the system ANSI code page (e.g. Shift_JIS on Japanese Windows) for non-UTF-8 files.
    /// BOM 検査と UTF-8 バイト列検証によりファイルエンコーディングを自動検出します。
    /// UTF-8 でないファイルにはシステム ANSI コードページ（日本語 Windows では Shift_JIS 等）へフォールバックします。
    /// </summary>
    public static class EncodingDetector
    {
        /// <summary>
        /// Default number of bytes to sample for encoding detection.
        /// エンコーディング検出に使用するサンプルバイト数の既定値。
        /// </summary>
        private const int DEFAULT_SAMPLE_SIZE = 8192;

        /// <summary>
        /// Ensures <see cref="CodePagesEncodingProvider"/> is registered so that legacy
        /// code pages (e.g. Shift_JIS / cp932) are available at runtime.
        /// Call once at application startup.
        /// <see cref="CodePagesEncodingProvider"/> を登録し、レガシーコードページ
        /// （Shift_JIS / cp932 等）が実行時に利用可能になるようにします。
        /// アプリケーション起動時に一度だけ呼び出してください。
        /// </summary>
        public static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Detects the encoding of the specified file.
        /// 指定ファイルのエンコーディングを検出します。
        /// </summary>
        /// <param name="filePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <returns>The detected <see cref="Encoding"/>. / 検出された <see cref="Encoding"/>。</returns>
        public static Encoding DetectFileEncoding(string filePath)
        {
            return DetectFileEncoding(filePath, DEFAULT_SAMPLE_SIZE);
        }

        /// <summary>
        /// Detects the encoding of the specified file using the given sample size.
        /// 指定サンプルサイズでファイルエンコーディングを検出します。
        /// </summary>
        /// <param name="filePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <param name="sampleSize">Maximum number of bytes to read for detection. / 検出に読み込む最大バイト数。</param>
        /// <returns>The detected <see cref="Encoding"/>. / 検出された <see cref="Encoding"/>。</returns>
        internal static Encoding DetectFileEncoding(string filePath, int sampleSize)
        {
            byte[] sample;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int bytesToRead = (int)Math.Min(sampleSize, fs.Length);
                sample = new byte[bytesToRead];
                int totalRead = 0;
                while (totalRead < bytesToRead)
                {
                    int read = fs.Read(sample, totalRead, bytesToRead - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead < bytesToRead)
                {
                    Array.Resize(ref sample, totalRead);
                }
            }

            return DetectEncodingFromBytes(sample);
        }

        /// <summary>
        /// Detects encoding from a byte array (BOM check, then UTF-8 validation, then ANSI fallback).
        /// バイト配列からエンコーディングを検出します（BOM → UTF-8 検証 → ANSI フォールバック）。
        /// </summary>
        internal static Encoding DetectEncodingFromBytes(byte[] bytes)
        {
            // BOM detection / BOM 検出
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode; // UTF-16 LE
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode; // UTF-16 BE
            }

            // UTF-8 validation: try strict decode / UTF-8 検証: 厳密デコードを試行
            if (IsValidUtf8(bytes))
            {
                return Encoding.UTF8;
            }

            // Fallback to system ANSI code page (e.g. Shift_JIS / cp932 on Japanese Windows)
            // システム ANSI コードページへフォールバック（日本語 Windows では Shift_JIS / cp932）
            try
            {
                int ansiCodePage = GetAnsiCodePage();
                if (ansiCodePage > 0)
                {
                    return Encoding.GetEncoding(ansiCodePage);
                }
            }
#pragma warning disable CA1031 // Best-effort fallback; if ANSI code page lookup fails, return UTF-8. / ベストエフォートのフォールバック; ANSI コードページ取得失敗時は UTF-8 を返す。
            catch (Exception)
            {
                // Encoding not available even with CodePages registered; fall through to UTF-8
                // CodePages 登録済みでもエンコーディングが利用不可; UTF-8 にフォールスルー
            }
#pragma warning restore CA1031

            return Encoding.UTF8;
        }

        /// <summary>
        /// Validates whether the byte array is valid UTF-8.
        /// バイト配列が有効な UTF-8 であるか検証します。
        /// </summary>
        private static bool IsValidUtf8(byte[] bytes)
        {
            try
            {
                var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                utf8Strict.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the ANSI code page for the current system culture, or 0 if unavailable.
        /// 現在のシステムカルチャの ANSI コードページを返します。取得不可時は 0。
        /// </summary>
        private static int GetAnsiCodePage()
        {
            // On Windows, CurrentCulture.TextInfo.ANSICodePage returns the locale-specific code page
            // (e.g. 932 for Japanese). On Linux/macOS this typically returns 0 or an unusable value,
            // but the fallback to UTF-8 is correct there since the locale is usually UTF-8.
            // Windows では CurrentCulture.TextInfo.ANSICodePage がロケール固有のコードページ
            // （日本語なら 932）を返します。Linux/macOS では通常 0 か使用不能な値ですが、
            // ロケールが通常 UTF-8 なので UTF-8 フォールバックで正しい動作になります。
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        }
    }
}
