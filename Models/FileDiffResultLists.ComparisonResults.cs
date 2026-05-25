using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Partial class for comparison result storage: diff details, disassembler labels, and ignored files.
    /// 比較結果ストレージの partial クラス: 差分詳細、逆アセンブララベル、除外ファイル。
    /// </summary>
    public sealed partial class FileDiffResultLists
    {
        private static readonly StringComparer s_relativePathKeyComparer = StringComparer.Ordinal;

        // ──────────────────────────────────────────────
        // Diff detail results
        // 差分詳細結果
        // ──────────────────────────────────────────────

        /// <summary>
        /// Thread-safe dictionary mapping file relative paths to their comparison results.
        /// ファイル間の比較結果を保持する辞書（並列比較で安全に書き込みできるよう ConcurrentDictionary）。
        /// </summary>
        public ConcurrentDictionary<string, DiffDetailResult> FileRelativePathToDiffDetailDictionary { get; } = new ConcurrentDictionary<string, DiffDetailResult>(StringComparer.Ordinal);

        /// <summary>
        /// Disassembler display label per IL-compared file (e.g., "dotnet-ildasm (version: 0.12.0)").
        /// IL 比較を実施したファイルごとの逆アセンブラ表示ラベル（例: "dotnet-ildasm (version: 0.12.0)"）。
        /// </summary>
        public ConcurrentDictionary<string, string> FileRelativePathToIlDisassemblerLabelDictionary { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// .NET SDK / target framework version per .NET assembly (e.g., ".NET 8.0.413", "net8.0").
        /// Populated for both old and new assemblies as "old → new" when different, or single value when identical.
        /// .NET アセンブリごとの .NET SDK / ターゲットフレームワークバージョン。
        /// 旧新が異なる場合は "old → new"、同一なら単一値を格納。
        /// </summary>
        public ConcurrentDictionary<string, string> FileRelativePathToSdkVersionDictionary { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Returns <see langword="true"/> when at least one file comparison resulted in a SHA256 mismatch.
        /// 少なくとも 1 件のファイル比較で SHA256 不一致が発生した場合に <see langword="true"/> を返します。
        /// </summary>
        public bool HasAnySha256Mismatch => FileRelativePathToDiffDetailDictionary.Values.Any(result => result == DiffDetailResult.SHA256Mismatch);

        /// <summary>
        /// Records the comparison result for a file, optionally associating a disassembler label for IL comparisons.
        /// ファイルの比較結果を記録します。IL 比較時は逆アセンブラ表示ラベルも関連付けます。
        /// </summary>
        public void RecordDiffDetail(string fileRelativePath, DiffDetailResult diffDetailResult, string? ilDisassemblerLabel = null)
        {
            // Upsert: overwrite if exists, add if not (thread-safe)
            // 既に存在する場合は上書き、存在しなければ追加（スレッドセーフ）
            FileRelativePathToDiffDetailDictionary[fileRelativePath] = diffDetailResult;
            if (diffDetailResult == DiffDetailResult.ILMatch || diffDetailResult == DiffDetailResult.ILMismatch)
            {
                if (!string.IsNullOrWhiteSpace(ilDisassemblerLabel))
                {
                    FileRelativePathToIlDisassemblerLabelDictionary[fileRelativePath] = ilDisassemblerLabel;
                }
                else
                {
                    FileRelativePathToIlDisassemblerLabelDictionary.TryRemove(fileRelativePath, out _);
                }
            }
            else
            {
                FileRelativePathToIlDisassemblerLabelDictionary.TryRemove(fileRelativePath, out _);
            }
        }

        // ──────────────────────────────────────────────
        // Ignored files
        // 除外ファイル
        // ──────────────────────────────────────────────

        /// <summary>
        /// Relative paths and folder locations (old/new) of files excluded by IgnoredExtensions.
        /// IgnoredExtensions 対象ファイルの相対パスと所在（旧/新フォルダ）情報。
        /// </summary>
        public ConcurrentDictionary<string, IgnoredFileLocation> IgnoredFilesRelativePathToLocation { get; } = new ConcurrentDictionary<string, IgnoredFileLocation>(s_relativePathKeyComparer);

        /// <summary>
        /// Records the location info for a file matched by IgnoredExtensions.
        /// IgnoredExtensions に該当したファイルの所在情報を記録します。
        /// </summary>
        public void RecordIgnoredFile(string fileRelativePath, IgnoredFileLocation location)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }
            IgnoredFilesRelativePathToLocation.AddOrUpdate(fileRelativePath, location, (_, existing) => existing | location);
        }
    }
}
