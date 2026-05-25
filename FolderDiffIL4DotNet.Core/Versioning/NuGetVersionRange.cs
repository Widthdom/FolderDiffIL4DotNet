using System;

namespace FolderDiffIL4DotNet.Core.Versioning
{
    /// <summary>
    /// Parses and evaluates NuGet version range expressions used in the NuGet vulnerability API.
    /// Supports interval notation: <c>[1.0.0, 2.0.0)</c>, <c>(, 4.3.1)</c>, <c>[1.2.3]</c>, etc.
    /// NuGet 脆弱性 API で使用される NuGet バージョン範囲式を解析・評価します。
    /// 区間記法をサポート: <c>[1.0.0, 2.0.0)</c>、<c>(, 4.3.1)</c>、<c>[1.2.3]</c> 等。
    /// </summary>
    public static class NuGetVersionRange
    {
        /// <summary>
        /// Determines whether the given version string falls within the specified NuGet version range.
        /// 指定された NuGet バージョン範囲内にバージョン文字列が含まれるかを判定します。
        /// </summary>
        /// <param name="versionRange">NuGet version range expression (e.g. <c>"(, 4.3.1)"</c>). / NuGet バージョン範囲式（例: <c>"(, 4.3.1)"</c>）。</param>
        /// <param name="version">Version string to check (e.g. <c>"4.3.0"</c>). / チェック対象のバージョン文字列（例: <c>"4.3.0"</c>）。</param>
        /// <returns>True if the version is within the range; false otherwise or on parse failure. / バージョンが範囲内なら true、範囲外またはパース失敗時は false。</returns>
        public static bool Contains(string versionRange, string version)
        {
            if (string.IsNullOrWhiteSpace(versionRange) || string.IsNullOrWhiteSpace(version))
                return false;

            var ver = ParseVersion(version);
            if (ver == null)
                return false;

            var trimmed = versionRange.Trim();
            if (trimmed.Length < 2)
                return false;

            char first = trimmed[0];
            char last = trimmed[trimmed.Length - 1];

            // Exact version: [1.2.3] / 完全一致: [1.2.3]
            if (first == '[' && last == ']' && !trimmed.Contains(','))
            {
                var exact = ParseVersion(trimmed.Substring(1, trimmed.Length - 2).Trim());
                return exact != null && CompareVersions(ver, exact) == 0;
            }

            // Interval notation: [min, max), (min, max], etc. / 区間記法
            if ((first == '[' || first == '(') && (last == ']' || last == ')'))
            {
                bool minInclusive = first == '[';
                bool maxInclusive = last == ']';

                string inner = trimmed.Substring(1, trimmed.Length - 2);
                int commaIdx = inner.IndexOf(',');
                if (commaIdx < 0)
                    return false;

                string minPart = inner.Substring(0, commaIdx).Trim();
                string maxPart = inner.Substring(commaIdx + 1).Trim();

                // Check lower bound / 下限チェック
                if (minPart.Length > 0)
                {
                    var min = ParseVersion(minPart);
                    if (min == null) return false;
                    int cmp = CompareVersions(ver, min);
                    if (minInclusive ? cmp < 0 : cmp <= 0)
                        return false;
                }

                // Check upper bound / 上限チェック
                if (maxPart.Length > 0)
                {
                    var max = ParseVersion(maxPart);
                    if (max == null) return false;
                    int cmp = CompareVersions(ver, max);
                    if (maxInclusive ? cmp > 0 : cmp >= 0)
                        return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a version string into an array of numeric parts [major, minor, patch, revision].
        /// バージョン文字列を数値パーツ配列 [major, minor, patch, revision] に解析します。
        /// </summary>
        public static int[]? ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            // Strip pre-release suffix (e.g. "-preview.1") / プレリリースサフィックスを除去
            int hyphenIdx = version.IndexOf('-');
            string core = hyphenIdx >= 0 ? version.Substring(0, hyphenIdx) : version;

            // Strip metadata suffix (e.g. "+build.123") / メタデータサフィックスを除去
            int plusIdx = core.IndexOf('+');
            if (plusIdx >= 0)
                core = core.Substring(0, plusIdx);

            var parts = core.Split('.');
            if (parts.Length == 0 || parts.Length > 4)
                return null;

            var result = new int[4]; // major, minor, patch, revision
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int val) || val < 0)
                    return null;
                result[i] = val;
            }
            return result;
        }

        /// <summary>
        /// Compares two parsed version arrays lexicographically.
        /// 解析済みバージョン配列を辞書順で比較します。
        /// </summary>
        private static int CompareVersions(int[] a, int[] b)
        {
            for (int i = 0; i < 4; i++)
            {
                int cmp = a[i].CompareTo(b[i]);
                if (cmp != 0) return cmp;
            }
            return 0;
        }
    }
}
