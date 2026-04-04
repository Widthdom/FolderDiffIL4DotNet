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
        /// Extracts the signature (first directive line) from a parsed block.
        /// Returns an empty string for preamble or inter-block lines that have no directive.
        /// パース済みブロックからシグネチャ（最初のディレクティブ行）を抽出します。
        /// ディレクティブを持たないプリアンブルやブロック間の行の場合は空文字列を返します。
        /// </summary>
        /// <param name="blockLines">A single block as returned by <see cref="ParseBlocks"/>. / <see cref="ParseBlocks"/> が返した単一ブロック。</param>
        /// <returns>The directive line (trimmed) if found, otherwise an empty string. / ディレクティブ行（トリム済み）が見つかれば返し、なければ空文字列。</returns>
        public static string ExtractBlockSignature(IReadOnlyList<string> blockLines)
        {
            if (blockLines == null || blockLines.Count == 0)
            {
                return string.Empty;
            }

            // The first line of a directive block is the directive itself.
            // For preamble blocks, no line starts with a block directive.
            // ディレクティブブロックの最初の行がディレクティブそのもの。
            // プリアンブルブロックではブロックディレクティブで始まる行はない。
            string firstTrimmed = blockLines[0].TrimStart();
            if (IsBlockStart(firstTrimmed))
            {
                return firstTrimmed;
            }
            return string.Empty;
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
        /// Counts the net brace change in a line (opening minus closing),
        /// skipping braces inside string literals ("...") and after line comments (//).
        /// This prevents false block boundary detection from braces in IL string operands
        /// (e.g. <c>ldstr "JSON: {\"key\": \"value\"}"</c>) or comments.
        /// 行中の波括弧の差分（開き - 閉じ）を数えます。
        /// 文字列リテラル（"..."）内およびラインコメント（//）以降の波括弧はスキップします。
        /// IL 文字列オペランド（例: <c>ldstr "JSON: {\"key\": \"value\"}"</c>）や
        /// コメント内の波括弧によるブロック境界の誤検知を防止します。
        /// </summary>
        private static int CountBraces(string line)
        {
            int count = 0;
            bool inString = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Check for line comment start (outside of string literals)
                // 文字列リテラル外でのラインコメント開始をチェック
                if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    // Rest of line is a comment — no more braces to count
                    // 行の残りはコメント — これ以上波括弧をカウントしない
                    break;
                }

                // Track string literal boundaries (handle escaped quotes)
                // 文字列リテラルの境界を追跡（エスケープされた引用符を処理）
                if (c == '"')
                {
                    if (inString)
                    {
                        // Check if this quote is escaped by a backslash
                        // この引用符がバックスラッシュでエスケープされているかチェック
                        int backslashCount = 0;
                        for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
                            backslashCount++;
                        if (backslashCount % 2 == 0)
                            inString = false; // Unescaped quote — end of string / エスケープされていない引用符 — 文字列終了
                    }
                    else
                    {
                        inString = true;
                    }
                    continue;
                }

                if (!inString)
                {
                    if (c == '{') count++;
                    else if (c == '}') count--;
                }
            }
            return count;
        }
    }
}
