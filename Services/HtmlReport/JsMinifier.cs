namespace FolderDiffIL4DotNet.Services.HtmlReport
{
    /// <summary>
    /// Strips comments and blank lines from JavaScript, and minifies CSS using NUglify,
    /// to reduce HTML report size while preserving whitespace-sensitive string.replace()
    /// patterns used by downloadReviewed().
    /// JavaScript からコメントと空行を除去し、CSS は NUglify でミニファイして
    /// HTML レポートサイズを削減します。downloadReviewed() が使用する
    /// 空白依存の string.replace() パターンを維持します。
    /// </summary>
    internal static class JsMinifier
    {
        /// <summary>
        /// Strips single-line comments and blank lines from JavaScript source code.
        /// NUglify full minification is intentionally avoided because downloadReviewed()
        /// uses string.replace() with exact whitespace patterns (e.g. 'const __savedState__  = null;')
        /// that would break if whitespace around assignments were collapsed.
        /// JavaScript ソースコードから単行コメントと空行を除去します。
        /// downloadReviewed() が正確な空白パターンで string.replace() を使用するため
        /// （例: 'const __savedState__  = null;'）、NUglify の完全ミニファイは意図的に回避しています。
        /// </summary>
        internal static string Minify(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            // Strip single-line comments (// ...) that are NOT inside string literals,
            // and remove resulting blank lines. This removes ~30-40% of JS size from
            // the bilingual comment lines without disturbing whitespace in code.
            // 文字列リテラル内にない単行コメント（// ...）を除去し、結果として生じる空行を削除します。
            // コード内の空白を乱さずに、バイリンガルコメント行からJS サイズの約30-40% を削減します。
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (var rawLine in source.Split('\n'))
            {
                var stripped = StripLineComment(rawLine);
                if (stripped.Trim().Length > 0)
                {
                    sb.AppendLine(stripped.TrimEnd());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Strips a trailing // comment from a line, respecting string literals.
        /// Returns the line unchanged if // only appears inside a string.
        /// 文字列リテラルを考慮しつつ行末の // コメントを除去します。
        /// // が文字列内にのみ存在する場合は行をそのまま返します。
        /// </summary>
        private static string StripLineComment(string line)
        {
            bool inSingle = false;
            bool inDouble = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Skip escaped characters inside strings / 文字列内のエスケープ文字をスキップ
                if ((inSingle || inDouble) && c == '\\' && i + 1 < line.Length)
                {
                    i++;
                    continue;
                }

                if (c == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                }
                else if (c == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                }
                else if (c == '/' && !inSingle && !inDouble && i + 1 < line.Length && line[i + 1] == '/')
                {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        /// <summary>
        /// Strips CSS block comments (/* ... */) except important ones (/*! ... */),
        /// and removes resulting blank lines. NUglify full CSS minification is avoided because
        /// downloadReviewed() uses a regex to match ':root { --col-reason-w:...' which would
        /// break if whitespace were collapsed, and tests assert on '@media (max-width: ...)' spacing.
        /// CSS ブロックコメント（/* ... */）を除去します（重要コメント /*! ... */ は除く）。
        /// downloadReviewed() が ':root { --col-reason-w:...' を正規表現でマッチさせるため、
        /// また、テストが '@media (max-width: ...)' の空白をアサートするため、
        /// NUglify の完全 CSS ミニファイは回避します。
        /// </summary>
        internal static string MinifyCss(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            // Remove block comments except important (/*! ... */)
            // 重要コメント（/*! ... */）以外のブロックコメントを除去
            var sb = new System.Text.StringBuilder(source.Length);
            int pos = 0;
            while (pos < source.Length)
            {
                if (pos + 1 < source.Length && source[pos] == '/' && source[pos + 1] == '*')
                {
                    bool isImportant = pos + 2 < source.Length && source[pos + 2] == '!';
                    int end = source.IndexOf("*/", pos + 2, System.StringComparison.Ordinal);
                    if (end < 0) end = source.Length - 2;

                    if (isImportant)
                    {
                        sb.Append(source, pos, end + 2 - pos);
                    }

                    pos = end + 2;
                }
                else
                {
                    sb.Append(source[pos]);
                    pos++;
                }
            }

            // Remove blank lines / 空行を除去
            var result = new System.Text.StringBuilder(sb.Length);
            foreach (var rawLine in sb.ToString().Split('\n'))
            {
                if (rawLine.Trim().Length > 0)
                {
                    result.AppendLine(rawLine.TrimEnd());
                }
            }

            return result.ToString();
        }
    }
}
