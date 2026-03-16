using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Core.Text
{
    /// <summary>
    /// 行単位のテキスト差分を計算する LCS ベースのユーティリティ。
    /// </summary>
    public static class TextDiffer
    {
        /// <summary>コンテキスト行（変更なし）。</summary>
        public const char Context = ' ';
        /// <summary>削除行（old 側にのみ存在）。</summary>
        public const char Removed = '-';
        /// <summary>追加行（new 側にのみ存在）。</summary>
        public const char Added = '+';
        /// <summary>ハンクヘッダ行（@@ -a,b +c,d @@ 形式）。</summary>
        public const char HunkHeader = '@';
        /// <summary>出力行数上限による打ち切りを示す行。</summary>
        public const char Truncated = '~';

        /// <summary>
        /// 差分の 1 行を表す不変レコード。
        /// </summary>
        /// <param name="Kind">行の種別 (<see cref="Context"/> / <see cref="Removed"/> / <see cref="Added"/> / <see cref="HunkHeader"/> / <see cref="Truncated"/>)。</param>
        /// <param name="Text">行テキスト（HunkHeader の場合は "@@ -a,b +c,d @@" 形式）。</param>
        /// <param name="OldLineNo">old ファイル上の 1-based 行番号。該当なし (Added / HunkHeader / Truncated) は 0。</param>
        /// <param name="NewLineNo">new ファイル上の 1-based 行番号。該当なし (Removed / HunkHeader / Truncated) は 0。</param>
        public readonly record struct DiffLine(char Kind, string Text, int OldLineNo = 0, int NewLineNo = 0);

        /// <summary>
        /// 2 つの行配列の unified diff を計算します。
        /// </summary>
        /// <param name="oldLines">old 側のテキスト行配列。</param>
        /// <param name="newLines">new 側のテキスト行配列。</param>
        /// <param name="contextLines">変更箇所の前後に表示するコンテキスト行数（既定: 3）。</param>
        /// <param name="maxOutputLines">出力する最大行数（ハンクヘッダ含む、既定: 500）。超過分は Truncated 行で打ち切り。</param>
        /// <returns>差分行のリスト。完全一致の場合は空のリストを返します。</returns>
        public static IReadOnlyList<DiffLine> Compute(
            string[] oldLines,
            string[] newLines,
            int contextLines = 3,
            int maxOutputLines = 500)
        {
            if (oldLines == null) throw new ArgumentNullException(nameof(oldLines));
            if (newLines == null) throw new ArgumentNullException(nameof(newLines));
            if (contextLines < 0) contextLines = 0;
            if (maxOutputLines < 1) maxOutputLines = 1;

            int m = oldLines.Length, n = newLines.Length;

            // DP テーブルが大きくなりすぎる場合はスキップ (> 4M セル ≈ 16 MB)
            if ((long)m * n > 4_000_000L)
            {
                return new[]
                {
                    new DiffLine(Truncated,
                        $"Inline diff skipped: file too large for LCS ({m} vs {n} lines). " +
                        "Reduce InlineDiffMaxInputLines or disable EnableInlineDiff to suppress this message.")
                };
            }

            // LCS DP テーブルを構築
            var dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    dp[i, j] = string.Equals(oldLines[i - 1], newLines[j - 1], StringComparison.Ordinal)
                        ? dp[i - 1, j - 1] + 1
                        : Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            // バックトレースして編集スクリプトを生成（old/new の 0-based インデックス付き）
            var edits = new List<(char Kind, int OldIdx, int NewIdx)>(capacity: m + n);
            int ii = m, jj = n;
            while (ii > 0 || jj > 0)
            {
                if (ii > 0 && jj > 0 &&
                    string.Equals(oldLines[ii - 1], newLines[jj - 1], StringComparison.Ordinal))
                {
                    edits.Add((Context, ii - 1, jj - 1));
                    ii--;
                    jj--;
                }
                else if (jj > 0 && (ii == 0 || dp[ii, jj - 1] >= dp[ii - 1, jj]))
                {
                    edits.Add((Added, -1, jj - 1));
                    jj--;
                }
                else
                {
                    edits.Add((Removed, ii - 1, -1));
                    ii--;
                }
            }
            edits.Reverse();

            return BuildHunks(oldLines, newLines, edits, contextLines, maxOutputLines);
        }

        // ── Hunk 構築 ────────────────────────────────────────────────────────

        private static IReadOnlyList<DiffLine> BuildHunks(
            string[] old,
            string[] @new,
            List<(char Kind, int OldIdx, int NewIdx)> edits,
            int contextLines,
            int maxOutputLines)
        {
            // 変更行 (Added / Removed) の位置を収集
            var changedPositions = new List<int>(edits.Count / 4 + 1);
            for (int i = 0; i < edits.Count; i++)
            {
                if (edits[i].Kind != Context)
                    changedPositions.Add(i);
            }

            if (changedPositions.Count == 0) return Array.Empty<DiffLine>();

            // ハンク範囲をマージ（隣接するハンクを結合）
            var hunkRanges = new List<(int Start, int End)>();
            int hs = Math.Max(0, changedPositions[0] - contextLines);
            int he = Math.Min(edits.Count - 1, changedPositions[0] + contextLines);

            for (int ci = 1; ci < changedPositions.Count; ci++)
            {
                int ns = Math.Max(0, changedPositions[ci] - contextLines);
                if (ns <= he + 1)
                {
                    he = Math.Min(edits.Count - 1, changedPositions[ci] + contextLines);
                }
                else
                {
                    hunkRanges.Add((hs, he));
                    hs = ns;
                    he = Math.Min(edits.Count - 1, changedPositions[ci] + contextLines);
                }
            }
            hunkRanges.Add((hs, he));

            var result = new List<DiffLine>(capacity: Math.Min(maxOutputLines + 2, 256));
            bool truncated = false;

            foreach (var (start, end) in hunkRanges)
            {
                if (result.Count >= maxOutputLines) { truncated = true; break; }

                // ハンクヘッダ: old/new の開始行番号とカウントを計算
                int oldLineStart = -1, newLineStart = -1, oldCount = 0, newCount = 0;
                for (int e = start; e <= end; e++)
                {
                    var (k, oi, ni) = edits[e];
                    if (k != Added)
                    {
                        if (oldLineStart < 0 && oi >= 0) oldLineStart = oi + 1;
                        oldCount++;
                    }
                    if (k != Removed)
                    {
                        if (newLineStart < 0 && ni >= 0) newLineStart = ni + 1;
                        newCount++;
                    }
                }
                if (oldLineStart < 0) oldLineStart = 1;
                if (newLineStart < 0) newLineStart = 1;

                result.Add(new DiffLine(
                    HunkHeader,
                    $"@@ -{oldLineStart},{oldCount} +{newLineStart},{newCount} @@",
                    OldLineNo: 0,
                    NewLineNo: 0));

                for (int e = start; e <= end; e++)
                {
                    if (result.Count >= maxOutputLines) { truncated = true; break; }

                    var (k, oi, ni) = edits[e];
                    string lineText = k == Added ? @new[ni] : old[oi];
                    int oldLn = oi >= 0 ? oi + 1 : 0;
                    int newLn = ni >= 0 ? ni + 1 : 0;
                    result.Add(new DiffLine(k, lineText, oldLn, newLn));
                }
                if (truncated) break;
            }

            if (truncated)
            {
                result.Add(new DiffLine(
                    Truncated,
                    "... (diff output truncated — increase InlineDiffMaxOutputLines to see more)"));
            }

            return result;
        }
    }
}
