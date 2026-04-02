using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Core.IL
{
    /// <summary>
    /// Parses IL disassembly output into top-level blocks (methods, classes, properties, etc.)
    /// for order-independent comparison. IL lines that fall outside any block are grouped as
    /// a single "preamble" block preserving their original order.
    /// IL 逆アセンブリ出力をトップレベルブロック（メソッド、クラス、プロパティ等）に分割し、
    /// 順序非依存の比較を可能にします。ブロック外の行はプリアンブルとしてまとめられます。
    /// </summary>
    public static class ILBlockParser
    {
        // IL directives that start a nestable block (closed by a matching '}')
        // ネスト可能なブロックを開始する IL ディレクティブ（対応する '}' で閉じる）
        private static readonly string[] s_blockDirectives = new[]
        {
            ".method ",
            ".class ",
            ".property ",
            ".event ",
            ".field ",
        };

        /// <summary>
        /// Splits filtered IL lines into logical blocks. Each block is a list of lines
        /// representing a top-level IL construct (method, class, etc.).
        /// Lines before the first block or between blocks form the "preamble" block (index 0).
        /// フィルタ済み IL 行を論理ブロックに分割します。各ブロックはトップレベル IL 構造
        /// （メソッド、クラス等）を表す行のリスト。最初のブロック前やブロック間の行は
        /// プリアンブルブロック（インデックス 0）にまとめます。
        /// </summary>
        /// <param name="lines">Filtered IL lines (MVID / configured strings already excluded). / フィルタ済み IL 行（MVID / 設定文字列除外済み）。</param>
        /// <returns>List of blocks, where each block is a list of IL lines. / ブロックのリスト。各ブロックは IL 行のリスト。</returns>
        public static List<List<string>> ParseBlocks(IReadOnlyList<string> lines)
        {
            var blocks = new List<List<string>>();
            var currentBlock = new List<string>(); // preamble / プリアンブル
            int braceDepth = 0;
            bool inBlock = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (!inBlock && IsBlockStart(trimmed))
                {
                    // Save preamble or previous inter-block lines
                    // プリアンブルまたはブロック間の行を保存
                    if (currentBlock.Count > 0)
                    {
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                    }
                    inBlock = true;
                    braceDepth = 0;
                }

                currentBlock.Add(line);

                if (inBlock)
                {
                    // Count braces to detect block end
                    // 波括弧を数えてブロック終了を検出
                    braceDepth += CountBraces(trimmed);

                    if (braceDepth <= 0 && trimmed.StartsWith("}", StringComparison.Ordinal))
                    {
                        // Block ended — save it and start new inter-block collection
                        // ブロック終了 — 保存して新しいブロック間コレクションを開始
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                        inBlock = false;
                        braceDepth = 0;
                    }
                }
            }

            // Remaining lines (trailing preamble or unclosed block)
            // 残りの行（末尾のプリアンブルまたは閉じられていないブロック）
            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            return blocks;
        }

        /// <summary>
        /// Determines whether a trimmed line starts a new top-level IL block.
        /// トリム済みの行が新しいトップレベル IL ブロックの開始かどうかを判定します。
        /// </summary>
        private static bool IsBlockStart(string trimmedLine)
        {
            for (int i = 0; i < s_blockDirectives.Length; i++)
            {
                if (trimmedLine.StartsWith(s_blockDirectives[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Counts the net brace change in a line (opening minus closing).
        /// 行中の波括弧の差分（開き - 閉じ）を数えます。
        /// </summary>
        private static int CountBraces(string line)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '{') count++;
                else if (c == '}') count--;
            }
            return count;
        }
    }
}
