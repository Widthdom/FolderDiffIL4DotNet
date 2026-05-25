using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Partial class for analysis metadata: semantic changes, dependency changes, warnings, and disassembler info.
    /// 分析メタデータの partial クラス: セマンティック変更、依存関係変更、警告、逆アセンブラ情報。
    /// </summary>
    public sealed partial class FileDiffResultLists
    {
        // ──────────────────────────────────────────────
        // Disassembler metadata
        // 逆アセンブラメタデータ
        // ──────────────────────────────────────────────

        /// <summary>
        /// Disassembler names and versions used during actual tool execution.
        /// 実行中に使用された逆アセンブラの名称とバージョン（実ツール実行）。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersions { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Disassembler names and versions retrieved via cache.
        /// キャッシュ経由で利用された逆アセンブラの名称とバージョン。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersionsFromCache { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Disassembler availability results probed at startup (null until probed).
        /// 起動時にプローブされた逆アセンブラ利用可否の結果（プローブ前は null）。
        /// </summary>
        private IReadOnlyList<DisassemblerProbeResult>? _disassemblerAvailability;

        /// <summary>
        /// Gets or sets the disassembler availability probe results.
        /// Thread-safe via <see cref="Volatile"/>.
        /// 逆アセンブラ利用可否プローブ結果を取得・設定します。
        /// <see cref="Volatile"/> によりスレッドセーフです。
        /// </summary>
        public IReadOnlyList<DisassemblerProbeResult>? DisassemblerAvailability
        {
            get => Volatile.Read(ref _disassemblerAvailability);
            set => Volatile.Write(ref _disassemblerAvailability, value);
        }

        /// <summary>
        /// Records the disassembler tool name and version used.
        /// 使用した逆アセンブラ名とバージョンを記録します。
        /// </summary>
        public void RecordDisassemblerToolVersion(string toolName, string? version, bool fromCache = false)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return;
            }
            var label = string.IsNullOrWhiteSpace(version) ? toolName : $"{toolName} (version: {version})";
            var target = fromCache ? DisassemblerToolVersionsFromCache : DisassemblerToolVersions;
            target[label] = 0;
        }

        // ──────────────────────────────────────────────
        // Timestamp warnings
        // タイムスタンプ警告
        // ──────────────────────────────────────────────

        /// <summary>
        /// Warnings for Modified files where the new file's timestamp is older than the old file's.
        /// Modified と判定されたファイルのうち、new 側の更新日時が old 側より古いものの警告一覧。
        /// </summary>
        public ConcurrentDictionary<string, FileTimestampRegressionWarning> NewFileTimestampOlderThanOldWarnings { get; } = new ConcurrentDictionary<string, FileTimestampRegressionWarning>(s_relativePathKeyComparer);

        /// <summary>
        /// Returns <see langword="true"/> when at least one modified file has a newer-side timestamp older than the older-side.
        /// Modified ファイルのうち新側タイムスタンプが旧側より古いものが 1 件以上ある場合に <see langword="true"/> を返します。
        /// </summary>
        public bool HasAnyNewFileTimestampOlderThanOldWarning => !NewFileTimestampOlderThanOldWarnings.IsEmpty;

        /// <summary>
        /// Records a warning when a Modified file's new-side timestamp is older than its old-side timestamp.
        /// Modified と判定されたファイルについて、new 側の更新日時が old 側より古い場合の警告を記録します。
        /// </summary>
        public void RecordNewFileTimestampOlderThanOldWarning(string fileRelativePath, string oldTimestamp, string newTimestamp)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }

            NewFileTimestampOlderThanOldWarnings[fileRelativePath] = new FileTimestampRegressionWarning(fileRelativePath, oldTimestamp, newTimestamp);
        }

        // ──────────────────────────────────────────────
        // IL filter warnings
        // IL フィルタ警告
        // ──────────────────────────────────────────────

        /// <summary>
        /// Warning messages for potentially over-broad IL filter strings configured via
        /// <c>ILIgnoreLineContainingStrings</c>. Recorded during diff execution and
        /// rendered in both Markdown and HTML report Warnings sections.
        /// <c>ILIgnoreLineContainingStrings</c> で設定された過度に広範な IL フィルタ文字列に対する
        /// 警告メッセージ。差分実行時に記録され、Markdown および HTML レポートの警告セクションに表示されます。
        /// </summary>
        public ConcurrentBag<string> ILFilterWarnings { get; } = new ConcurrentBag<string>();

        /// <summary>
        /// Returns <see langword="true"/> when at least one IL filter warning exists.
        /// IL フィルタ警告が 1 件以上ある場合に <see langword="true"/> を返します。
        /// </summary>
        public bool HasAnyILFilterWarning => !ILFilterWarnings.IsEmpty;

        // ──────────────────────────────────────────────
        // Semantic & dependency analysis
        // セマンティック・依存関係分析
        // ──────────────────────────────────────────────

        /// <summary>
        /// Assembly semantic change summaries for ILMismatch files, keyed by file relative path.
        /// ILMismatch ファイルに対するアセンブリセマンティック変更要約。キーはファイルの相対パス。
        /// </summary>
        public ConcurrentDictionary<string, AssemblySemanticChangesSummary> FileRelativePathToAssemblySemanticChanges { get; } = new ConcurrentDictionary<string, AssemblySemanticChangesSummary>(StringComparer.Ordinal);

        /// <summary>
        /// Dependency change summaries for .deps.json files, keyed by file relative path.
        /// .deps.json ファイルに対する依存関係変更要約。キーはファイルの相対パス。
        /// </summary>
        public ConcurrentDictionary<string, DependencyChangeSummary> FileRelativePathToDependencyChanges { get; } = new ConcurrentDictionary<string, DependencyChangeSummary>(StringComparer.Ordinal);

        /// <summary>
        /// Estimated change pattern tags per file, keyed by file relative path.
        /// ファイルごとの推定変更パターンタグ。キーはファイルの相対パス。
        /// </summary>
        public ConcurrentDictionary<string, IReadOnlyList<ChangeTag>> FileRelativePathToChangeTags { get; } = new ConcurrentDictionary<string, IReadOnlyList<ChangeTag>>(StringComparer.Ordinal);

        /// <summary>
        /// Returns the maximum <see cref="ChangeImportance"/> for a file, or <see langword="null"/> if no semantic changes exist.
        /// ファイルの最大 <see cref="ChangeImportance"/> を返します。セマンティック変更が存在しない場合は <see langword="null"/>。
        /// </summary>
        public ChangeImportance? GetMaxImportance(string fileRelativePath)
        {
            ChangeImportance? max = null;
            if (FileRelativePathToAssemblySemanticChanges.TryGetValue(fileRelativePath, out var summary) && summary.HasChanges)
                max = summary.MaxImportance;
            if (FileRelativePathToDependencyChanges.TryGetValue(fileRelativePath, out var depSummary) && depSummary.HasChanges)
            {
                var depMax = depSummary.MaxImportance;
                max = max == null ? depMax : (depMax > max.Value ? depMax : max.Value);
            }
            return max;
        }

        /// <summary>
        /// Returns all distinct <see cref="ChangeImportance"/> levels for a file across assembly semantic and dependency changes.
        /// ファイルのアセンブリセマンティック変更と依存関係変更にわたる重複のない <see cref="ChangeImportance"/> レベルをすべて返します。
        /// </summary>
        public HashSet<ChangeImportance> GetAllImportanceLevels(string fileRelativePath)
        {
            var levels = new HashSet<ChangeImportance>();
            if (FileRelativePathToAssemblySemanticChanges.TryGetValue(fileRelativePath, out var summary))
                foreach (var e in summary.Entries) levels.Add(e.Importance);
            if (FileRelativePathToDependencyChanges.TryGetValue(fileRelativePath, out var depSummary))
                foreach (var e in depSummary.Entries) levels.Add(e.Importance);
            return levels;
        }
    }
}
