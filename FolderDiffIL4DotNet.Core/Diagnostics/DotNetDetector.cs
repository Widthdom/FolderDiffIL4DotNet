using System;
using System.IO;

namespace FolderDiffIL4DotNet.Core.Diagnostics
{
    /// <summary>
    /// Result status of .NET executable detection.
    /// .NET 実行可能判定の結果種別。
    /// </summary>
    public enum DotNetExecutableDetectionStatus
    {
        /// <summary>
        /// The target file was determined not to be a .NET executable.
        /// 対象ファイルが .NET 実行可能形式ではないと判定されました。
        /// </summary>
        NotDotNetExecutable,

        /// <summary>
        /// The target file was determined to be a .NET executable.
        /// 対象ファイルが .NET 実行可能形式であると判定されました。
        /// </summary>
        DotNetExecutable,

        /// <summary>
        /// An exception occurred during detection; the result could not be determined.
        /// 判定中に例外が発生し、結果を確定できませんでした。
        /// </summary>
        Failed
    }

    /// <summary>
    /// Holds the .NET executable detection result and, on failure, the exception.
    /// .NET 実行可能判定の結果と、失敗時の例外を保持します。
    /// </summary>
    public readonly record struct DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus Status, Exception Exception = null)
    {
        /// <summary>
        /// True when the file was determined to be a .NET executable.
        /// .NET 実行可能と判定された場合に true。
        /// </summary>
        public bool IsDotNetExecutable => Status == DotNetExecutableDetectionStatus.DotNetExecutable;

        /// <summary>
        /// True when the detection itself failed (e.g. I/O error).
        /// 判定処理そのものが失敗した場合に true。
        /// </summary>
        public bool IsFailure => Status == DotNetExecutableDetectionStatus.Failed;
    }

    /// <summary>
    /// Parses PE/CLR headers to determine whether a file is a .NET executable.
    /// PE/CLR ヘッダーを解析し、.NET 実行可能ファイルかどうかを判定するクラス。
    /// </summary>
    public static class DotNetDetector
    {
        private const ushort DOS_HEADER_MAGIC = 0x5A4D;            // "MZ"
        private const int DOS_HEADER_PE_POINTER_OFFSET = 0x3C;
        private const uint PE_SIGNATURE = 0x00004550;             // "PE\0\0"
        private const int PE_SIGNATURE_SIZE = 4;
        private const int COFF_HEADER_SIZE = 20;
        private const int OPTIONAL_HEADER_START_OFFSET = PE_SIGNATURE_SIZE + COFF_HEADER_SIZE;
        private const ushort OPTIONAL_HEADER_MAGIC_PE32 = 0x010B;
        private const ushort OPTIONAL_HEADER_MAGIC_PE32_PLUS = 0x020B;
        private const int CLR_RUNTIME_HEADER_OFFSET_PE32 = 0x70;
        private const int CLR_RUNTIME_HEADER_OFFSET_PE32_PLUS = 0x80;
        private const int CLR_RUNTIME_HEADER_MINIMUM_BYTES = 8;
        /// <summary>
        /// Determines whether the file is a .NET executable (PE with CLR header).
        /// Supports both PE32 (32-bit) and PE32+ (64-bit), correctly identifying all .NET assemblies that can be disassembled by DnSpy.
        /// Returns a result that distinguishes between non-.NET files and detection failures.
        /// 指定されたファイルが .NET 実行可能ファイル（PE かつ CLR ヘッダを持つ）かを判定します。
        /// PE32/PE32+ 両対応。非 .NET ファイルと判定失敗を区別した結果を返します。
        /// </summary>
        public static DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath)
        {
            try
            {
                using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read))
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    // Verify DOS header magic number ("MZ")
                    // DOS ヘッダのマジックナンバー ("MZ") を確認
                    if (binaryReader.ReadUInt16() != DOS_HEADER_MAGIC)
                    {
                        return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable);
                    }

                    // Read PE header offset from the DOS header
                    // DOS ヘッダ内のオフセットから PE ヘッダの位置を取得
                    fileStream.Seek(offset: DOS_HEADER_PE_POINTER_OFFSET, SeekOrigin.Begin);
                    int peHeaderOffset = binaryReader.ReadInt32();

                    // Ensure PE header fits within the file
                    // ファイルサイズチェック（PE ヘッダが範囲内か）
                    if (peHeaderOffset < 0 || peHeaderOffset >= fileStream.Length - OPTIONAL_HEADER_START_OFFSET)
                    {
                        return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable);
                    }

                    // Verify PE signature ("PE\0\0")
                    // PE シグネチャを確認
                    fileStream.Seek(offset: peHeaderOffset, SeekOrigin.Begin);
                    if (binaryReader.ReadUInt32() != PE_SIGNATURE)
                    {
                        return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable);
                    }

                    // Skip COFF header to reach the Optional Header
                    // COFF ヘッダをスキップし Optional Header の先頭へ
                    fileStream.Seek(offset: peHeaderOffset + OPTIONAL_HEADER_START_OFFSET, SeekOrigin.Begin);

                    // Read Optional Header magic to distinguish PE32 (32-bit) from PE32+ (64-bit)
                    // Optional Header の Magic を読んで 32bit/64bit を判定
                    ushort magic = binaryReader.ReadUInt16();
                    int clrHeaderOffset;
                    var optionalHeaderStart = peHeaderOffset + OPTIONAL_HEADER_START_OFFSET;

                    if (magic == OPTIONAL_HEADER_MAGIC_PE32)
                    {
                        // PE32: CLR Runtime Header is at Data Directory[14]
                        // PE32 の場合、CLR Runtime Header は Data Directory[14] に配置
                        clrHeaderOffset = optionalHeaderStart + CLR_RUNTIME_HEADER_OFFSET_PE32;
                    }
                    else if (magic == OPTIONAL_HEADER_MAGIC_PE32_PLUS)
                    {
                        // PE32+: CLR Runtime Header is at Data Directory[14]
                        // PE32+ の場合、CLR Runtime Header は Data Directory[14] に配置
                        clrHeaderOffset = optionalHeaderStart + CLR_RUNTIME_HEADER_OFFSET_PE32_PLUS;
                    }
                    else
                    {
                        // Unknown PE format / 不明な PE 形式
                        return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable);
                    }

                    // Verify we can read the CLR header RVA and size
                    // CLR ヘッダの RVA とサイズを確認（最低バイト数を読めるかチェック）
                    if (clrHeaderOffset + CLR_RUNTIME_HEADER_MINIMUM_BYTES > fileStream.Length)
                    {
                        return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable);
                    }

                    fileStream.Seek(offset: clrHeaderOffset, SeekOrigin.Begin);
                    uint clrHeaderRva = binaryReader.ReadUInt32();
                    uint clrHeaderSize = binaryReader.ReadUInt32();

                    // A non-zero CLR header RVA with a valid size indicates a .NET assembly
                    // CLR ヘッダが存在しサイズが妥当なら .NET アセンブリと判定
                    return new DotNetExecutableDetectionResult(
                        clrHeaderRva != 0 && clrHeaderSize > 0
                            ? DotNetExecutableDetectionStatus.DotNetExecutable
                            : DotNetExecutableDetectionStatus.NotDotNetExecutable);
                }
            }
            catch (ArgumentException ex)
            {
                return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.Failed, ex);
            }
            catch (IOException ex)
            {
                return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.Failed, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.Failed, ex);
            }
            catch (NotSupportedException ex)
            {
                return new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.Failed, ex);
            }
        }

        /// <summary>
        /// Returns true if the file is a .NET executable; returns false on detection failure.
        /// Use <see cref="DetectDotNetExecutable(string)"/> for detailed failure information.
        /// .NET 実行可能ファイルなら true を返します。判定不能時も false。詳細は <see cref="DetectDotNetExecutable(string)"/> を使用。
        /// </summary>
        public static bool IsDotNetExecutable(string fileAbsolutePath)
            => DetectDotNetExecutable(fileAbsolutePath).IsDotNetExecutable;
    }
}
