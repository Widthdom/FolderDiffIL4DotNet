using System;
using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Provides predefined spinner animation themes for CLI progress display.
    /// CLI プログレス表示用の定義済みスピナーアニメーションテーマを提供します。
    /// </summary>
    internal static class SpinnerThemes
    {
        /// <summary>
        /// Applies the specified spinner theme from CLI options to the config builder.
        /// Returns early when multiple spinners are detected (falls back to matcha).
        /// CLI オプションから指定されたスピナーテーマを設定ビルダーに適用します。
        /// 複数スピナー検出時はマッチャにフォールバックして早期リターンします。
        /// </summary>
        internal static void Apply(ConfigSettingsBuilder builder, CliOptions opts)
        {
            // Easter egg: when multiple spinner themes are specified, show a humorous message
            // and fall back to matcha theme.
            // イースターエッグ: 複数のスピナーテーマが同時指定された場合、ユーモラスなメッセージを
            // 表示して抹茶テーマにフォールバック。
            if (opts.MultipleSpinnersDetected)
            {
                Console.WriteLine(MULTIPLE_SPINNERS_MESSAGE);
                ApplyMatcha(builder);
                return;
            }

            // --random-spinner: randomly select one of the 7 themes
            // --random-spinner: 7つのテーマからランダムに1つを選択
            if (opts.RandomSpinner)
            {
                ApplyRandom(builder);
                return;
            }

            if (opts.Coffee) ApplyCoffee(builder);
            if (opts.Beer) ApplyBeer(builder);
            if (opts.Matcha) ApplyMatcha(builder);
            if (opts.Whisky) ApplyWhisky(builder);
            if (opts.Wine) ApplyWine(builder);
            if (opts.Ramen) ApplyRamen(builder);
            if (opts.Sushi) ApplySushi(builder);
        }

        /// <summary>
        /// Easter egg message shown when multiple spinner theme flags are specified simultaneously.
        /// 複数のスピナーテーマフラグが同時指定されたときに表示するイースターエッグメッセージ。
        /// </summary>
        internal const string MULTIPLE_SPINNERS_MESSAGE =
            "Mixing drinks is not recommended. How about some matcha instead? / 飲み物の同時摂取は推奨しません。マッチャにしませんか？";

        // ── Theme applicators ────────────────────────────────────────────────
        // テーマ適用メソッド

        private static void ApplyCoffee(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "☕ Grinding    ", "☕ Grinding.   ", "☕ Grinding..  ", "☕ Grinding... ",
                "☕ Heating     ", "☕ Heating.    ", "☕ Heating..   ", "☕ Heating...  ",
                "☕ Brewing     ", "☕ Brewing.    ", "☕ Brewing..   ", "☕ Brewing...  ",
            };

        private static void ApplyBeer(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🍺 Tapping    ", "🍺 Tapping.   ", "🍺 Tapping..  ", "🍺 Tapping... ",
                "🍺 Pouring    ", "🍺 Pouring.   ", "🍺 Pouring..  ", "🍺 Pouring... ",
                "🍺 Foaming    ", "🍺 Foaming.   ", "🍺 Foaming..  ", "🍺 Foaming... ",
                "🍺 Cheers!    ",
            };

        internal static void ApplyMatcha(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🍵 Sifting      ", "🍵 Sifting.     ", "🍵 Sifting..    ", "🍵 Sifting...   ",
                "🍵 Pouring      ", "🍵 Pouring.     ", "🍵 Pouring..    ", "🍵 Pouring...   ",
                "🍵 Whisking     ", "🍵 Whisking.    ", "🍵 Whisking..   ", "🍵 Whisking...  ",
                "🍵 Douzo!       ",
            };

        private static void ApplyWhisky(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🥃 Mashing       ", "🥃 Mashing.      ", "🥃 Mashing..     ", "🥃 Mashing...    ",
                "🥃 Distilling    ", "🥃 Distilling.   ", "🥃 Distilling..  ", "🥃 Distilling... ",
                "🥃 Aging         ", "🥃 Aging.        ", "🥃 Aging..       ", "🥃 Aging...      ",
                "🥃 Slainte!      ",
            };

        private static void ApplyWine(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🍷 Crushing     ", "🍷 Crushing.    ", "🍷 Crushing..   ", "🍷 Crushing...  ",
                "🍷 Aging        ", "🍷 Aging.       ", "🍷 Aging..      ", "🍷 Aging...     ",
                "🍷 Pouring      ", "🍷 Pouring.     ", "🍷 Pouring..    ", "🍷 Pouring...   ",
                "🍷 Sante!       ",
            };

        private static void ApplyRamen(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🍜 Boiling       ", "🍜 Boiling.      ", "🍜 Boiling..     ", "🍜 Boiling...    ",
                "🍜 Steaming      ", "🍜 Steaming.     ", "🍜 Steaming..    ", "🍜 Steaming...   ",
                "🍜 Slurping      ", "🍜 Slurping.     ", "🍜 Slurping..    ", "🍜 Slurping...   ",
                "🍜 Itadakimasu!  ",
            };

        private static void ApplySushi(ConfigSettingsBuilder builder) =>
            builder.SpinnerFrames = new List<string>
            {
                "🍣 Slicing       ", "🍣 Slicing.      ", "🍣 Slicing..     ", "🍣 Slicing...    ",
                "🍣 Shaping       ", "🍣 Shaping.      ", "🍣 Shaping..     ", "🍣 Shaping...    ",
                "🍣 Pressing      ", "🍣 Pressing.     ", "🍣 Pressing..    ", "🍣 Pressing...   ",
                "🍣 Itadakimasu!  ",
            };

        /// <summary>
        /// Randomly selects one of the 7 spinner themes and applies it to the builder.
        /// 7つのスピナーテーマからランダムに1つを選択してビルダーに適用します。
        /// </summary>
        private static void ApplyRandom(ConfigSettingsBuilder builder)
        {
            var themes = new Action<ConfigSettingsBuilder>[]
            {
                ApplyCoffee, ApplyBeer, ApplyMatcha, ApplyWhisky,
                ApplyWine, ApplyRamen, ApplySushi,
            };

            int index = Random.Shared.Next(themes.Length);
            themes[index](builder);
        }
    }
}
