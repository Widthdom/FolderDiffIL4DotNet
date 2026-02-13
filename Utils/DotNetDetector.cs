using System;
using System.IO;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// PE/CLR ヘッダーを解析し、.NET 実行可能ファイルかどうかを判定するクラス
    /// </summary>
    public static class DotNetDetector
    {
        #region constants
        /// <summary>
        /// DOS ヘッダのマジックナンバー ("MZ")。
        /// </summary>
        private const ushort DOS_HEADER_MAGIC = 0x5A4D;

        /// <summary>
        /// DOS ヘッダ内の PE ヘッダ先頭位置を示すオフセット。
        /// </summary>
        private const int DOS_HEADER_PE_POINTER_OFFSET = 0x3C;

        /// <summary>
        /// PE シグネチャ値 ("PE\0\0")。
        /// </summary>
        private const uint PE_SIGNATURE = 0x00004550;

        /// <summary>
        /// PE シグネチャのバイト数。
        /// </summary>
        private const int PE_SIGNATURE_SIZE = 4;

        /// <summary>
        /// COFF ヘッダのサイズ（20 バイト）。
        /// </summary>
        private const int COFF_HEADER_SIZE = 20;

        /// <summary>
        /// Optional Header 先頭までの総オフセット（PE シグネチャ + COFF ヘッダ）。
        /// </summary>
        private const int OPTIONAL_HEADER_START_OFFSET = PE_SIGNATURE_SIZE + COFF_HEADER_SIZE;

        /// <summary>
        /// Optional Header のマジック (PE32)。
        /// </summary>
        private const ushort OPTIONAL_HEADER_MAGIC_PE32 = 0x010B;

        /// <summary>
        /// Optional Header のマジック (PE32+)。
        /// </summary>
        private const ushort OPTIONAL_HEADER_MAGIC_PE32_PLUS = 0x020B;

        /// <summary>
        /// PE32 時の CLR Runtime Header へのオフセット。
        /// </summary>
        private const int CLR_RUNTIME_HEADER_OFFSET_PE32 = 0x70;

        /// <summary>
        /// PE32+ 時の CLR Runtime Header へのオフセット。
        /// </summary>
        private const int CLR_RUNTIME_HEADER_OFFSET_PE32_PLUS = 0x80;

        /// <summary>
        /// CLR Runtime Header の最小必要バイト数。
        /// </summary>
        private const int CLR_RUNTIME_HEADER_MINIMUM_BYTES = 8;
        #endregion

        #region public methods
        /// <summary>
        /// 指定されたファイルが .NET 実行可能ファイル（PE かつ CLR ヘッダを持つ）かを判定します。
        /// PE32（32bit）とPE32+（64bit）の両方に対応し、DnSpyで逆アセンブル可能な全ての.NETアセンブリを正しく判定します。
        /// エラーが発生した場合や判定不能な場合は false を返します（例外は送出しません）。
        /// </summary>
        /// <param name="fileAbsolutePath">判定するファイルの絶対パス</param>
        /// <returns>.NET 実行可能ファイルと判定できた場合は true、それ以外（非 PE/CLR、読み取り失敗含む）の場合は false。</returns>
        /// <exception cref="Exception">内部で発生した例外は捕捉されるため、呼び出し元へは送出されません。</exception>
        public static bool IsDotNetExecutable(string fileAbsolutePath)
        {
            try
            {
                using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read))
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    // DOSヘッダのマジックナンバー (DOS_HEADER_MAGIC: "MZ") を確認
                    if (binaryReader.ReadUInt16() != DOS_HEADER_MAGIC)
                    {
                        return false;
                    }

                    // DOSヘッダ内のオフセット（DOS_HEADER_PE_POINTER_OFFSET）から PE ヘッダの位置を取得
                    fileStream.Seek(offset: DOS_HEADER_PE_POINTER_OFFSET, SeekOrigin.Begin);
                    int peHeaderOffset = binaryReader.ReadInt32();

                    // ファイルサイズチェック（PEヘッダが範囲内か）
                    if (peHeaderOffset < 0 || peHeaderOffset >= fileStream.Length - OPTIONAL_HEADER_START_OFFSET)
                    {
                        return false;
                    }

                    // PEシグネチャ (PE_SIGNATURE: "PE\0\0") を確認
                    fileStream.Seek(offset: peHeaderOffset, SeekOrigin.Begin);
                    if (binaryReader.ReadUInt32() != PE_SIGNATURE)
                    {
                        return false;
                    }

                    // COFFヘッダ（COFF_HEADER_SIZE バイト）をスキップし Optional Header の先頭へ
                    fileStream.Seek(offset: peHeaderOffset + OPTIONAL_HEADER_START_OFFSET, SeekOrigin.Begin);

                    // OptionalHeaderのMagicを読んで32bit/64bitを判定
                    ushort magic = binaryReader.ReadUInt16();
                    int clrHeaderOffset;
                    var optionalHeaderStart = peHeaderOffset + OPTIONAL_HEADER_START_OFFSET;

                    if (magic == OPTIONAL_HEADER_MAGIC_PE32)
                    {
                        // PE32の場合、CLR Runtime Header は Data Directory[14]（CLR_RUNTIME_HEADER_OFFSET_PE32）に配置
                        clrHeaderOffset = optionalHeaderStart + CLR_RUNTIME_HEADER_OFFSET_PE32;
                    }
                    else if (magic == OPTIONAL_HEADER_MAGIC_PE32_PLUS)
                    {
                        // PE32+の場合、CLR Runtime Header は Data Directory[14]（CLR_RUNTIME_HEADER_OFFSET_PE32_PLUS）に配置
                        clrHeaderOffset = optionalHeaderStart + CLR_RUNTIME_HEADER_OFFSET_PE32_PLUS;
                    }
                    else
                    {
                        return false; // 不明なPE形式
                    }

                    // CLRヘッダのRVAとサイズを確認（CLR_RUNTIME_HEADER_MINIMUM_BYTES を読めるかチェック）
                    if (clrHeaderOffset + CLR_RUNTIME_HEADER_MINIMUM_BYTES > fileStream.Length)
                    {
                        return false;
                    }

                    fileStream.Seek(offset: clrHeaderOffset, SeekOrigin.Begin);
                    uint clrHeaderRva = binaryReader.ReadUInt32();
                    uint clrHeaderSize = binaryReader.ReadUInt32();

                    // CLRヘッダが存在し、サイズが妥当な場合は .NET アセンブリと判定
                    return clrHeaderRva != 0 && clrHeaderSize > 0;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
