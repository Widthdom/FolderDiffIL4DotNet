using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    // IL comparison, cache, and disassembler settings partial.
    // IL 比較・キャッシュ・逆アセンブラ設定の部分クラス。
    public sealed partial class ConfigSettings
    {
        // ── IL comparison defaults / IL 比較デフォルト ────────────────────────
        /// <summary>Default value for <see cref="ShouldOutputILText"/>. / <see cref="ShouldOutputILText"/> の既定値。</summary>
        public const bool DefaultShouldOutputILText = true;
        /// <summary>Default value for <see cref="ShouldIgnoreILLinesContainingConfiguredStrings"/>. / <see cref="ShouldIgnoreILLinesContainingConfiguredStrings"/> の既定値。</summary>
        public const bool DefaultShouldIgnoreILLinesContainingConfiguredStrings = false;
        /// <summary>Default value for <see cref="SkipIL"/>. / <see cref="SkipIL"/> の既定値。</summary>
        public const bool DefaultSkipIL = false;
        /// <summary>Default value for <see cref="ShouldIgnoreMVID"/>. / <see cref="ShouldIgnoreMVID"/> の既定値。</summary>
        public const bool DefaultShouldIgnoreMVID = true;

        // ── IL cache defaults / IL キャッシュデフォルト ───────────────────────
        /// <summary>Default value for <see cref="EnableILCache"/>. / <see cref="EnableILCache"/> の既定値。</summary>
        public const bool DefaultEnableILCache = true;
        /// <summary>Default value for <see cref="ILCacheStatsLogIntervalSeconds"/>. / <see cref="ILCacheStatsLogIntervalSeconds"/> の既定値。</summary>
        public const int DefaultILCacheStatsLogIntervalSeconds = 60;
        /// <summary>Default value for <see cref="ILCacheMaxDiskFileCount"/>. / <see cref="ILCacheMaxDiskFileCount"/> の既定値。</summary>
        public const int DefaultILCacheMaxDiskFileCount = 1000;
        /// <summary>Default value for <see cref="ILCacheMaxDiskMegabytes"/>. / <see cref="ILCacheMaxDiskMegabytes"/> の既定値。</summary>
        public const int DefaultILCacheMaxDiskMegabytes = 512;
        /// <summary>Default value for <see cref="ILCacheMaxMemoryMegabytes"/>. / <see cref="ILCacheMaxMemoryMegabytes"/> の既定値。</summary>
        public const int DefaultILCacheMaxMemoryMegabytes = 256;
        /// <summary>Default value for <see cref="ILPrecomputeBatchSize"/>. / <see cref="ILPrecomputeBatchSize"/> の既定値。</summary>
        public const int DefaultILPrecomputeBatchSize = 2048;

        // ── Disassembler defaults / 逆アセンブラデフォルト ────────────────────
        /// <summary>Default value for <see cref="DisassemblerBlacklistTtlMinutes"/>. / <see cref="DisassemblerBlacklistTtlMinutes"/> の既定値。</summary>
        public const int DefaultDisassemblerBlacklistTtlMinutes = 10;
        /// <summary>Default value for <see cref="DisassemblerTimeoutSeconds"/>. / <see cref="DisassemblerTimeoutSeconds"/> の既定値。</summary>
        public const int DefaultDisassemblerTimeoutSeconds = 60;

        // ── IL comparison properties / IL 比較プロパティ ─────────────────────

        /// <summary>
        /// Whether to output full IL text.
        /// IL全文を出力するか否か。
        /// </summary>
        public bool ShouldOutputILText { get; }

        /// <summary>
        /// Whether to ignore IL lines that contain any of the configured strings during comparison.
        /// IL 比較時に、指定文字列を「含む」行を無視するかどうか。
        /// </summary>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; }

        /// <summary>
        /// List of strings to ignore in IL lines during comparison.
        /// IL 比較時に無視対象とする文字列リスト。
        /// </summary>
        public IReadOnlyList<string> ILIgnoreLineContainingStrings { get; }

        /// <summary>
        /// Whether to skip IL comparison for .NET assemblies.
        /// .NET アセンブリの IL 比較をスキップするかどうか。
        /// </summary>
        public bool SkipIL { get; }

        /// <summary>
        /// Whether to ignore MVID (Module Version ID) lines during IL comparison.
        /// When false, MVID differences are included in the comparison, which can detect
        /// recompilation even when the source code is identical.
        /// IL 比較時に MVID（Module Version ID）行を無視するかどうか。
        /// false の場合、ソースコードが同一でも再コンパイルを検出するために MVID 差異が比較に含まれる。
        /// </summary>
        public bool ShouldIgnoreMVID { get; }

        // ── IL cache properties / IL キャッシュプロパティ ─────────────────────

        /// <summary>
        /// Whether to cache IL disassembly results.
        /// IL 逆アセンブル結果をキャッシュするか。
        /// </summary>
        public bool EnableILCache { get; }

        /// <summary>
        /// Absolute path to the IL cache directory.
        /// IL キャッシュ格納ディレクトリ。
        /// </summary>
        public string ILCacheDirectoryAbsolutePath { get; }

        /// <summary>
        /// Interval (seconds) for IL cache statistics log output.
        /// IL キャッシュ統計ログの出力間隔（秒）。
        /// </summary>
        public int ILCacheStatsLogIntervalSeconds { get; }

        /// <summary>
        /// Maximum number of files in the on-disk IL cache.
        /// ディスク IL キャッシュの最大ファイル数。
        /// </summary>
        public int ILCacheMaxDiskFileCount { get; }

        /// <summary>
        /// Size limit (MB) for the on-disk IL cache.
        /// ディスク IL キャッシュのサイズ上限（MB）。
        /// </summary>
        public int ILCacheMaxDiskMegabytes { get; }

        /// <summary>
        /// Memory budget (MB) for the in-memory IL cache. Default is 256 MB; set 0 to restore unlimited mode (entry-count limit only).
        /// メモリ内 IL キャッシュのメモリ予算（MB）。既定値は 256 MB。0 を指定すると従来どおり無制限（エントリ数上限のみ）に戻ります。
        /// </summary>
        public int ILCacheMaxMemoryMegabytes { get; }

        /// <summary>
        /// Batch size for IL precomputation.
        /// IL 事前計算のバッチサイズ。
        /// </summary>
        public int ILPrecomputeBatchSize { get; }

        // ── Disassembler properties / 逆アセンブラプロパティ ──────────────────

        /// <summary>
        /// Blacklist TTL (minutes) for disassembler tools.
        /// 逆アセンブラツールのブラックリスト有効期間（分）。
        /// </summary>
        public int DisassemblerBlacklistTtlMinutes { get; }

        /// <summary>
        /// Timeout (seconds) for each disassembler process invocation. 0 means no timeout.
        /// 各逆アセンブラプロセス実行のタイムアウト（秒）。0 はタイムアウトなし。
        /// </summary>
        public int DisassemblerTimeoutSeconds { get; }
    }
}
