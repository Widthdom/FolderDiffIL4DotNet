using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Core.Text
{
    /// <summary>
    /// 行単位のテキスト差分を計算する Myers diff ベースのユーティリティ。
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
        /// Myers diff アルゴリズム（O(D² + N + M) 時間・O(D²) 空間）を使用するため、
        /// ファイルが大きくても差分行数 D が小さければ高速に動作します。
        /// </summary>
        /// <param name="oldLines">old 側のテキスト行配列。</param>
        /// <param name="newLines">new 側のテキスト行配列。</param>
        /// <param name="contextLines">変更箇所の前後に表示するコンテキスト行数（既定: 3）。</param>
        /// <param name="maxOutputLines">出力する最大行数（ハンクヘッダ含む、既定: 500）。超過分は Truncated 行で打ち切り。</param>
        /// <param name="maxEditDistance">
        /// 許容する最大編集距離（挿入行数 + 削除行数の合計、既定: 4000）。
        /// 実際の差分がこの値を超える場合は単一の Truncated 行を返します。
        /// </param>
        /// <returns>差分行のリスト。完全一致の場合は空のリストを返します。</returns>
        public static IReadOnlyList<DiffLine> Compute(
            string[] oldLines,
            string[] newLines,
            int contextLines = 3,
            int maxOutputLines = 500,
            int maxEditDistance = 4000)
        {
            if (oldLines == null) throw new ArgumentNullException(nameof(oldLines));
            if (newLines == null) throw new ArgumentNullException(nameof(newLines));
            if (contextLines < 0) contextLines = 0;
            if (maxOutputLines < 1) maxOutputLines = 1;
            if (maxEditDistance < 1) maxEditDistance = 1;

            int m = oldLines.Length, n = newLines.Length;

            // Myers diff: O(D² + N + M) 時間・O(D²) 空間。D が maxEditDistance を超えた場合は null。
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
        /// Myers diff アルゴリズムで編集スクリプトを生成します。
        /// 編集距離 D が maxEditDistance を超えた場合は null を返します。
        /// 時間計算量: O(D² + N + M)。空間計算量: O(D²)。
        /// </summary>
        private static List<(char Kind, int OldIdx, int NewIdx)> MyersDiff(
            string[] old, string[] @new, int maxEditDistance)
        {
            int N = old.Length, M = @new.Length;
            int maxD = Math.Min(N + M, maxEditDistance);
            int offset = maxD; // k ∈ [-maxD, maxD] を非負インデックスにシフト

            // V[k + offset] = 対角線 k 上で到達できる最大の x 座標
            var V = new int[2 * maxD + 2];
            V[offset + 1] = 0; // 初期状態: k=1 から x=0 でスタートできるよう設定

            // trace[d] = ステップ d 開始時の V スナップショット（バックトラック用）
            // スナップショットはバックトラックに必要な k±1 範囲のみ保存
            var trace = new (int Lo, int[] Data)[maxD + 1];

            int foundD = -1;
            bool completed = false;

            for (int d = 0; d <= maxD && !completed; d++)
            {
                // ステップ d 開始前の V を保存（バックトラックで k±1 参照に必要な範囲）
                int lo = Math.Max(0, offset - d - 1);
                int hi = Math.Min(2 * maxD, offset + d + 1);
                var snap = new int[hi - lo + 1];
                Array.Copy(V, lo, snap, 0, snap.Length);
                trace[d] = (lo, snap);

                for (int k = -d; k <= d && !completed; k += 2)
                {
                    int koff = k + offset;
                    // 前ステップの V から移動方向を決定: down = new から挿入 / right = old から削除
                    bool down = k == -d || (k != d && V[koff - 1] < V[koff + 1]);
                    int x = down ? V[koff + 1] : V[koff - 1] + 1;
                    int y = x - k;

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

                // ステップ d でどの方向に移動したかを復元
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

                // スネーク部分（コンテキスト行）: (xStart, yStart) → (x, y)
                for (int sx = x - 1; sx >= xStart; sx--)
                    edits.Add((Context, sx, sx - k));

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

            // d=0 の初期スネーク（対角線 k=0）: (0,0) → (x, y) はすべてコンテキスト
            // この時点で k = x - y = 0 が成立する
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
