using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FolderDiffIL4DotNet.Core.IL;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Hierarchy-aware block comparison helpers for <see cref="ILOutputService"/>.
    /// <see cref="ILOutputService"/> の階層対応ブロック比較補助です。
    /// </summary>
    public sealed partial class ILOutputService
    {
        /// <summary>
        /// Compares two filtered IL line lists using signature-aware, block-based (order-independent) comparison.
        /// Parses IL into hierarchy-aware blocks via <see cref="ILBlockParser"/>, then compares multisets
        /// of (fixed-size container path key, signature, hash) tuples. This handles compiler-induced member reordering
        /// within a class while still detecting content changes, body swaps, and moves between classes.
        /// フィルタ済み IL 行リストをシグネチャ対応のブロック単位（順序非依存）で比較します。
        /// <see cref="ILBlockParser"/> で IL を論理ブロック（メソッド、クラス等）に分割し、
        /// class/member階層を保持したブロックへ解析し、(固定長container path key, シグネチャ, ハッシュ) tupleの
        /// マルチセットとして比較します。同一class内のmember並び替えを許容しつつ、本体変更、
        /// method間の本体入れ替え、class間移動を正しく検知します。
        /// </summary>
        internal static bool BlockAwareSequenceEqual(IReadOnlyList<string> filteredLines1, IReadOnlyList<string> filteredLines2)
        {
            var blocks1 = ILBlockParser.ParseComparableBlocks(filteredLines1);
            var blocks2 = ILBlockParser.ParseComparableBlocks(filteredLines2);
            if (blocks1.Count != blocks2.Count)
            {
                return false;
            }

            var hashBag1 = BuildBlockHashBag(blocks1);
            var hashBag2 = BuildBlockHashBag(blocks2);
            if (hashBag1.Count != hashBag2.Count)
            {
                return false;
            }

            foreach (var kvp in hashBag1)
            {
                if (!hashBag2.TryGetValue(kvp.Key, out int count2) || count2 != kvp.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a multiset ((fixed-size container key, signature, hash) → count) from a list of IL blocks.
        /// Each block's signature is extracted via <see cref="ILBlockParser.ExtractBlockSignature"/>,
        /// ensuring that blocks are matched by both identity (signature) and content (hash).
        /// IL ブロックのリストからマルチセット（(固定長container key, シグネチャ, ハッシュ) → 出現回数）を構築します。
        /// 各ブロックのシグネチャは <see cref="ILBlockParser.ExtractBlockSignature"/> で抽出し、
        /// ブロックの同一性（シグネチャ）と内容（ハッシュ）の両方で照合します。
        /// </summary>
        private static Dictionary<(string Container, string Signature, string Hash), int> BuildBlockHashBag(
            IReadOnlyList<ILComparableBlock> blocks)
        {
            var bag = new Dictionary<(string Container, string Signature, string Hash), int>();
            foreach (var block in blocks)
            {
                string signature = ILBlockParser.ExtractBlockSignature(block.Lines).Trim();
                string hash = ComputeBlockHash(block.Lines);
                var key = (block.ContainerComparisonKey, signature, hash);
                bag.TryGetValue(key, out int count);
                bag[key] = count + 1;
            }

            return bag;
        }

        /// <summary>
        /// Computes a SHA256 hash of an IL block's content (all lines joined with newline).
        /// IL ブロックの内容（全行を改行で結合）の SHA256 ハッシュを計算します。
        /// </summary>
        private static string ComputeBlockHash(IReadOnlyList<string> blockLines)
        {
            using var sha256 = SHA256.Create();
            var sb = new StringBuilder();
            for (int i = 0; i < blockLines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                // Trim leading/trailing whitespace to absorb indentation variations
                // 先頭・末尾空白をトリムしてインデント差異を吸収
                sb.Append(blockLines[i].Trim());
            }

            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }
    }
}
