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

        private static bool IsDisassemblerStdoutNoticeLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var trimmed = line.Trim();
            return trimmed.StartsWith(ILSPY_UPDATE_NOTICE_PREFIX, StringComparison.OrdinalIgnoreCase)
                || (trimmed.StartsWith(ILSPY_LATEST_VERSION_PREFIX, StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains(ILSPY_CURRENT_VERSION_FRAGMENT, StringComparison.OrdinalIgnoreCase));
        }
    }
}
