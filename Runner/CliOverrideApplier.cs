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

            if (opts.CreatorIlIgnoreProfile != null)
            {
                builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
                var mergedStrings = new List<string>(builder.ILIgnoreLineContainingStrings);
                var seen = new HashSet<string>(mergedStrings, System.StringComparer.Ordinal);
                foreach (var value in CreatorPrivilegeIlIgnoreProfiles.GetStringsOrThrow(opts.CreatorIlIgnoreProfile))
                {
                    if (seen.Add(value))
                    {
                        mergedStrings.Add(value);
                    }
                }

                builder.ILIgnoreLineContainingStrings = mergedStrings;
            }

            SpinnerThemes.Apply(builder, opts);
        }
    }
}
