using System;
using System.Linq;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Text
{
    [Trait("Category", "Unit")]
    public sealed class TextDifferTests
    {
        // ── Exact match / 完全一致 ────────────────────────────────────────────

        [Fact]
        public void Compute_IdenticalLines_ReturnsEmpty()
        {
            var old = new[] { "line1", "line2", "line3" };
            var @new = new[] { "line1", "line2", "line3" };

            var result = TextDiffer.Compute(old, @new);

            Assert.Empty(result);
        }

        [Fact]
        public void Compute_BothEmpty_ReturnsEmpty()
        {
            var result = TextDiffer.Compute(Array.Empty<string>(), Array.Empty<string>());

            Assert.Empty(result);
        }

        // ── Additions only / 追加のみ ────────────────────────────────────────

        [Fact]
        public void Compute_OldEmpty_AllAdded()
        {
            var old = Array.Empty<string>();
            var @new = new[] { "a", "b" };

            var result = TextDiffer.Compute(old, @new);

            Assert.Contains(result, l => l.Kind == TextDiffer.HunkHeader);
            var added = result.Where(l => l.Kind == TextDiffer.Added).ToList();
            Assert.Equal(2, added.Count);
            Assert.Equal("a", added[0].Text);
            Assert.Equal("b", added[1].Text);
            Assert.All(result.Where(l => l.Kind == TextDiffer.Added), l => Assert.Equal(0, l.OldLineNo));
        }

        // ── Deletions only / 削除のみ ────────────────────────────────────────

        [Fact]
        public void Compute_NewEmpty_AllRemoved()
        {
            var old = new[] { "x", "y" };
            var @new = Array.Empty<string>();

            var result = TextDiffer.Compute(old, @new);

            var removed = result.Where(l => l.Kind == TextDiffer.Removed).ToList();
            Assert.Equal(2, removed.Count);
            Assert.Equal("x", removed[0].Text);
            Assert.Equal("y", removed[1].Text);
            Assert.All(result.Where(l => l.Kind == TextDiffer.Removed), l => Assert.Equal(0, l.NewLineNo));
        }

        // ── Single-line change / 単一行変更 ──────────────────────────────────

        [Fact]
        public void Compute_SingleLineChanged_ProducesRemoveAndAdd()
        {
            var old = new[] { "hello" };
            var @new = new[] { "world" };

            var result = TextDiffer.Compute(old, @new);

            Assert.Contains(result, l => l.Kind == TextDiffer.Removed && l.Text == "hello");
            Assert.Contains(result, l => l.Kind == TextDiffer.Added && l.Text == "world");
        }

        // ── Line numbers / 行番号 ────────────────────────────────────────────

        [Fact]
        public void Compute_LineNumbers_AreOneBasedAndCorrect()
        {
            var old = new[] { "ctx1", "ctx2", "old3", "ctx4", "ctx5" };
            var @new = new[] { "ctx1", "ctx2", "new3", "ctx4", "ctx5" };

            var result = TextDiffer.Compute(old, @new, contextLines: 2);

            var removedLine = result.Single(l => l.Kind == TextDiffer.Removed);
            var addedLine = result.Single(l => l.Kind == TextDiffer.Added);

            Assert.Equal(3, removedLine.OldLineNo);
            Assert.Equal(0, removedLine.NewLineNo);
            Assert.Equal(0, addedLine.OldLineNo);
            Assert.Equal(3, addedLine.NewLineNo);
        }

        // ── Context lines / コンテキスト行 ───────────────────────────────────

        [Fact]
        public void Compute_ContextLines_AroundChange()
        {
            var old = new[] { "a", "b", "c", "d", "e" };
            var @new = new[] { "a", "b", "X", "d", "e" };

            var result = TextDiffer.Compute(old, @new, contextLines: 1);

            var contextLines = result.Where(l => l.Kind == TextDiffer.Context).ToList();
            // context=1 -- 1 line before and after the change (line 3): "b"(2) and "d"(4)
            // context=1 → 変更行(line3)の前後1行: "b"(2) と "d"(4)
            Assert.Equal(2, contextLines.Count);
            Assert.Contains(contextLines, l => l.Text == "b");
            Assert.Contains(contextLines, l => l.Text == "d");
            // "a" (first) and "e" (last) should NOT be included
            // 最初の "a" と最後の "e" は含まれない
            Assert.DoesNotContain(contextLines, l => l.Text == "a");
            Assert.DoesNotContain(contextLines, l => l.Text == "e");
        }

        [Fact]
        public void Compute_ContextLines_Zero_NoContextLines()
        {
            var old = new[] { "a", "changed", "c" };
            var @new = new[] { "a", "changed2", "c" };

            var result = TextDiffer.Compute(old, @new, contextLines: 0);

            Assert.DoesNotContain(result, l => l.Kind == TextDiffer.Context);
        }

        // ── Hunk header / ハンクヘッダ ───────────────────────────────────────

        [Fact]
        public void Compute_HunkHeader_HasCorrectFormat()
        {
            var old = new[] { "line1" };
            var @new = new[] { "changed" };

            var result = TextDiffer.Compute(old, @new);

            var hunk = result.Single(l => l.Kind == TextDiffer.HunkHeader);
            Assert.StartsWith("@@", hunk.Text);
            Assert.Contains("-1,", hunk.Text);
            Assert.Contains("+1,", hunk.Text);
            Assert.EndsWith("@@", hunk.Text.TrimEnd());
        }

        // ── Multiple hunks / 複数ハンク ──────────────────────────────────────

        [Fact]
        public void Compute_MultipleHunks_TwoHunkHeaders()
        {
            // Changes far apart produce 2 hunks
            // 変更箇所が離れていれば 2 ハンクになる
            var old = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
            var @new = new[] { "X", "2", "3", "4", "5", "6", "7", "8", "9", "Y" };

            var result = TextDiffer.Compute(old, @new, contextLines: 1);

            var hunkHeaders = result.Where(l => l.Kind == TextDiffer.HunkHeader).ToList();
            Assert.Equal(2, hunkHeaders.Count);
        }

        // ── Output limit truncation / 出力上限による打ち切り ─────────────────

        [Fact]
        public void Compute_MaxOutputLines_TruncatesWithTruncatedLine()
        {
            var old = Enumerable.Range(1, 20).Select(i => $"old{i}").ToArray();
            var @new = Enumerable.Range(1, 20).Select(i => $"new{i}").ToArray();

            var result = TextDiffer.Compute(old, @new, contextLines: 0, maxOutputLines: 5);

            Assert.Contains(result, l => l.Kind == TextDiffer.Truncated);
            // Number of lines before the truncation marker should be <= maxOutputLines
            // 打ち切り行より前の行数は maxOutputLines 以下
            int truncIdx = result.ToList().FindIndex(l => l.Kind == TextDiffer.Truncated);
            Assert.True(truncIdx <= 5);
        }

        // ── Edit distance limit / 編集距離上限 ──────────────────────────────

        [Fact]
        public void Compute_EditDistanceExceedsLimit_ReturnsTruncatedMessage()
        {
            // Edit distance 10 (all lines differ) exceeds maxEditDistance=5 -- skipped
            // maxEditDistance=5 に対して編集距離 10（すべて異なる行）→ スキップ
            var old = Enumerable.Range(1, 10).Select(i => $"old{i}").ToArray();
            var @new = Enumerable.Range(1, 10).Select(i => $"new{i}").ToArray();

            var result = TextDiffer.Compute(old, @new, maxEditDistance: 5);

            Assert.Single(result);
            Assert.Equal(TextDiffer.Truncated, result[0].Kind);
            Assert.Contains("too large", result[0].Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("InlineDiffMaxEditDistance", result[0].Text);
        }

        [Fact]
        public void Compute_LargeFilesSmallEditDistance_ProducesCorrectDiff()
        {
            // Old LCS guard would skip when m*n > 4M, but Myers diff can handle small diffs.
            // 3000 x 2000 lines: 2000 common, 1000 deleted -- edit distance D=1000
            // 旧 LCS ガードでは m*n > 4M でスキップされていたが、Myers diff なら小さい差分は処理できる。
            // 3000 行 x 2000 行: 共通行 2000、削除行 1000 → 編集距離 D=1000
            var old = Enumerable.Range(1, 3000).Select(i => $"line{i}").ToArray();
            var @new = Enumerable.Range(1, 2000).Select(i => $"line{i}").ToArray();

            var result = TextDiffer.Compute(old, @new, contextLines: 0, maxOutputLines: ConfigSettings.DefaultInlineDiffMaxOutputLines);

            // Old LCS guard returned Truncated, but Myers diff produces the correct diff
            // 旧 LCS ガードでは Truncated が返っていたが、Myers diff では正しく差分が得られる
            Assert.False(result.Count == 1 && result[0].Kind == TextDiffer.Truncated);
            Assert.Contains(result, l => l.Kind == TextDiffer.Removed);
            Assert.DoesNotContain(result, l => l.Kind == TextDiffer.Added);
        }

        [Fact]
        public void Compute_VeryLargeFilesWithTinyDiff_ProducesInlineDiff()
        {
            // Even very large files are handled correctly when the diff is small.
            // 10000 common lines with a single changed line in the middle.
            // ファイルが大きくても差分が小さければ正常に処理できることを検証。
            // 10000 行の共通行に 1 行の変更を挟む。
            var old = Enumerable.Range(1, 10000).Select(i => $"common{i}").ToArray();
            var @new = old.ToArray();
            old[5000] = "old-changed";
            @new[5000] = "new-changed";

            var result = TextDiffer.Compute(old, @new, contextLines: 0);

            Assert.Contains(result, l => l.Kind == TextDiffer.Removed && l.Text == "old-changed");
            Assert.Contains(result, l => l.Kind == TextDiffer.Added   && l.Text == "new-changed");
            // Should produce a proper diff, not a single Truncated entry
            // 単独の Truncated で返すのではなく、正しく差分が得られること
            Assert.False(result.Count == 1 && result[0].Kind == TextDiffer.Truncated);
        }

        // ── Null arguments / null 引数 ──────────────────────────────────────

        [Fact]
        public void Compute_NullOld_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TextDiffer.Compute(null, new[] { "a" }));
        }

        [Fact]
        public void Compute_NullNew_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TextDiffer.Compute(new[] { "a" }, null));
        }

        // ── Unicode ──────────────────────────────────────────────────────────

        [Fact]
        public void Compute_UnicodeLines_HandledCorrectly()
        {
            var old = new[] { "日本語", "変更前", "中文" };
            var @new = new[] { "日本語", "変更後", "中文" };

            var result = TextDiffer.Compute(old, @new);

            Assert.Contains(result, l => l.Kind == TextDiffer.Removed && l.Text == "変更前");
            Assert.Contains(result, l => l.Kind == TextDiffer.Added && l.Text == "変更後");
        }

        // ── Leading whitespace / 行頭空白 ────────────────────────────────────

        [Fact]
        public void Compute_WhitespaceOnlyDiff_DetectsChange()
        {
            var old = new[] { "  indented" };
            var @new = new[] { "    indented" };  // extra spaces

            var result = TextDiffer.Compute(old, @new);

            Assert.Contains(result, l => l.Kind == TextDiffer.Removed);
            Assert.Contains(result, l => l.Kind == TextDiffer.Added);
        }

        // ── HunkHeader OldLineNo/NewLineNo ──────────────────────────────────

        [Fact]
        public void Compute_HunkHeaderLine_HasZeroLineNumbers()
        {
            var old = new[] { "a" };
            var @new = new[] { "b" };

            var result = TextDiffer.Compute(old, @new);

            var hunk = result.Single(l => l.Kind == TextDiffer.HunkHeader);
            Assert.Equal(0, hunk.OldLineNo);
            Assert.Equal(0, hunk.NewLineNo);
        }

        // ── Context line number consistency / コンテキスト行の行番号整合性 ──

        [Fact]
        public void Compute_ContextLines_BothLineNumbersSet()
        {
            var old = new[] { "ctx", "changed", "ctx2" };
            var @new = new[] { "ctx", "new",     "ctx2" };

            var result = TextDiffer.Compute(old, @new, contextLines: 1);

            foreach (var line in result.Where(l => l.Kind == TextDiffer.Context))
            {
                Assert.True(line.OldLineNo > 0, "Context line should have OldLineNo > 0");
                Assert.True(line.NewLineNo > 0, "Context line should have NewLineNo > 0");
            }
        }
    }
}
