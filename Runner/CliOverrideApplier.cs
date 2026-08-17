using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Applies CLI option overrides to a <see cref="ConfigSettingsBuilder"/>,
    /// giving CLI flags priority over config.json values.
    /// CLI オプションのオーバーライドを <see cref="ConfigSettingsBuilder"/> に適用し、
    /// config.json の値より CLI フラグを優先させます。
    /// </summary>
    internal static class CliOverrideApplier
    {
        /// <summary>
        /// Overrides <paramref name="builder"/> values with CLI options from <paramref name="opts"/>.
        /// <paramref name="opts"/> の CLI オプションで <paramref name="builder"/> の値を上書きします。
        /// </summary>
        internal static void Apply(ConfigSettingsBuilder builder, CliOptions opts)
        {
            if (opts.ThreadsOverride.HasValue)
            {
                builder.MaxParallelism = opts.ThreadsOverride.Value;
            }

            if (opts.NoIlCache)
            {
                builder.EnableILCache = false;
            }

            if (opts.SkipIL)
            {
                builder.SkipIL = true;
            }

            if (opts.NoTimestampWarnings)
            {
                builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false;
            }

            string? creatorProfile = opts.CreatorIlIgnoreProfile;
            if (opts.Creator && creatorProfile == null)
            {
                creatorProfile = CreatorPrivilegeIlIgnoreProfiles.DefaultProfileName;
            }

            if (creatorProfile != null)
            {
                builder.ShouldILNormalizeContainingConfiguredStrings = true;
                var configuredStrings = builder.ILNormalizeContainingStrings;
                var orderedStrings = new List<string>(
                    CreatorPrivilegeIlIgnoreProfiles.GetStringsOrThrow(creatorProfile));

                // Creator defaults form the normalization baseline. Keep configured values after
                // them, preserving duplicates so validation can report cross-source relationships.
                // creator 既定値を正規化の基盤として先に置き、設定値をその後へ追加します。
                // 重複は保持し、双方にまたがる重複・包含関係を検証できるようにします。
                orderedStrings.AddRange(configuredStrings);
                builder.ILNormalizeContainingStrings = orderedStrings;
            }

            SpinnerThemes.Apply(builder, opts);
        }
    }
}
