using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Services
{
    public sealed partial class DotNetDisassembleService
    {
        private const string ILSPY_UPDATE_NOTICE_PREFIX = "You are not using the latest version of the tool";
        private const string ILSPY_LATEST_VERSION_PREFIX = "Latest version is ";
        private const string ILSPY_CURRENT_VERSION_FRAGMENT = "(yours is ";

        /// <summary>
        /// Removes known disassembler tool update notices from stdout while leaving IL lines intact.
        /// 逆アセンブラツールの既知の更新通知行だけを stdout から除去し、IL 行は保持します。
        /// </summary>
        internal static string StripDisassemblerStdoutNotices(string stdout)
        {
            if (string.IsNullOrEmpty(stdout))
            {
                return string.Empty;
            }

            var lines = SplitToLines(stdout);
            var filtered = StripDisassemblerStdoutNoticeLines(lines);
            return filtered.Count == lines.Count ? stdout : JoinLines(filtered);
        }

        /// <summary>
        /// Removes known disassembler tool update notices from a line collection.
        /// 逆アセンブラツールの既知の更新通知行だけを行リストから除去します。
        /// </summary>
        internal static List<string> StripDisassemblerStdoutNoticeLines(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            List<string>? filtered = null;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (IsDisassemblerStdoutNoticeLine(line))
                {
                    filtered ??= CopyPrefix(lines, i);
                    continue;
                }

                filtered?.Add(line);
            }

            return filtered ?? new List<string>(lines);
        }

        private static List<string> CopyPrefix(IReadOnlyList<string> lines, int count)
        {
            var copied = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                copied.Add(lines[i]);
            }
            return copied;
        }

        /// <summary>
        /// Returns true when the text has at least one line that is neither blank nor a known tool notice.
        /// Scans without allocating and stops at the first IL line.
        /// 空白行でも既知のツール通知行でもない行が 1 行以上あれば true を返します。
        /// 割り当てなしで走査し、最初の IL 行で打ち切ります。
        /// </summary>
        internal static bool ContainsIlContent(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var line in text.AsSpan().EnumerateLines())
            {
                if (IsIlContentLine(line))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true when at least one line is neither blank nor a known tool notice.
        /// 空白行でも既知のツール通知行でもない行が 1 行以上あれば true を返します。
        /// </summary>
        internal static bool ContainsIlContent(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            for (int i = 0; i < lines.Count; i++)
            {
                if (IsIlContentLine(lines[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true when the line is neither blank nor a known tool notice, i.e. it carries IL output.
        /// 空白行でも既知のツール通知行でもない、つまり IL 出力を含む行であれば true を返します。
        /// </summary>
        private static bool IsIlContentLine(ReadOnlySpan<char> line)
            => !line.IsWhiteSpace() && !IsDisassemblerStdoutNoticeLine(line);

        /// <summary>
        /// Returns true when the line is a known disassembler tool notice; null or blank lines are never notices.
        /// 既知の逆アセンブラツール通知行であれば true を返します。null や空白行は通知行として扱いません。
        /// </summary>
        private static bool IsDisassemblerStdoutNoticeLine(string? line)
            => !string.IsNullOrWhiteSpace(line) && IsDisassemblerStdoutNoticeLine(line.AsSpan());

        /// <summary>
        /// Span-based notice check used by the allocation-free scan in <see cref="ContainsIlContent(string?)"/>.
        /// <see cref="ContainsIlContent(string?)"/> の割り当てなし走査で使う、span ベースの通知行判定です。
        /// </summary>
        private static bool IsDisassemblerStdoutNoticeLine(ReadOnlySpan<char> line)
        {
            var trimmed = line.Trim();
            return trimmed.StartsWith(ILSPY_UPDATE_NOTICE_PREFIX, StringComparison.OrdinalIgnoreCase)
                || (trimmed.StartsWith(ILSPY_LATEST_VERSION_PREFIX, StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains(ILSPY_CURRENT_VERSION_FRAGMENT, StringComparison.OrdinalIgnoreCase));
        }
    }
}
