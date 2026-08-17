using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Produces effective configured substring lists shared by IL comparison and reports.
    /// IL 比較とレポートで共有する実効設定部分文字列リストを生成します。
    /// </summary>
    internal static class ILConfiguredSubstringHelper
    {
        internal static List<string> GetEffectiveIgnoreLineSubstrings(IReadOnlyList<string>? configuredStrings)
        {
            return GetNonEmptyDistinct(configuredStrings, trimValues: true);
        }

        internal static List<string> GetEffectiveNormalizationSubstrings(IReadOnlyList<string>? configuredStrings)
        {
            string? limitError = ConfigSettings.GetILNormalizeContainingStringsLimitError(configuredStrings);
            if (limitError != null)
            {
                throw new InvalidOperationException($"Invalid runtime configuration: {limitError}");
            }

            return GetNonEmptyDistinct(configuredStrings, trimValues: false);
        }

        private static List<string> GetNonEmptyDistinct(
            IReadOnlyList<string>? configuredStrings,
            bool trimValues)
        {
            if (configuredStrings == null)
            {
                return new List<string>();
            }

            return configuredStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => trimValues ? value.Trim() : value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
