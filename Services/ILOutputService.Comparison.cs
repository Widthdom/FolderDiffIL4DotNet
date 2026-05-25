using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IL;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// IL comparison, filtering, and disassembler-label helpers for <see cref="ILOutputService"/>.
    /// <see cref="ILOutputService"/> の IL 比較・フィルタリング・逆アセンブラ表示補助をまとめた partial です。
    /// </summary>
    public sealed partial class ILOutputService
    {
        /// <summary>
        /// Compares two IL line collections after applying exclusion filters, without materializing
        /// the filtered results into separate lists. Advances dual indices, skipping excluded lines,
        /// and short-circuits on the first mismatch — O(1) extra memory beyond the input lists.
        /// 2 つの IL 行コレクションを除外フィルタ適用後に比較します。フィルタ済みの別リストを
        /// 実体化せずに 2 つのインデックスを進め、除外行をスキップしながら最初の不一致で即終了します。
        /// 入力リスト以外の追加メモリは O(1) です。
        /// </summary>
        internal static bool StreamingFilteredSequenceEqual(
            IReadOnlyList<string> lines1,
            IReadOnlyList<string> lines2,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            bool shouldIgnoreMVID = true)
        {
            int i = 0;
            int j = 0;
            int count1 = lines1.Count;
            int count2 = lines2.Count;
            while (true)
            {
                while (i < count1 && ShouldExcludeIlLine(lines1[i], shouldIgnoreContainingStrings, ilIgnoreContainingStrings, shouldIgnoreMVID))
                {
                    i++;
                }

                while (j < count2 && ShouldExcludeIlLine(lines2[j], shouldIgnoreContainingStrings, ilIgnoreContainingStrings, shouldIgnoreMVID))
                {
                    j++;
                }

                bool end1 = i >= count1;
                bool end2 = j >= count2;
                if (end1 && end2)
                {
                    return true;
                }

                if (end1 || end2)
                {
                    return false;
                }

                // Compare with leading/trailing whitespace trimmed to absorb indentation
                // variations between disassembler versions or formatting differences.
                // 逆アセンブラバージョン間のインデント差異やフォーマット差異を吸収するため
                // 先頭・末尾空白をトリムして比較する。
                if (!lines1[i].AsSpan().Trim().SequenceEqual(lines2[j].AsSpan().Trim()))
                {
                    return false;
                }

                i++;
                j++;
            }
        }

        /// <summary>
        /// Filters IL lines by applying exclusion rules, returning a new list of non-excluded lines.
        /// Used when the filtered result must be materialized (e.g. for IL text file output).
        /// IL 行を除外ルールでフィルタリングし、除外されなかった行の新しいリストを返します。
        /// フィルタ結果の実体化が必要な場合（IL テキストファイル出力等）に使用します。
        /// </summary>
        internal static List<string> FilterIlLines(
            IReadOnlyList<string> lines,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            bool shouldIgnoreMVID = true)
        {
            var result = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                if (!ShouldExcludeIlLine(lines[i], shouldIgnoreContainingStrings, ilIgnoreContainingStrings, shouldIgnoreMVID))
                {
                    result.Add(lines[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Splits IL text into lines and filters out excluded lines in a single pass,
        /// avoiding intermediate list allocations from separate Split → Where → ToList chains.
        /// IL テキストを行に分割し、除外行を 1 パスでフィルタリングすることで
        /// Split → Where → ToList の中間リスト割り当てを回避します。
        /// </summary>
        private static List<string> SplitAndFilterIlLines(
            string ilText,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            bool shouldIgnoreMVID = true)
        {
            var result = new List<string>();
            int startIndex = 0;
            int length = ilText.Length;
            while (startIndex <= length)
            {
                int newlineIndex = ilText.IndexOf('\n', startIndex);
                string line;
                if (newlineIndex < 0)
                {
                    line = ilText.Substring(startIndex);
                    startIndex = length + 1;
                }
                else
                {
                    line = ilText.Substring(startIndex, newlineIndex - startIndex);
                    startIndex = newlineIndex + 1;
                }

                if (!ShouldExcludeIlLine(line, shouldIgnoreContainingStrings, ilIgnoreContainingStrings, shouldIgnoreMVID))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether a line should be excluded from IL comparison.
        /// IL 比較時に除外すべき行かを判定します。
        /// </summary>
        private static bool ShouldExcludeIlLine(
            string line,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            bool shouldIgnoreMVID = true)
        {
            if (line is null)
            {
                return false;
            }

            if (shouldIgnoreMVID && line.StartsWith(Constants.IL_MVID_LINE_PREFIX, StringComparison.Ordinal))
            {
                return true;
            }

            if (!shouldIgnoreContainingStrings || ilIgnoreContainingStrings == null || ilIgnoreContainingStrings.Count == 0)
            {
                return false;
            }

            return ilIgnoreContainingStrings.Any(target => line.Contains(target, StringComparison.Ordinal));
        }

        /// <summary>
        /// Compares two filtered IL line lists using signature-aware, block-based (order-independent) comparison.
        /// Parses IL into logical blocks (methods, classes, etc.) via <see cref="ILBlockParser"/>,
        /// extracts each block's signature (directive line) and content hash, then compares as multisets
        /// of (signature, hash) pairs. This handles compiler-induced reordering while correctly detecting
        /// content changes even when blocks with different signatures have identical bodies.
        /// フィルタ済み IL 行リストをシグネチャ対応のブロック単位（順序非依存）で比較します。
        /// <see cref="ILBlockParser"/> で IL を論理ブロック（メソッド、クラス等）に分割し、
        /// 各ブロックのシグネチャ（ディレクティブ行）とコンテンツハッシュを抽出してから
        /// (シグネチャ, ハッシュ) ペアのマルチセットとして比較します。コンパイラによる並び替えを
        /// 許容しつつ、異なるシグネチャのブロック間でのコンテンツ入れ替わりを正しく検知します。
        /// </summary>
        internal static bool BlockAwareSequenceEqual(IReadOnlyList<string> filteredLines1, IReadOnlyList<string> filteredLines2)
        {
            var blocks1 = ILBlockParser.ParseBlocks(filteredLines1);
            var blocks2 = ILBlockParser.ParseBlocks(filteredLines2);
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
        /// Builds a multiset ((signature, hash) → count) from a list of IL blocks.
        /// Each block's signature is extracted via <see cref="ILBlockParser.ExtractBlockSignature"/>,
        /// ensuring that blocks are matched by both identity (signature) and content (hash).
        /// IL ブロックのリストからマルチセット（(シグネチャ, ハッシュ) → 出現回数）を構築します。
        /// 各ブロックのシグネチャは <see cref="ILBlockParser.ExtractBlockSignature"/> で抽出し、
        /// ブロックの同一性（シグネチャ）と内容（ハッシュ）の両方で照合します。
        /// </summary>
        private static Dictionary<(string Signature, string Hash), int> BuildBlockHashBag(List<List<string>> blocks)
        {
            var bag = new Dictionary<(string Signature, string Hash), int>();
            foreach (var block in blocks)
            {
                string signature = ILBlockParser.ExtractBlockSignature(block);
                string hash = ComputeBlockHash(block);
                var key = (signature, hash);
                bag.TryGetValue(key, out int count);
                bag[key] = count + 1;
            }

            return bag;
        }

        /// <summary>
        /// Computes a SHA256 hash of an IL block's content (all lines joined with newline).
        /// IL ブロックの内容（全行を改行で結合）の SHA256 ハッシュを計算します。
        /// </summary>
        private static string ComputeBlockHash(List<string> blockLines)
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

        /// <summary>
        /// Validates configured IL filter strings and returns warning messages for any that appear
        /// too short or overly broad. Strings shorter than <see cref="IL_FILTER_STRING_MIN_LENGTH"/>
        /// characters risk matching legitimate IL lines.
        /// 設定された IL フィルタ文字列を検証し、短すぎるまたは広範すぎるパターンに対する
        /// 警告メッセージを返します。<see cref="IL_FILTER_STRING_MIN_LENGTH"/> 文字未満の文字列は
        /// 正規の IL 行を誤って除外するリスクがあります。
        /// </summary>
        /// <param name="normalizedStrings">The normalized filter strings as returned by <see cref="GetNormalizedIlIgnoreContainingStrings"/>. / <see cref="GetNormalizedIlIgnoreContainingStrings"/> が返した正規化済みフィルタ文字列。</param>
        /// <returns>List of warning messages (empty if all strings are safe). / 警告メッセージのリスト（すべて安全なら空）。</returns>
        internal static List<string> ValidateILFilterStrings(IReadOnlyCollection<string> normalizedStrings)
        {
            var warnings = new List<string>();
            if (normalizedStrings == null || normalizedStrings.Count == 0)
            {
                return warnings;
            }

            foreach (var s in normalizedStrings)
            {
                if (s.Length < IL_FILTER_STRING_MIN_LENGTH)
                {
                    warnings.Add($"ILIgnoreLineContainingStrings: \"{s}\" is very short ({s.Length} chars) and may inadvertently exclude legitimate IL lines. Consider using a more specific pattern.");
                }
            }

            return warnings;
        }

        /// <summary>
        /// Normalises the strings used for contains-based line exclusion during IL comparison (removes null/whitespace, trims, deduplicates).
        /// IL 比較時に「含む」判定で除外対象とする文字列を正規化します（null/空白除外、trim、重複排除）。
        /// </summary>
        private static List<string> GetNormalizedIlIgnoreContainingStrings(IReadOnlyConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null)
            {
                return new List<string>();
            }

            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Merges the disassembler labels used for old/new into a single comparison label.
        /// old/new で使用された逆アセンブラ表示ラベルを比較用に 1 つへまとめます。
        /// </summary>
        private static string? BuildComparisonDisassemblerLabel(string commandStringOld, string commandStringNew)
        {
            var oldLabel = BuildToolAndVersionLabel(commandStringOld);
            var newLabel = BuildToolAndVersionLabel(commandStringNew);
            if (string.IsNullOrWhiteSpace(oldLabel))
            {
                return newLabel;
            }

            if (string.IsNullOrWhiteSpace(newLabel))
            {
                return oldLabel;
            }

            if (string.Equals(oldLabel, newLabel, StringComparison.OrdinalIgnoreCase))
            {
                return oldLabel;
            }

            throw new InvalidOperationException($"IL comparison requires the same disassembler and version for old/new. old: '{oldLabel}', new: '{newLabel}'.");
        }

        /// <summary>
        /// Extracts a "toolName (version: x.y.z)" label from a command string.
        /// 実行コマンド文字列から「ツール名 (version: x.y.z)」形式を抽出します。
        /// </summary>
        private static string? BuildToolAndVersionLabel(string commandString)
        {
            if (string.IsNullOrWhiteSpace(commandString))
            {
                return null;
            }

            var tokens = ProcessHelper.TokenizeCommand(commandString);
            if (tokens.Count == 0)
            {
                return null;
            }

            string toolName;
            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase) &&
                tokens.Count >= 2 &&
                (string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase)))
            {
                toolName = Constants.DOTNET_ILDASM;
            }
            else
            {
                toolName = Path.GetFileName(tokens[0]);
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            if (string.Equals(toolName, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                toolName = Constants.DOTNET_ILDASM;
            }

            int versionStart = commandString.IndexOf(VERSION_LABEL_PREFIX, StringComparison.Ordinal);
            if (versionStart < 0)
            {
                return toolName;
            }

            int versionEnd = commandString.IndexOf(')', versionStart + VERSION_LABEL_PREFIX.Length);
            if (versionEnd <= versionStart)
            {
                return toolName;
            }

            string version = commandString.Substring(
                versionStart + VERSION_LABEL_PREFIX.Length,
                versionEnd - (versionStart + VERSION_LABEL_PREFIX.Length)).Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                return toolName;
            }

            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return $"{Constants.ILDASM_LABEL} (version: {version})";
            }

            return $"{toolName} (version: {version})";
        }
    }
}
