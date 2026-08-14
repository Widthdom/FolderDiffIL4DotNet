using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IL;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// IL comparison, filtering, and disassembler-label helpers for <see cref="ILOutputService"/>.
    /// <see cref="ILOutputService"/> の IL 比較・フィルタリング・逆アセンブラ表示補助をまとめた partial です。
    /// </summary>
    public sealed partial class ILOutputService
    {
        private const string NORMALIZED_VALUE_PREFIX = "<nildiff:normalized:";
        internal const string RVA_NORMALIZED_VALUE = NORMALIZED_VALUE_PREFIX + "rva>";
        internal const string MVID_NORMALIZED_VALUE = NORMALIZED_VALUE_PREFIX + "mvid>";
        internal const string CODE_SIZE_NORMALIZED_VALUE = NORMALIZED_VALUE_PREFIX + "code-size>";
        internal const string TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE = NORMALIZED_VALUE_PREFIX + "type-library-timestamp>";
        internal const string CONFIGURED_NORMALIZED_VALUE = NORMALIZED_VALUE_PREFIX + "configured-value>";
        internal const int MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE = 65_536;
        internal const int MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS = 4 * 1024 * 1024;
        private const int CONFIGURED_NORMALIZATION_INITIAL_SEGMENT_GROWTH = 256;
        private const int IL_NORMALIZATION_WARNING_DETAIL_LIMIT = 100;

        internal static IReadOnlyList<(string Disassembler, string Prefix, string Replacement)> BuiltInNormalizationRules { get; } =
            Array.AsReadOnly(
                new[]
                {
                    (Disassembler: Constants.DOTNET_ILDASM, Prefix: Constants.IL_MVID_LINE_PREFIX, Replacement: $" {MVID_NORMALIZED_VALUE}"),
                    (Disassembler: Constants.DOTNET_ILDASM, Prefix: Constants.IL_RVA_LINE_PREFIX, Replacement: RVA_NORMALIZED_VALUE),
                    (Disassembler: Constants.ILSPY_CMD, Prefix: Constants.IL_ILSPY_RVA_LINE_PREFIX, Replacement: RVA_NORMALIZED_VALUE),
                    (Disassembler: Constants.DOTNET_ILDASM, Prefix: Constants.IL_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX, Replacement: TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE),
                    (Disassembler: Constants.ILSPY_CMD, Prefix: Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX, Replacement: $" {TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE}"),
                    (Disassembler: Constants.DOTNET_ILDASM, Prefix: Constants.IL_CODE_SIZE_LINE_PREFIX, Replacement: CODE_SIZE_NORMALIZED_VALUE),
                    (Disassembler: Constants.ILSPY_CMD, Prefix: Constants.IL_ILSPY_CODE_SIZE_LINE_PREFIX, Replacement: CODE_SIZE_NORMALIZED_VALUE)
                }
                .OrderBy(rule => rule.Prefix, StringComparer.Ordinal)
                .ThenBy(rule => rule.Disassembler, StringComparer.Ordinal)
                .ToArray());

        /// <summary>
        /// Compares two IL line collections after applying exclusion filters, without materializing
        /// the complete filtered results into separate lists. Advances dual indices, skipping excluded
        /// lines, and short-circuits on the first mismatch. Configured normalization uses a bounded
        /// representation of the current line, subject to explicit replacement and output limits;
        /// collision-free marker selection also tracks marker suffixes already present across the inputs.
        /// 2 つの IL 行コレクションを除外フィルタ適用後に比較します。フィルタ済みの別リストを
        /// 全体として実体化せずに 2 つのインデックスを進め、除外行をスキップしながら最初の不一致で
        /// 即終了します。設定値の正規化では、明示的な置換数・出力長上限の範囲内で現在行を有界な
        /// 表現として扱い、衝突しないマーカーの選択では入力全体に既存のマーカー suffix も追跡します。
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// A matching line exceeds a configured-normalization replacement or output limit.
        /// 一致する行が設定値正規化の置換数または出力長上限を超えた場合に発生します。
        /// </exception>
        internal static bool StreamingFilteredSequenceEqual(
            IReadOnlyList<string> lines1,
            IReadOnlyList<string> lines2,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings)
        {
            string configuredNormalizedValue = ilNormalizeContainingStrings.Count == 0
                ? CONFIGURED_NORMALIZED_VALUE
                : CreateCollisionFreeConfiguredNormalizedValue(lines1, lines2);
            return StreamingFilteredSequenceEqual(
                lines1,
                lines2,
                shouldIgnoreContainingStrings,
                ilIgnoreContainingStrings,
                ilNormalizeContainingStrings,
                configuredNormalizedValue);
        }

        private static bool StreamingFilteredSequenceEqual(
            IReadOnlyList<string> lines1,
            IReadOnlyList<string> lines2,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings,
            string configuredNormalizedValue)
        {
            int i = 0;
            int j = 0;
            while (true)
            {
                bool hasLine1 = TryReadNextFilteredNormalizedLine(
                    lines1,
                    ref i,
                    shouldIgnoreContainingStrings,
                    ilIgnoreContainingStrings,
                    ilNormalizeContainingStrings,
                    configuredNormalizedValue,
                    out string normalized1);
                bool hasLine2 = TryReadNextFilteredNormalizedLine(
                    lines2,
                    ref j,
                    shouldIgnoreContainingStrings,
                    ilIgnoreContainingStrings,
                    ilNormalizeContainingStrings,
                    configuredNormalizedValue,
                    out string normalized2);

                bool end1 = !hasLine1;
                bool end2 = !hasLine2;
                if (end1 && end2)
                {
                    return true;
                }

                if (end1 || end2)
                {
                    return false;
                }

                // Compare with leading/trailing whitespace trimmed to absorb indentation
                // variations between disassembler versions or formatting differences.
                // 逆アセンブラバージョン間のインデント差異やフォーマット差異を吸収するため
                // 先頭・末尾空白をトリムして比較する。
                if (!normalized1.AsSpan().Trim().SequenceEqual(normalized2.AsSpan().Trim()))
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Filters and normalizes IL lines, returning a materialized list of comparable lines.
        /// Used when the result must be retained, for example for IL text file output.
        /// IL 行をフィルタリングおよび正規化し、比較可能な行を実体化したリストとして返します。
        /// IL テキストファイル出力など、結果の保持が必要な場合に使用します。
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// A matching line exceeds a configured-normalization replacement or output limit.
        /// 一致する行が設定値正規化の置換数または出力長上限を超えた場合に発生します。
        /// </exception>
        internal static List<string> FilterIlLines(
            IReadOnlyList<string> lines,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings)
        {
            string configuredNormalizedValue = ilNormalizeContainingStrings.Count == 0
                ? CONFIGURED_NORMALIZED_VALUE
                : CreateCollisionFreeConfiguredNormalizedValue(lines);
            return FilterIlLines(
                lines,
                shouldIgnoreContainingStrings,
                ilIgnoreContainingStrings,
                ilNormalizeContainingStrings,
                configuredNormalizedValue);
        }

        private static List<string> FilterIlLines(
            IReadOnlyList<string> lines,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings,
            string configuredNormalizedValue)
        {
            var result = new List<string>(lines.Count);
            int lineIndex = 0;
            while (TryReadNextFilteredNormalizedLine(
                lines,
                ref lineIndex,
                shouldIgnoreContainingStrings,
                ilIgnoreContainingStrings,
                ilNormalizeContainingStrings,
                configuredNormalizedValue,
                out string normalizedLine))
            {
                result.Add(normalizedLine);
            }

            return result;
        }

        private static bool TryReadNextFilteredNormalizedLine(
            IReadOnlyList<string> lines,
            ref int lineIndex,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings,
            string configuredNormalizedValue,
            out string normalizedLine)
        {
            while (lineIndex < lines.Count)
            {
                int startIndex = lineIndex;
                bool isIlspyTypeLibraryTimestampBlock = TryNormalizeIlspyTypeLibraryTimestampBlock(
                    lines,
                    startIndex,
                    out int endIndexExclusive,
                    out string candidateLine);
                if (!isIlspyTypeLibraryTimestampBlock)
                {
                    endIndexExclusive = startIndex + 1;
                    candidateLine = lines[startIndex];
                }

                lineIndex = endIndexExclusive;
                if (ShouldExcludeIlLine(
                    lines[startIndex],
                    shouldIgnoreContainingStrings,
                    ilIgnoreContainingStrings))
                {
                    continue;
                }

                normalizedLine = NormalizeIlLine(
                    candidateLine,
                    ilNormalizeContainingStrings,
                    configuredNormalizedValue);
                return true;
            }

            normalizedLine = string.Empty;
            return false;
        }

        private static bool TryNormalizeIlspyTypeLibraryTimestampBlock(
            IReadOnlyList<string> lines,
            int startIndex,
            out int endIndexExclusive,
            out string normalizedLine)
        {
            string headerLine = lines[startIndex];
            ReadOnlySpan<char> content = headerLine.AsSpan().TrimStart();
            if (!content.StartsWith(Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX, StringComparison.Ordinal))
            {
                endIndexExclusive = startIndex + 1;
                normalizedLine = headerLine;
                return false;
            }

            // ilspycmd emits custom-attribute blobs over multiple lines. Collapse the complete
            // timestamp blob so its byte payload cannot leak into line or block comparison.
            // ilspycmd は custom attribute の blob を複数行で出力するため、timestamp blob 全体を
            // 縮約し、バイト列が行比較やブロック比較へ残らないようにする。
            bool hasPayloadLine = false;
            for (int currentIndex = startIndex + 1; currentIndex < lines.Count; currentIndex++)
            {
                if (lines[currentIndex].AsSpan().Trim().SequenceEqual(")".AsSpan()))
                {
                    if (!hasPayloadLine)
                    {
                        break;
                    }

                    endIndexExclusive = currentIndex + 1;
                    normalizedLine = NormalizeSuffixAfterPrefix(
                        headerLine,
                        Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                        $" {TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE}");
                    return true;
                }

                if (!IsIlspyCustomAttributeBlobPayloadLine(lines[currentIndex]))
                {
                    break;
                }

                hasPayloadLine = true;
            }

            endIndexExclusive = startIndex + 1;
            normalizedLine = headerLine;
            return false;
        }

        private static bool IsIlspyCustomAttributeBlobPayloadLine(string line)
        {
            ReadOnlySpan<char> content = line.AsSpan().Trim();
            if (content.IsEmpty)
            {
                return false;
            }

            int tokenLength = 0;
            bool hasByte = false;
            foreach (char value in content)
            {
                if (char.IsWhiteSpace(value))
                {
                    if (tokenLength != 0 && tokenLength != 2)
                    {
                        return false;
                    }

                    hasByte |= tokenLength == 2;
                    tokenLength = 0;
                    continue;
                }

                if (!Uri.IsHexDigit(value) || ++tokenLength > 2)
                {
                    return false;
                }
            }

            if (tokenLength != 0 && tokenLength != 2)
            {
                return false;
            }

            return hasByte || tokenLength == 2;
        }

        /// <summary>
        /// Splits IL text into lines, collapses supported multiline ILSpy timestamp forms,
        /// and applies filtering and normalization in a single pass.
        /// IL テキストを行に分割し、対応する ILSpy の複数行 timestamp 形式をまとめて、
        /// フィルタリングと正規化を 1 パスで適用します。
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// A matching line exceeds a configured-normalization replacement or output limit.
        /// 一致する行が設定値正規化の置換数または出力長上限を超えた場合に発生します。
        /// </exception>
        private static List<string> SplitAndFilterIlLines(
            string ilText,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings,
            IReadOnlyCollection<string> ilNormalizeContainingStrings)
        {
            var result = new List<string>();
            List<string>? pendingIlspyTimestampBlock = null;
            int startIndex = 0;
            int length = ilText.Length;
            while (startIndex <= length)
            {
                int newlineIndex = ilText.IndexOf('\n', startIndex);
                string line;
                if (newlineIndex < 0)
                {
                    line = ilText.Substring(startIndex);
                    startIndex = length + 1;
                }
                else
                {
                    line = ilText.Substring(startIndex, newlineIndex - startIndex);
                    startIndex = newlineIndex + 1;
                }

                if (pendingIlspyTimestampBlock != null)
                {
                    bool isClosingLine = line.AsSpan().Trim().SequenceEqual(")".AsSpan());
                    if (isClosingLine && pendingIlspyTimestampBlock.Count > 1)
                    {
                        pendingIlspyTimestampBlock.Add(line);
                        result.AddRange(FilterIlLines(
                            pendingIlspyTimestampBlock,
                            shouldIgnoreContainingStrings,
                            ilIgnoreContainingStrings,
                            ilNormalizeContainingStrings));
                        pendingIlspyTimestampBlock = null;
                        continue;
                    }

                    if (IsIlspyCustomAttributeBlobPayloadLine(line))
                    {
                        pendingIlspyTimestampBlock.Add(line);
                        continue;
                    }

                    AddOrdinaryLines(pendingIlspyTimestampBlock);
                    pendingIlspyTimestampBlock = null;
                }

                if (line.AsSpan().TrimStart().StartsWith(
                    Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                    StringComparison.Ordinal))
                {
                    pendingIlspyTimestampBlock = new List<string> { line };
                    continue;
                }

                AddOrdinaryLine(line);
            }

            if (pendingIlspyTimestampBlock != null)
            {
                AddOrdinaryLines(pendingIlspyTimestampBlock);
            }

            return result;

            void AddOrdinaryLines(IEnumerable<string> lines)
            {
                foreach (string pendingLine in lines)
                {
                    AddOrdinaryLine(pendingLine);
                }
            }

            void AddOrdinaryLine(string candidate)
            {
                if (!ShouldExcludeIlLine(candidate, shouldIgnoreContainingStrings, ilIgnoreContainingStrings))
                {
                    result.Add(NormalizeIlLine(candidate, ilNormalizeContainingStrings));
                }
            }
        }

        /// <summary>
        /// Determines whether a line should be excluded from IL comparison.
        /// IL 比較時に除外すべき行かを判定します。
        /// </summary>
        private static bool ShouldExcludeIlLine(
            string line,
            bool shouldIgnoreContainingStrings,
            IReadOnlyCollection<string> ilIgnoreContainingStrings)
        {
            if (line is null)
            {
                return false;
            }

            if (!shouldIgnoreContainingStrings || ilIgnoreContainingStrings.Count == 0)
            {
                return false;
            }

            return ilIgnoreContainingStrings.Any(target => line.Contains(target, StringComparison.Ordinal));
        }

        /// <summary>
        /// Replaces known build-variant IL values and configured matching substrings with stable placeholders.
        /// 既知のビルド依存 IL 値と設定された一致部分を安定したプレースホルダーへ置換します。
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// The line has a configured match and exceeds a configured-normalization replacement or output limit.
        /// 行に設定値の一致があり、設定値正規化の置換数または出力長上限を超えた場合に発生します。
        /// </exception>
        internal static string NormalizeIlLine(
            string line,
            IReadOnlyCollection<string> ilNormalizeContainingStrings)
        {
            string configuredNormalizedValue = ilNormalizeContainingStrings.Count == 0 || line is null
                ? CONFIGURED_NORMALIZED_VALUE
                : CreateCollisionFreeConfiguredNormalizedValue(new[] { line });
            return NormalizeIlLine(
                line,
                ilNormalizeContainingStrings,
                configuredNormalizedValue);
        }

        private static string NormalizeIlLine(
            string? line,
            IReadOnlyCollection<string> ilNormalizeContainingStrings,
            string configuredNormalizedValue)
        {
            if (line is null)
            {
                return string.Empty;
            }

            string normalized = line;
            foreach (var rule in BuiltInNormalizationRules)
            {
                normalized = NormalizeSuffixAfterPrefix(normalized, rule.Prefix, rule.Replacement);
            }

            if (ilNormalizeContainingStrings.Count == 0)
            {
                return normalized;
            }

            return ApplyConfiguredNormalization(
                normalized,
                ilNormalizeContainingStrings,
                configuredNormalizedValue);
        }

        private static string ApplyConfiguredNormalization(
            string line,
            IReadOnlyCollection<string> configuredStrings,
            string configuredNormalizedValue)
        {
            // Keep generated markers in protected segments so later rules inspect only unreplaced input fragments.
            // 生成したマーカーを保護セグメントに保持し、後続規則は未置換の入力断片だけを照合します。
            var segments = new List<ConfiguredNormalizationSegment>
            {
                new(
                    line,
                    StartIndex: 0,
                    Length: line.Length,
                    IsProtectedMarker: false,
                    RepetitionCount: 1)
            };
            int totalReplacementCount = 0;
            long outputLength = line.Length;

            foreach (string target in configuredStrings)
            {
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                int ruleMatchCount = CountConfiguredNormalizationMatches(
                    segments,
                    target,
                    MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE - totalReplacementCount);
                if (ruleMatchCount == 0)
                {
                    continue;
                }

                // Track the logical length without materializing intermediate output. A later rule can shrink it.
                // 中間出力を実体化せず論理長だけを追跡します。後続規則で短くなる場合があります。
                long projectedOutputLength = CalculateConfiguredNormalizationOutputLength(
                    outputLength,
                    ruleMatchCount,
                    target.Length,
                    configuredNormalizedValue.Length);
                segments = ApplyConfiguredNormalizationRule(
                    segments,
                    target,
                    configuredNormalizedValue,
                    ruleMatchCount);
                totalReplacementCount = checked(totalReplacementCount + ruleMatchCount);
                outputLength = projectedOutputLength;
            }

            // The output limit applies only when configured normalization actually replaces a match.
            // 設定値正規化で実際に一致を置換する場合にだけ、出力長上限を適用します。
            if (totalReplacementCount == 0)
            {
                return line;
            }

            if (outputLength < 0
                || outputLength > MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS)
            {
                throw CreateConfiguredNormalizationOutputLimitException();
            }

            // Enforce the final output limit immediately before allocating its contiguous storage.
            // 最終出力の連続領域を割り当てる直前に出力長上限を適用します。
            int boundedOutputLength = checked((int)outputLength);
            var result = new StringBuilder(boundedOutputLength);
            foreach (ConfiguredNormalizationSegment segment in segments)
            {
                for (int repetition = 0; repetition < segment.RepetitionCount; repetition++)
                {
                    result.Append(segment.Source, segment.StartIndex, segment.Length);
                }
            }
            return result.ToString();
        }

        private static int CountConfiguredNormalizationMatches(
            IReadOnlyList<ConfiguredNormalizationSegment> segments,
            string target,
            int remainingReplacementCount)
        {
            int matchCount = 0;
            foreach (ConfiguredNormalizationSegment segment in segments)
            {
                if (segment.IsProtectedMarker)
                {
                    continue;
                }

                int endIndex = checked(segment.StartIndex + segment.Length);
                int startIndex = segment.StartIndex;
                while (true)
                {
                    int matchIndex = segment.Source.IndexOf(
                        target,
                        startIndex,
                        endIndex - startIndex,
                        StringComparison.Ordinal);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    if (matchCount >= remainingReplacementCount)
                    {
                        throw CreateConfiguredNormalizationReplacementLimitException();
                    }

                    matchCount = checked(matchCount + 1);
                    startIndex = checked(matchIndex + target.Length);
                }
            }

            return matchCount;
        }

        private static long CalculateConfiguredNormalizationOutputLength(
            long currentOutputLength,
            int replacementCount,
            int targetLength,
            int replacementLength)
        {
            try
            {
                long lengthDeltaPerReplacement = checked((long)replacementLength - targetLength);
                long totalLengthDelta = checked(lengthDeltaPerReplacement * replacementCount);
                long projectedOutputLength = checked(currentOutputLength + totalLengthDelta);
                if (projectedOutputLength < 0)
                {
                    throw CreateConfiguredNormalizationOutputLimitException();
                }

                return projectedOutputLength;
            }
            catch (OverflowException)
            {
                throw CreateConfiguredNormalizationOutputLimitException();
            }
        }

        private static InvalidDataException CreateConfiguredNormalizationReplacementLimitException()
        {
            return new InvalidDataException(
                $"{nameof(ConfigSettings.ILNormalizeContainingStrings)}: configured IL normalization exceeded the per-line replacement limit of {MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE} matches.");
        }

        private static InvalidDataException CreateConfiguredNormalizationOutputLimitException()
        {
            return new InvalidDataException(
                $"{nameof(ConfigSettings.ILNormalizeContainingStrings)}: configured IL normalization would exceed the per-line output limit of {MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS} UTF-16 code units.");
        }

        private static List<ConfiguredNormalizationSegment> ApplyConfiguredNormalizationRule(
            List<ConfiguredNormalizationSegment> segments,
            string target,
            string configuredNormalizedValue,
            int ruleMatchCount)
        {
            int maximumSegmentCount = checked(segments.Count + checked(ruleMatchCount * 2));
            int initialSegmentCount = Math.Min(
                maximumSegmentCount,
                checked(segments.Count + CONFIGURED_NORMALIZATION_INITIAL_SEGMENT_GROWTH));
            var nextSegments = new List<ConfiguredNormalizationSegment>(initialSegmentCount);
            foreach (ConfiguredNormalizationSegment segment in segments)
            {
                if (segment.IsProtectedMarker
                    || segment.Source.IndexOf(
                        target,
                        segment.StartIndex,
                        segment.Length,
                        StringComparison.Ordinal) < 0)
                {
                    AppendConfiguredNormalizationSegment(nextSegments, segment);
                    continue;
                }

                AppendConfiguredNormalizationReplacements(
                    nextSegments,
                    segment,
                    target,
                    configuredNormalizedValue);
            }

            return nextSegments;
        }

        private static void AppendConfiguredNormalizationReplacements(
            List<ConfiguredNormalizationSegment> destination,
            ConfiguredNormalizationSegment segment,
            string target,
            string configuredNormalizedValue)
        {
            // Preserve unmatched fragments for later rules and protect only generated replacement markers.
            // 一致しない断片は後続規則向けに未保護で残し、生成した置換マーカーだけを保護します。
            int startIndex = segment.StartIndex;
            int endIndex = checked(segment.StartIndex + segment.Length);
            while (true)
            {
                int matchIndex = segment.Source.IndexOf(
                    target,
                    startIndex,
                    endIndex - startIndex,
                    StringComparison.Ordinal);
                if (matchIndex < 0)
                {
                    if (startIndex < endIndex)
                    {
                        AppendConfiguredNormalizationSegment(destination, new ConfiguredNormalizationSegment(
                            segment.Source,
                            StartIndex: startIndex,
                            Length: endIndex - startIndex,
                            IsProtectedMarker: false,
                            RepetitionCount: 1));
                    }
                    break;
                }

                if (matchIndex > startIndex)
                {
                    AppendConfiguredNormalizationSegment(destination, new ConfiguredNormalizationSegment(
                        segment.Source,
                        StartIndex: startIndex,
                        Length: matchIndex - startIndex,
                        IsProtectedMarker: false,
                        RepetitionCount: 1));
                }

                AppendConfiguredNormalizationSegment(destination, new ConfiguredNormalizationSegment(
                    configuredNormalizedValue,
                    StartIndex: 0,
                    Length: configuredNormalizedValue.Length,
                    IsProtectedMarker: true,
                    RepetitionCount: 1));
                startIndex = checked(matchIndex + target.Length);
            }
        }

        private static void AppendConfiguredNormalizationSegment(
            List<ConfiguredNormalizationSegment> destination,
            ConfiguredNormalizationSegment segment)
        {
            // Coalesce adjacent raw slices and marker repeats so dense matches avoid per-match strings or segments.
            // 密な一致で置換ごとの文字列やセグメントを作らないよう、隣接 raw slice とマーカー反復を結合します。
            if (segment.IsProtectedMarker && destination.Count > 0)
            {
                ConfiguredNormalizationSegment previous = destination[^1];
                if (previous.IsProtectedMarker
                    && string.Equals(previous.Source, segment.Source, StringComparison.Ordinal))
                {
                    destination[^1] = previous with
                    {
                        RepetitionCount = checked(previous.RepetitionCount + segment.RepetitionCount)
                    };
                    return;
                }
            }
            else if (!segment.IsProtectedMarker && destination.Count > 0)
            {
                ConfiguredNormalizationSegment previous = destination[^1];
                if (!previous.IsProtectedMarker
                    && ReferenceEquals(previous.Source, segment.Source)
                    && checked(previous.StartIndex + previous.Length) == segment.StartIndex)
                {
                    destination[^1] = previous with
                    {
                        Length = checked(previous.Length + segment.Length)
                    };
                    return;
                }
            }

            destination.Add(segment);
        }

        private readonly record struct ConfiguredNormalizationSegment(
            string Source,
            int StartIndex,
            int Length,
            bool IsProtectedMarker,
            int RepetitionCount);

        /// <summary>
        /// Selects a deterministic configured-value marker that cannot already occur in either raw IL input.
        /// raw IL のいずれにも存在しない、決定的な設定値正規化マーカーを選択します。
        /// </summary>
        private static string CreateCollisionFreeConfiguredNormalizedValue(params IReadOnlyList<string>[] lineCollections)
        {
            string markerStem = NORMALIZED_VALUE_PREFIX + "configured-value";
            var usedSuffixes = new HashSet<int>();

            // Collect every generated-form marker already present in the inputs before choosing the first unused form.
            // 最初の未使用形式を選ぶ前に、入力中に既に存在する生成可能形式のマーカーをすべて収集します。
            foreach (IReadOnlyList<string> lines in lineCollections)
            {
                foreach (string line in lines)
                {
                    RecordUsedConfiguredMarkerSuffixes(line, markerStem, usedSuffixes);
                }
            }

            // Suffix zero represents the unsuffixed marker; generated numeric suffixes start at one.
            // suffix 0 はsuffixなしのマーカーを表し、生成する数値suffixは1から始めます。
            if (!usedSuffixes.Contains(0))
            {
                return CONFIGURED_NORMALIZED_VALUE;
            }

            int suffix = 1;
            while (usedSuffixes.Contains(suffix))
            {
                suffix++;
            }
            return $"{markerStem}-{suffix}>";
        }

        private static void RecordUsedConfiguredMarkerSuffixes(
            string line,
            string markerStem,
            HashSet<int> usedSuffixes)
        {
            int searchIndex = 0;
            while (searchIndex < line.Length)
            {
                int markerIndex = line.IndexOf(markerStem, searchIndex, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    break;
                }

                int suffixStart = markerIndex + markerStem.Length;
                if (suffixStart < line.Length && line[suffixStart] == '>')
                {
                    usedSuffixes.Add(0);
                }
                else if (suffixStart < line.Length && line[suffixStart] == '-')
                {
                    TryRecordNumericSuffix(line, suffixStart + 1, usedSuffixes);
                }

                searchIndex = suffixStart;
            }
        }

        private static void TryRecordNumericSuffix(string line, int digitStart, HashSet<int> usedSuffixes)
        {
            // Accept only canonical positive suffixes because the selector never emits zero or leading zeros.
            // 選択処理は0や先頭0付きsuffixを生成しないため、正規の正整数suffixだけを対象にします。
            if (digitStart >= line.Length
                || line[digitStart] is < '1' or > '9')
            {
                return;
            }

            int value = 0;
            int index = digitStart;
            while (index < line.Length && line[index] is >= '0' and <= '9')
            {
                int digit = line[index] - '0';
                if (value > (int.MaxValue - digit) / 10)
                {
                    // Values outside Int32 cannot be emitted by the selector and therefore cannot collide.
                    // Int32範囲外の値は選択処理が生成できず、衝突しないため無視します。
                    return;
                }

                value = (value * 10) + digit;
                index++;
            }

            if (index < line.Length && line[index] == '>')
            {
                usedSuffixes.Add(value);
            }
        }

        private static string NormalizeSuffixAfterPrefix(string line, string prefix, string replacement)
        {
            ReadOnlySpan<char> content = line.AsSpan().TrimStart();
            if (!content.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line;
            }

            // Translate the trimmed offset back to the original line to preserve its indentation and prefix.
            // trim後のoffsetを元の行へ戻し、インデントとprefixを保持します。
            int prefixIndex = line.Length - content.Length;
            return string.Concat(line.AsSpan(0, prefixIndex + prefix.Length), replacement);
        }

        /// <summary>
        /// Compares two filtered IL line lists using signature-aware, block-based (order-independent) comparison.
        /// Parses IL into logical blocks (methods, classes, etc.) via <see cref="ILBlockParser"/>,
        /// extracts each block's signature (directive line) and content hash, then compares as multisets
        /// of (signature, hash) pairs. This handles compiler-induced reordering while correctly detecting
        /// content changes even when blocks with different signatures have identical bodies.
        /// フィルタ済み IL 行リストをシグネチャ対応のブロック単位（順序非依存）で比較します。
        /// <see cref="ILBlockParser"/> で IL を論理ブロック（メソッド、クラス等）に分割し、
        /// 各ブロックのシグネチャ（ディレクティブ行）とコンテンツハッシュを抽出してから
        /// (シグネチャ, ハッシュ) ペアのマルチセットとして比較します。コンパイラによる並び替えを
        /// 許容しつつ、異なるシグネチャのブロック間でのコンテンツ入れ替わりを正しく検知します。
        /// </summary>
        internal static bool BlockAwareSequenceEqual(IReadOnlyList<string> filteredLines1, IReadOnlyList<string> filteredLines2)
        {
            var blocks1 = ILBlockParser.ParseBlocks(filteredLines1);
            var blocks2 = ILBlockParser.ParseBlocks(filteredLines2);
            if (blocks1.Count != blocks2.Count)
            {
                return false;
            }

            var hashBag1 = BuildBlockHashBag(blocks1);
            var hashBag2 = BuildBlockHashBag(blocks2);
            if (hashBag1.Count != hashBag2.Count)
            {
                return false;
            }

            foreach (var kvp in hashBag1)
            {
                if (!hashBag2.TryGetValue(kvp.Key, out int count2) || count2 != kvp.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a multiset ((signature, hash) → count) from a list of IL blocks.
        /// Each block's signature is extracted via <see cref="ILBlockParser.ExtractBlockSignature"/>,
        /// ensuring that blocks are matched by both identity (signature) and content (hash).
        /// IL ブロックのリストからマルチセット（(シグネチャ, ハッシュ) → 出現回数）を構築します。
        /// 各ブロックのシグネチャは <see cref="ILBlockParser.ExtractBlockSignature"/> で抽出し、
        /// ブロックの同一性（シグネチャ）と内容（ハッシュ）の両方で照合します。
        /// </summary>
        private static Dictionary<(string Signature, string Hash), int> BuildBlockHashBag(List<List<string>> blocks)
        {
            var bag = new Dictionary<(string Signature, string Hash), int>();
            foreach (var block in blocks)
            {
                string signature = ILBlockParser.ExtractBlockSignature(block);
                string hash = ComputeBlockHash(block);
                var key = (signature, hash);
                bag.TryGetValue(key, out int count);
                bag[key] = count + 1;
            }

            return bag;
        }

        /// <summary>
        /// Computes a SHA256 hash of an IL block's content (all lines joined with newline).
        /// IL ブロックの内容（全行を改行で結合）の SHA256 ハッシュを計算します。
        /// </summary>
        private static string ComputeBlockHash(List<string> blockLines)
        {
            using var sha256 = SHA256.Create();
            var sb = new StringBuilder();
            for (int i = 0; i < blockLines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                // Trim leading/trailing whitespace to absorb indentation variations
                // 先頭・末尾空白をトリムしてインデント差異を吸収
                sb.Append(blockLines[i].Trim());
            }

            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        /// <summary>
        /// Validates configured IL line-ignore strings and returns warning messages for strings
        /// shorter than <see cref="IL_FILTER_STRING_MIN_LENGTH"/> characters.
        /// 設定された IL 行除外文字列を検証し、<see cref="IL_FILTER_STRING_MIN_LENGTH"/> 文字未満の
        /// 文字列に対する警告メッセージを返します。
        /// </summary>
        /// <returns>List of warning messages (empty if all strings are safe). / 警告メッセージのリスト（すべて安全なら空）。</returns>
        internal static List<string> ValidateILIgnoreContainingStrings(IReadOnlyList<string> configuredStrings)
        {
            var effectiveStrings = ILConfiguredSubstringHelper.GetEffectiveIgnoreLineSubstrings(configuredStrings);
            return ValidateMinimumLength(
                effectiveStrings,
                nameof(ConfigSettings.ILIgnoreLineContainingStrings),
                "exclude legitimate IL lines");
        }

        /// <summary>
        /// Validates configured IL normalization strings and returns warning messages for strings
        /// shorter than <see cref="IL_FILTER_STRING_MIN_LENGTH"/> characters.
        /// 設定された IL 正規化文字列を検証し、<see cref="IL_FILTER_STRING_MIN_LENGTH"/> 文字未満の
        /// 文字列に対する警告メッセージを返します。
        /// </summary>
        internal static List<string> ValidateILNormalizeContainingStrings(IReadOnlyList<string> configuredStrings)
        {
            var effectiveStrings = ILConfiguredSubstringHelper.GetEffectiveNormalizationSubstrings(configuredStrings);
            var warnings = new List<string>();
            int suppressedWarningCount = 0;
            AppendMinimumLengthWarnings(
                effectiveStrings,
                nameof(ConfigSettings.ILNormalizeContainingStrings),
                "normalize legitimate IL content",
                warnings,
                ref suppressedWarningCount);

            var configuredNonEmptyStrings = configuredStrings?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? new List<string>();
            foreach (var duplicate in configuredNonEmptyStrings
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key))
            {
                if (TryReserveNormalizationWarning(warnings, ref suppressedWarningCount))
                {
                    warnings.Add($"{nameof(ConfigSettings.ILNormalizeContainingStrings)}: \"{FormatConfiguredSubstringForWarning(duplicate)}\" is configured more than once. Duplicate entries are redundant.");
                }
            }

            for (int i = 0; i < effectiveStrings.Count; i++)
            {
                for (int j = i + 1; j < effectiveStrings.Count; j++)
                {
                    string first = effectiveStrings[i];
                    string second = effectiveStrings[j];
                    if (first.Contains(second, StringComparison.Ordinal) || second.Contains(first, StringComparison.Ordinal))
                    {
                        if (TryReserveNormalizationWarning(warnings, ref suppressedWarningCount))
                        {
                            warnings.Add($"{nameof(ConfigSettings.ILNormalizeContainingStrings)}: \"{FormatConfiguredSubstringForWarning(first)}\" and \"{FormatConfiguredSubstringForWarning(second)}\" overlap by containment. Configured normalization uses sequential ordinal matching on unreplaced raw text in listed order; inserted markers are not reprocessed, but the result may still depend on rule order. Use non-overlapping values or verify the reported application order.");
                        }
                    }
                }
            }

            if (suppressedWarningCount > 0)
            {
                warnings.Add($"{nameof(ConfigSettings.ILNormalizeContainingStrings)}: showing the first {IL_NORMALIZATION_WARNING_DETAIL_LIMIT} safety warning details; {suppressedWarningCount} additional warnings were suppressed. Reduce short, duplicate, or overlapping values to review every safety warning.");
            }

            return warnings;
        }

        private static void AppendMinimumLengthWarnings(
            IReadOnlyCollection<string> normalizedStrings,
            string settingName,
            string riskDescription,
            List<string> warnings,
            ref int suppressedWarningCount)
        {
            foreach (string value in normalizedStrings)
            {
                int unicodeCharacterCount = CountUnicodeCharacters(value);
                if (unicodeCharacterCount < IL_FILTER_STRING_MIN_LENGTH)
                {
                    if (TryReserveNormalizationWarning(warnings, ref suppressedWarningCount))
                    {
                        warnings.Add($"{settingName}: \"{FormatConfiguredSubstringForWarning(value)}\" is very short ({unicodeCharacterCount} chars) and may inadvertently {riskDescription}. Consider using a more specific pattern.");
                    }
                }
            }
        }

        private static bool TryReserveNormalizationWarning(
            List<string> warnings,
            ref int suppressedWarningCount)
        {
            if (warnings.Count < IL_NORMALIZATION_WARNING_DETAIL_LIMIT)
            {
                return true;
            }

            suppressedWarningCount++;
            return false;
        }

        private static List<string> ValidateMinimumLength(
            IReadOnlyCollection<string> normalizedStrings,
            string settingName,
            string riskDescription)
        {
            var warnings = new List<string>();
            if (normalizedStrings == null || normalizedStrings.Count == 0)
            {
                return warnings;
            }

            foreach (var s in normalizedStrings)
            {
                int unicodeCharacterCount = CountUnicodeCharacters(s);
                if (unicodeCharacterCount < IL_FILTER_STRING_MIN_LENGTH)
                {
                    warnings.Add($"{settingName}: \"{FormatConfiguredSubstringForWarning(s)}\" is very short ({unicodeCharacterCount} chars) and may inadvertently {riskDescription}. Consider using a more specific pattern.");
                }
            }

            return warnings;
        }

        private static int CountUnicodeCharacters(string value)
        {
            int count = 0;
            foreach (Rune _ in value.EnumerateRunes())
            {
                count++;
            }

            return count;
        }

        private static string FormatConfiguredSubstringForWarning(string value)
        {
            // Keep CommonMark punctuation inert and make invisible or ambiguous characters explicit.
            // CommonMark 記号を無効化し、不可視または曖昧な文字を明示します。
            var encoded = new StringBuilder(value.Length);
            int index = 0;
            while (index < value.Length)
            {
                char codeUnit = value[index];
                if (char.IsHighSurrogate(codeUnit)
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1]))
                {
                    int scalarValue = char.ConvertToUtf32(codeUnit, value[index + 1]);
                    AppendRuneForConfiguredSubstringWarning(encoded, new Rune(scalarValue));
                    index += 2;
                    continue;
                }

                if (char.IsSurrogate(codeUnit))
                {
                    // Preserve an unpaired UTF-16 code unit instead of replacing it with U+FFFD.
                    // 対にならない UTF-16 code unit を U+FFFD に置き換えず、その値を保持します。
                    AppendVisibleUnicodeEscapeForWarning(encoded, codeUnit);
                }
                else
                {
                    AppendRuneForConfiguredSubstringWarning(encoded, new Rune(codeUnit));
                }

                index++;
            }

            return encoded.ToString();
        }

        private static void AppendRuneForConfiguredSubstringWarning(StringBuilder encoded, Rune rune)
        {
            switch (rune.Value)
            {
                case '\\':
                    encoded.Append("&#92;&#92;");
                    break;
                case '\t':
                    encoded.Append("&#92;t");
                    break;
                case '\r':
                    encoded.Append("&#92;r");
                    break;
                case '\n':
                    encoded.Append("&#92;n");
                    break;
                default:
                    UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                    if (Rune.IsWhiteSpace(rune)
                        || category == UnicodeCategory.Control
                        || category == UnicodeCategory.Format
                        || category == UnicodeCategory.NonSpacingMark
                        || category == UnicodeCategory.SpacingCombiningMark
                        || category == UnicodeCategory.EnclosingMark)
                    {
                        AppendVisibleUnicodeEscapeForWarning(encoded, rune.Value);
                    }
                    else if (IsAsciiLetterOrDigitForWarning(rune.Value))
                    {
                        encoded.Append((char)rune.Value);
                    }
                    else
                    {
                        encoded.Append("&#")
                            .Append(rune.Value.ToString(CultureInfo.InvariantCulture))
                            .Append(';');
                    }
                    break;
            }
        }

        private static bool IsAsciiLetterOrDigitForWarning(int value) =>
            (value >= 'A' && value <= 'Z')
            || (value >= 'a' && value <= 'z')
            || (value >= '0' && value <= '9');

        private static void AppendVisibleUnicodeEscapeForWarning(StringBuilder destination, int value)
        {
            destination.Append(value <= 0xFFFF ? "&#92;u" : "&#92;U")
                .Append(value.ToString(value <= 0xFFFF ? "X4" : "X8", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Merges the disassembler labels used for old/new into a single comparison label.
        /// old/new で使用された逆アセンブラ表示ラベルを比較用に 1 つへまとめます。
        /// </summary>
        private static string? BuildComparisonDisassemblerLabel(string commandStringOld, string commandStringNew)
        {
            var oldLabel = BuildToolAndVersionLabel(commandStringOld);
            var newLabel = BuildToolAndVersionLabel(commandStringNew);
            if (string.IsNullOrWhiteSpace(oldLabel))
            {
                return newLabel;
            }

            if (string.IsNullOrWhiteSpace(newLabel))
            {
                return oldLabel;
            }

            if (string.Equals(oldLabel, newLabel, StringComparison.OrdinalIgnoreCase))
            {
                return oldLabel;
            }

            throw new InvalidOperationException($"IL comparison requires the same disassembler and version for old/new. old: '{oldLabel}', new: '{newLabel}'.");
        }

        /// <summary>
        /// Extracts a "toolName (version: x.y.z)" label from a command string.
        /// 実行コマンド文字列から「ツール名 (version: x.y.z)」形式を抽出します。
        /// </summary>
        private static string? BuildToolAndVersionLabel(string commandString)
        {
            if (string.IsNullOrWhiteSpace(commandString))
            {
                return null;
            }

            var tokens = ProcessHelper.TokenizeCommand(commandString);
            if (tokens.Count == 0)
            {
                return null;
            }

            string toolName;
            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase) &&
                tokens.Count >= 2 &&
                (string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase)))
            {
                toolName = Constants.DOTNET_ILDASM;
            }
            else
            {
                toolName = Path.GetFileName(tokens[0]);
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            if (string.Equals(toolName, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                toolName = Constants.DOTNET_ILDASM;
            }

            int versionStart = commandString.IndexOf(VERSION_LABEL_PREFIX, StringComparison.Ordinal);
            if (versionStart < 0)
            {
                return toolName;
            }

            int versionEnd = commandString.IndexOf(')', versionStart + VERSION_LABEL_PREFIX.Length);
            if (versionEnd <= versionStart)
            {
                return toolName;
            }

            string version = commandString.Substring(
                versionStart + VERSION_LABEL_PREFIX.Length,
                versionEnd - (versionStart + VERSION_LABEL_PREFIX.Length)).Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                return toolName;
            }

            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return $"{Constants.ILDASM_LABEL} (version: {version})";
            }

            return $"{toolName} (version: {version})";
        }
    }
}
