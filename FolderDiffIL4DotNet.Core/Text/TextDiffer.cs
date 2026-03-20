using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Core.Text
{
    /// <summary>
    /// Line-level text diff utility based on the Myers diff algorithm.
    /// 行単位のテキスト差分を計算する Myers diff ベースのユーティリティ。
    /// </summary>
    public static class TextDiffer
    {
        /// <summary>Context line (unchanged). / コンテキスト行（変更なし）。</summary>
        public const char Context = ' ';
        /// <summary>Removed line (old only). / 削除行（old 側のみ）。</summary>
        public const char Removed = '-';
        /// <summary>Added line (new only). / 追加行（new 側のみ）。</summary>
        public const char Added = '+';
        /// <summary>Hunk header line (@@ ... @@). / ハンクヘッダ行。</summary>
        public const char HunkHeader = '@';
        /// <summary>Truncation marker line. / 打ち切りを示す行。</summary>
        public const char Truncated = '~';

        /// <summary>
        /// Immutable record representing a single diff line.
        /// 差分の 1 行を表す不変レコード。
        /// </summary>
        public readonly record struct DiffLine(char Kind, string Text, int OldLineNo = 0, int NewLineNo = 0);

        /// <summary>
        /// Computes a unified diff of two line arrays using the Myers diff algorithm (O(D^2 + N + M) time, O(D^2) space).
        /// Fast when the edit distance D is small even for large files.
        /// 2 つの行配列の unified diff を Myers diff アルゴリズムで計算します。
        /// 差分行数 D が小さければファイルが大きくても高速に動作します。
        /// </summary>
        public static IReadOnlyList<DiffLine> Compute(
            string[] oldLines,
            string[] newLines,
            int contextLines = 3,
            int maxOutputLines = 10000,
            int maxEditDistance = 4000)
        {
            if (oldLines == null) throw new ArgumentNullException(nameof(oldLines));
            if (newLines == null) throw new ArgumentNullException(nameof(newLines));
            if (contextLines < 0) contextLines = 0;
            if (maxOutputLines < 1) maxOutputLines = 1;
            if (maxEditDistance < 1) maxEditDistance = 1;

            int m = oldLines.Length, n = newLines.Length;

            // Myers diff: O(D^2 + N + M) time, O(D^2) space. Returns null when D exceeds maxEditDistance.
            // Myers diff: O(D^2 + N + M) 時間、O(D^2) 空間。D が maxEditDistance を超えると null。
            var edits = MyersDiff(oldLines, newLines, maxEditDistance);
            if (edits == null)
            {
                return new[]
                {
                    new DiffLine(Truncated,
                        $"Inline diff skipped: edit distance too large (>{maxEditDistance} insertions/deletions " +
                        $"in {m} vs {n} lines). " +
                        "Increase InlineDiffMaxEditDistance in config to raise the limit.")
                };
            }

            return BuildHunks(oldLines, newLines, edits, contextLines, maxOutputLines);
        }

        // ── Myers diff ────────────────────────────────────────────────────────

        /// <summary>
        /// Generates an edit script using the Myers diff algorithm. Returns null if the edit distance exceeds maxEditDistance.
        /// Time: O(D^2 + N + M). Space: O(D^2).
        /// Myers diff アルゴリズムで編集スクリプトを生成します。編集距離が maxEditDistance を超えると null を返します。
        /// </summary>
        private static List<(char Kind, int OldIdx, int NewIdx)>? MyersDiff(
            string[] old, string[] @new, int maxEditDistance)
        {
            int N = old.Length, M = @new.Length;
            int maxD = Math.Min(N + M, maxEditDistance);
            // Shift k in [-maxD, maxD] to non-negative indices
            // k を非負インデックスにシフト
            int offset = maxD;

            // V[k + offset] = farthest x reached on diagonal k
            // V[k + offset] = 対角線 k 上で到達できる最大の x 座標
            var V = new int[2 * maxD + 2];
            V[offset + 1] = 0;

            // trace[d] = V snapshot at the start of step d (for backtracking); only the k+-1 range needed
            // trace[d] = ステップ d 開始時の V スナップショット（バックトラック用、k+-1 範囲のみ保存）
            var trace = new (int Lo, int[] Data)[maxD + 1];

            int foundD = -1;
            bool completed = false;

            for (int d = 0; d <= maxD && !completed; d++)
            {
                // Save V before step d (k+-1 range needed for backtracking)
                // ステップ d 開始前の V を保存（バックトラック用）
                int lo = Math.Max(0, offset - d - 1);
                int hi = Math.Min(2 * maxD, offset + d + 1);
                var snap = new int[hi - lo + 1];
                Array.Copy(V, lo, snap, 0, snap.Length);
                trace[d] = (lo, snap);

                for (int k = -d; k <= d && !completed; k += 2)
                {
                    int koff = k + offset;
                    // Decide direction from previous V: down = insertion from new / right = deletion from old
                    // 前ステップの V から移動方向を決定: down = 挿入 / right = 削除
                    bool down = k == -d || (k != d && V[koff - 1] < V[koff + 1]);
                    int x = down ? V[koff + 1] : V[koff - 1] + 1;
                    int y = x - k;

                    // Extend the snake (matching lines) as far as possible
                    // スネーク（共通行）を可能な限り延ばす
                    while (x < N && y < M && string.Equals(old[x], @new[y], StringComparison.Ordinal))
                    {
                        x++; y++;
                    }

                    V[koff] = x;

                    if (x >= N && y >= M)
                    {
                        foundD = d;
                        completed = true;
                    }
                }
            }

            if (foundD < 0) return null;

            return BacktrackMyers(old, @new, trace, foundD, offset, N, M);
        }

        private static List<(char Kind, int OldIdx, int NewIdx)> BacktrackMyers(
            string[] old,
            string[] @new,
            (int Lo, int[] Data)[] trace,
            int D,
            int offset,
            int N,
            int M)
        {
            var edits = new List<(char Kind, int OldIdx, int NewIdx)>(D * 2 + 16);
            int x = N, y = M;

            for (int d = D; d > 0; d--)
            {
                int k = x - y;
                var (lo, snap) = trace[d];
                int GetV(int kk) => snap[kk + offset - lo];

                // Restore which direction was taken at step d
                // ステップ d での移動方向を復元
                bool wasDown = k == -d || (k != d && GetV(k - 1) < GetV(k + 1));

                int prevK, xStart;
                if (wasDown)
                {
                    prevK = k + 1;
                    xStart = GetV(k + 1);
                }
                else
                {
                    prevK = k - 1;
                    xStart = GetV(k - 1) + 1;
                }
                int yStart = xStart - k;

                // Snake portion (context lines): (xStart, yStart) -> (x, y)
                // スネーク部分（コンテキスト行）: (xStart, yStart) -> (x, y)
                for (int sx = x - 1; sx >= xStart; sx--)
                    edits.Add((Context, sx, sx - k));

                // One edit line: down = Added, right = Removed
                // 編集 1 行: down = 追加、right = 削除
                if (wasDown)
                {
                    edits.Add((Added, -1, yStart - 1));
                    x = xStart;
                    y = yStart - 1;
                }
                else
                {
                    edits.Add((Removed, xStart - 1, -1));
                    x = xStart - 1;
                    y = yStart;
                }

                _ = prevK; // suppress unused warning
            }

            // Initial snake at d=0 (diagonal k=0): all context lines from (0,0) to (x, y)
            // d=0 の初期スネーク（対角線 k=0）: (0,0) -> (x, y) はすべてコンテキスト
            for (int sx = x - 1; sx >= 0; sx--)
                edits.Add((Context, sx, sx));

            edits.Reverse();
            return edits;
        }

        // ── Hunk 構築 ────────────────────────────────────────────────────────

        private static IReadOnlyList<DiffLine> BuildHunks(
            string[] old,
            string[] @new,
            List<(char Kind, int OldIdx, int NewIdx)> edits,
            int contextLines,
            int maxOutputLines)
        {
            // Collect positions of changed lines (Added / Removed)
            // 変更行 (Added / Removed) の位置を収集
            var changedPositions = new List<int>(edits.Count / 4 + 1);
            for (int i = 0; i < edits.Count; i++)
            {
                if (edits[i].Kind != Context)
                    changedPositions.Add(i);
            }

            if (changedPositions.Count == 0) return Array.Empty<DiffLine>();

            // Merge adjacent hunk ranges
            // 隣接するハンク範囲を結合
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

                // Compute hunk header: old/new start line numbers and counts
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
