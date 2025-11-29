using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// フォルダ間の差分を比較するサービスクラス
    /// </summary>
    public sealed class FolderDiffService
    {
        #region private read only member variables
        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// 進捗状況を報告するためのユーティリティ
        /// </summary>
        private readonly ProgressReportService _progressReporter;

        /// <summary>
        /// 旧バージョン側（比較元）フォルダの絶対パス
        /// </summary>
        private readonly string _oldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）フォルダの絶対パス
        /// </summary>
        private readonly string _newFolderAbsolutePath;

        /// <summary>
        /// レポート出力先の絶対パス
        /// </summary>
        private readonly string _reportsFolderAbsolutePath;

        /// <summary>
        /// IL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilOutputFolderAbsolutePath;

        /// <summary>
        /// 旧バージョン側（比較元）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilOldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilNewFolderAbsolutePath;

        /// <summary>
        /// IL 出力サービス
        /// </summary>
        private readonly ILOutputService _ilOutputService;

        /// <summary>
        /// ファイル比較サービス
        /// </summary>
        private readonly FileDiffService _fileDiffService;

        /// <summary>
        /// 実行時に決定されるネットワーク最適化フラグ（Auto検知 + 手動フラグ を統合）。
        /// </summary>
        private readonly bool _optimizeForNetworkShares;

        /// <summary>
        /// AutoDetectNetworkShares に基づく「旧フォルダ」側の自動検出結果。
        /// true の場合、この実行では旧フォルダパスがネットワーク共有（UNC/NFS/SMB/SSHFS 等）と判定されています。
        /// </summary>
        private readonly bool _detectedNetworkOld;

        /// <summary>
        /// AutoDetectNetworkShares に基づく「新フォルダ」側の自動検出結果。
        /// true の場合、この実行では新フォルダパスがネットワーク共有（UNC/NFS/SMB/SSHFS 等）と判定されています。
        /// </summary>
        private readonly bool _detectedNetworkNew;
        #endregion

        /// <summary>
        /// コンストラクタ。
        /// 必須パラメータをフィールドに束ね、IL 出力先（old/new サブディレクトリのパス）も初期化します。
        /// </summary>
        /// <param name="config">アプリケーションの設定情報</param>
        /// <param name="progressReporter">進捗状況を報告するためのユーティリティ</param>
        /// <param name="oldFolderAbsolutePath">旧バージョン側（比較元）フォルダの絶対パス</param>
        /// <param name="newFolderAbsolutePath">新バージョン側（比較先）フォルダの絶対パス</param>
        /// <param name="reportsFolderAbsolutePath">レポート出力先の絶対パス</param>
        /// <exception cref="ArgumentNullException">config または progressReporter または oldFolderAbsolutePath または newFolderAbsolutePath または reportsFolderAbsolutePath が null の場合。</exception>
        public FolderDiffService(ConfigSettings config, ProgressReportService progressReporter, string oldFolderAbsolutePath, string newFolderAbsolutePath, string reportsFolderAbsolutePath)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _progressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
            _oldFolderAbsolutePath = oldFolderAbsolutePath ?? throw new ArgumentNullException(nameof(oldFolderAbsolutePath));
            _newFolderAbsolutePath = newFolderAbsolutePath ?? throw new ArgumentNullException(nameof(newFolderAbsolutePath));
            _reportsFolderAbsolutePath = reportsFolderAbsolutePath ?? throw new ArgumentNullException(nameof(reportsFolderAbsolutePath));
            _ilOutputFolderAbsolutePath = Path.Combine(_reportsFolderAbsolutePath, Constants.IL_FOLDER_NAME);
            _ilOldFolderAbsolutePath = Path.Combine(_ilOutputFolderAbsolutePath, Constants.IL_OLD_SUB_DIR);
            _ilNewFolderAbsolutePath = Path.Combine(_ilOutputFolderAbsolutePath, Constants.IL_NEW_SUB_DIR);
            _ilOutputService = new ILOutputService(_config, _ilOutputFolderAbsolutePath, _ilOldFolderAbsolutePath, _ilNewFolderAbsolutePath);

            // Auto検知（UNC/NFS/SSHFS等） + 手動指定のORで最終判定
            _detectedNetworkOld = _config.AutoDetectNetworkShares && Utility.IsLikelyNetworkPath(_oldFolderAbsolutePath);
            _detectedNetworkNew = _config.AutoDetectNetworkShares && Utility.IsLikelyNetworkPath(_newFolderAbsolutePath);
            _optimizeForNetworkShares = _config.OptimizeForNetworkShares || _detectedNetworkOld || _detectedNetworkNew;

            _fileDiffService = new FileDiffService(_config, _ilOutputService, _oldFolderAbsolutePath, _newFolderAbsolutePath, _optimizeForNetworkShares);
        }

        /// <summary>
        /// 2つのフォルダ（old/new）を再帰的に走査し、無視拡張子を除外した上で
        /// Unchanged / Added / Removed / Modified を分類し、FileDiffResultLists に集計します。
        /// 進捗は old と new の相対パスの和集合件数を母数として 1 件ごとに報告します。
        /// </summary>
        /// <remarks>
        /// 主な処理の流れ:
        /// 1) old/new のファイル一覧取得（IgnoredExtensions を除外）
        /// 2) 進捗の母数を old∪new の相対パス件数で算出し、1 件処理ごとに ProgressReporter に通知
        /// 3) old 側を基準に走査し、各相対パスについて以下の順で比較:
        ///    - MD5 ハッシュ一致なら Unchanged
        ///    - .NET 実行可能なら IL を逆アセンブルして比較（MVID 行は除外）。
        ///    - TextFileExtensions に含まれる拡張子ならテキスト差分で比較
        ///    - いずれも一致しなければ Modified と判定
        ///    new 側に存在しない場合は Removed として記録
        /// 4) old 側に存在せず new 側のみに残ったパスは Added として記録
        ///
        /// 補足:
        /// - ShouldOutputILText が true の場合、各側の IL テキストを
        ///   Reports/<コマンドライン第3引数に指定したレポートのラベル>/IL/old と Reports/<コマンドライン第3引数に指定したレポートのラベル>/IL/newに保存します
        ///   に保存します（MVID 行は出力前に除外）。
        /// - IL 比較では実際に使用したツールとコマンド、バージョン情報をログへ併記します。
        /// - 分類結果と詳細は FileDiffResultLists に集計され、後段のレポート生成で利用されます。
        /// </remarks>
        /// <returns>非同期操作を表すタスク。</returns>
        /// <exception cref="IOException">ファイルの列挙や読み書きに失敗した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">アクセス権限が不足している場合。</exception>
        /// <exception cref="DirectoryNotFoundException">指定フォルダが存在しない場合。</exception>
        /// <exception cref="InvalidOperationException">IL 逆アセンブルツールが見つからない、または実行に失敗した場合。</exception>
        /// <exception cref="Exception">ログの初期化/クローズなど、その他の予期しないエラーが発生した場合。</exception>
        public async Task ExecuteFolderDiffAsync()
        {
            // 実行モードを最初に出力
            var mode = _optimizeForNetworkShares ? "Server/NAS-optimized" : "Local-optimized";
            var reason = $"manual={_config.OptimizeForNetworkShares}, auto={_config.AutoDetectNetworkShares}, oldIsNetwork={_detectedNetworkOld}, newIsNetwork={_detectedNetworkNew}";
            LoggerService.LogMessage($"[INFO] Execution mode: {mode} ({reason})", shouldOutputMessageToConsole: true);

            FileDiffResultLists.IgnoredFilesRelativePathToLocation.Clear();
            FileDiffResultLists.UnchangedFilesRelativePath.Clear();
            FileDiffResultLists.AddedFilesAbsolutePath.Clear();
            FileDiffResultLists.RemovedFilesAbsolutePath.Clear();
            FileDiffResultLists.ModifiedFilesRelativePath.Clear();
            FileDiffResultLists.FileRelativePathToDiffDetailDictionary.Clear();
            try
            {
                // 長時間の事前処理が続く場合でも利用者に動作中であることを知らせる。
                _progressReporter.ReportProgress(0.0);

                // IgnoredExtensions を大文字小文字を無視して評価するために HashSet を用意
                var ignoredExtensions = new HashSet<string>(_config.IgnoredExtensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

                FileDiffResultLists.OldFilesAbsolutePath = EnumerateIncludedFiles(_oldFolderAbsolutePath, ignoredExtensions, FileDiffResultLists.IgnoredFileLocation.Old);
                _progressReporter.ReportProgress(0.0);

                FileDiffResultLists.NewFilesAbsolutePath = EnumerateIncludedFiles(_newFolderAbsolutePath, ignoredExtensions, FileDiffResultLists.IgnoredFileLocation.New);
                _progressReporter.ReportProgress(0.0);

                // OldFilesの相対パス群とNewFilesの相対パス群の和（重複除外）を取得し、個数0なら処理を終了。
                var totalFilesRelativePathCount = 0;
                {
                    var oldFilesRelativePathHashSet = new HashSet<string>(
                        FileDiffResultLists.OldFilesAbsolutePath.Select(fileAbsolutePath => Path.GetRelativePath(_oldFolderAbsolutePath, fileAbsolutePath)),
                        StringComparer.OrdinalIgnoreCase);
                    var newFilesRelativePathHashSet = new HashSet<string>(
                        FileDiffResultLists.NewFilesAbsolutePath.Select(fileAbsolutePath => Path.GetRelativePath(_newFolderAbsolutePath, fileAbsolutePath)),
                        StringComparer.OrdinalIgnoreCase);

                    var unionOldAndNewFilesRelativePathHashSet = new HashSet<string>(oldFilesRelativePathHashSet, StringComparer.OrdinalIgnoreCase);
                    foreach (var fileRelativePath in newFilesRelativePathHashSet)
                    {
                        unionOldAndNewFilesRelativePathHashSet.Add(fileRelativePath);
                    }
                    totalFilesRelativePathCount = unionOldAndNewFilesRelativePathHashSet.Count;

                    if (totalFilesRelativePathCount == 0)
                    {
                        _progressReporter.ReportProgress(100);
                        return;
                    }
                }
                _progressReporter.ReportProgress(0.0);

                // 並列度を決定
                int maxParallel;
                if (_config.MaxParallelism <= 0)
                {
                    // 既定の並列度を、ローカルはCPU論理コア数、ネットワーク共有最適化時は上限8に抑制
                    maxParallel = _optimizeForNetworkShares
                        ? Math.Min(Environment.ProcessorCount, 8)
                        : Environment.ProcessorCount;
                }
                else
                {
                    maxParallel = _config.MaxParallelism;
                }
                LoggerService.LogMessage($"[INFO] Parallel diff processing: maxParallel={maxParallel} (configured={_config.MaxParallelism}, OptimizeForNetworkShares={_optimizeForNetworkShares}, logical processors={Environment.ProcessorCount})", shouldOutputMessageToConsole: true);
                _progressReporter.ReportProgress(0.0);

                // 事前情報を詳細に出力（大量ファイル時の無音区間対策）
                {
                    int oldCount = FileDiffResultLists.OldFilesAbsolutePath.Count;
                    int newCount = FileDiffResultLists.NewFilesAbsolutePath.Count;
                    LoggerService.LogMessage($"[INFO] Discovery complete: old={oldCount}, new={newCount}, union(relative)={totalFilesRelativePathCount}", shouldOutputMessageToConsole: true);

                    // .NET アセンブリ候補数も概算表示
                    var allFilesForLog = FileDiffResultLists.OldFilesAbsolutePath
                        .Concat(FileDiffResultLists.NewFilesAbsolutePath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    int dotNetAssemblyCandidates = 0;
                    try
                    {
                        dotNetAssemblyCandidates = allFilesForLog.Count(Utility.IsDotNetExecutable);
                    }
                    catch
                    {
                        // ignore
                    }
                    LoggerService.LogMessage($"[INFO] Precompute targets: totalFiles={allFilesForLog.Count}, dotnetAssemblyCandidates={dotNetAssemblyCandidates}", shouldOutputMessageToConsole: true);
                }
                _progressReporter.ReportProgress(0.0);

                // 新旧全ファイルに対してIL キャッシュ用の事前計算を実行
                await PrecomputeIlCachesAsync(maxParallel);
                _progressReporter.ReportProgress(0.0);

                //IL出力先フォルダの作成
                Utility.ValidateAbsolutePathLengthOrThrow(_ilOutputFolderAbsolutePath);
                Directory.CreateDirectory(_ilOutputFolderAbsolutePath);
                if (_config.ShouldOutputILText)
                {
                    Utility.ValidateAbsolutePathLengthOrThrow(_ilOldFolderAbsolutePath);
                    Utility.ValidateAbsolutePathLengthOrThrow(_ilNewFolderAbsolutePath);
                    Directory.CreateDirectory(_ilOldFolderAbsolutePath);
                    Directory.CreateDirectory(_ilNewFolderAbsolutePath);
                    LoggerService.LogMessage($"[INFO] Prepared IL output directories: old='{_ilOldFolderAbsolutePath}', new='{_ilNewFolderAbsolutePath}'", shouldOutputMessageToConsole: true);
                }
                int processedFileCount = 0;
                // new 側の全ファイル絶対パスを入れた集合（大文字小文字無視）。
                // old 側を走査しながら一致したパスを削除していき、最後に残ったものを Added 判定に利用する。
                var remainingNewFilesAbsolutePathHashSet = new HashSet<string>(FileDiffResultLists.NewFilesAbsolutePath, StringComparer.OrdinalIgnoreCase);
                if (maxParallel <= 1)
                {
                    processedFileCount = await DetermineDiffsSequentiallyAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount, processedFileCount);
                }
                else
                {
                    processedFileCount = await DetermineDiffsInParallelAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount, processedFileCount, maxParallel);
                }

                // 残ったnew側ファイルが「追加」
                foreach (var newFileAbsolutePath in remainingNewFilesAbsolutePathHashSet)
                {
                    // 追加されたファイル
                    FileDiffResultLists.AddedFilesAbsolutePath.Add(newFileAbsolutePath);
                    processedFileCount++;
                    _progressReporter.ReportProgress((double)processedFileCount * 100.0 / totalFilesRelativePathCount);
                }
            }
            catch (Exception)
            {
                LoggerService.LogMessage($"[ERROR] An error occurred while diffing '{_oldFolderAbsolutePath}' and '{_newFolderAbsolutePath}'.", shouldOutputMessageToConsole: true);
                throw;
            }
        }

        /// <summary>
        /// 新旧すべてのファイルを対象に、IL キャッシュ用の事前計算を実行します。
        /// MD5 計算や内部キー生成、.NET アセンブリに対する逆アセンブラ結果キャッシュのウォームアップを行います。
        /// </summary>
        /// <param name="maxParallel">最大並列度</param>
        private async Task PrecomputeIlCachesAsync(int maxParallel)
        {
            if (_optimizeForNetworkShares)
            {
                LoggerService.LogMessage("[INFO] Network-optimized mode: skip IL precompute to reduce network I/O.", shouldOutputMessageToConsole: true);
                _progressReporter.ReportProgress(0.0);
                return;
            }
            var allFilesAbsolutePath = FileDiffResultLists
                .OldFilesAbsolutePath
                .Concat(FileDiffResultLists.NewFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            using var keepAliveCts = new CancellationTokenSource();
            var keepAliveTask = Task.Run(async () =>
            {
                try
                {
                    while (!keepAliveCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), keepAliveCts.Token);
                        _progressReporter.ReportProgress(0.0);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected when the keep-alive loop is stopped
                }
            });

            try
            {
                try
                {
                    await _fileDiffService.PrecomputeAsync(allFilesAbsolutePath, maxParallel);
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage($"[WARNING] Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
            finally
            {
                keepAliveCts.Cancel();
                try
                {
                    await keepAliveTask;
                }
                catch (OperationCanceledException)
                {
                    // ignore cancellation
                }
                _progressReporter.ReportProgress(0.0);
            }
        }

        /// <summary>
        /// 逐次（単一スレッド）で差分判定を行います。
        /// old 側を 1 件ずつ処理し、Unchanged / Modified / Removed を分類して進捗を更新します。
        /// </summary>
        /// <param name="remainingNewFilesAbsolutePathHashSet">未処理の new 側ファイル集合（相対パス一致時に削除されます）</param>
        /// <param name="totalFilesRelativePathCount">進捗計算の母数</param>
        /// <param name="processedFileCountSoFar">これまでの処理件数</param>
        /// <returns>処理済件数</returns>
        private async Task<int> DetermineDiffsSequentiallyAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar)
        {
            foreach (var oldFileAbsolutePath in FileDiffResultLists.OldFilesAbsolutePath)
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);

                if (remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath))
                {
                    remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    if (await _fileDiffService.FilesAreEqualAsync(fileRelativePath))
                    {
                        // - Unchanged -
                        FileDiffResultLists.UnchangedFilesRelativePath.Add(fileRelativePath);
                    }
                    else
                    {
                        // - Modified -
                        FileDiffResultLists.ModifiedFilesRelativePath.Add(fileRelativePath);
                    }
                }
                else
                {
                    // - Removed -
                    FileDiffResultLists.RemovedFilesAbsolutePath.Add(oldFileAbsolutePath);
                }
                processedFileCountSoFar++;
                _progressReporter.ReportProgress((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount);
            }
            return processedFileCountSoFar;
        }

        /// <summary>
        /// 並列に差分判定を行います。共有コレクションの更新は低粒度ロックで保護し、
        /// 個別の比較処理はロック外で行ってスループットを確保します。
        /// </summary>
        /// <param name="remainingNewFilesAbsolutePathHashSet">未処理の new 側ファイル集合（相対パス一致時に削除されます）</param>
        /// <param name="totalFilesRelativePathCount">進捗計算の母数</param>
        /// <param name="processedFileCountSoFar">これまでの処理件数</param>
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>処理済件数</returns>
        private async Task<int> DetermineDiffsInParallelAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar, int maxParallel)
        {
            // lockRemaining: new 側の未処理集合（remainingNewFilesAbsolutePathHashSet）へのアクセスを直列化するためのロック。
            //   - 役割: 「存在確認（Contains）→ 削除（Remove）」をアトミックに行い、
            //           同じ相対パスの二重比較（重複処理）とレースコンディションによる不整合を防ぐ。
            //   - 粒度: メンバーシップ確認と削除の最小限の区間だけロックし、計算の重い処理はロック外で行う。
            var lockRemaining = new object();
            // lockCollections: 分類結果コレクション（Unchanged/Modified/Removed）への追加操作を直列化するためのロック。
            //   - 役割: 共有コレクション（List/Dictionary など）更新時の整合性を保つ。
            //   - 分離理由: lockRemaining と分けることで、未処理集合の保護と結果集計の保護を独立させ、
            //               競合範囲を縮小し、全体のスループットを高める。
            var lockCollections = new object();

            // 処理済件数をインクリメントするための変数
            int processedFileCount = processedFileCountSoFar;

            await Parallel.ForEachAsync(FileDiffResultLists.OldFilesAbsolutePath, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (oldFileAbsolutePath, cancellationToken) =>
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
                bool hasMatchingFileInNewFilesAbsolutePathHashSet;
                // 未処理集合の存在確認と削除は「確認→削除」を不可分にするため同一ロックで保護
                lock (lockRemaining)
                {
                    // new 側に同じ相対パスがあるかチェックし、あれば集合から除去（重複比較を防止）
                    hasMatchingFileInNewFilesAbsolutePathHashSet = remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath);
                    if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                    {
                        remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    }
                }
                if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                {
                    // ロック外で比較（I/O / 計算を含むため）してから結果のみをロック下で分類
                    bool areFilesEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath, maxParallel);
                    // 分類コレクション（Unchanged/Modified）への追加は別ロックで直列化
                    lock (lockCollections)
                    {
                        if (areFilesEqual)
                        {
                            // - Unchanged -
                            FileDiffResultLists.UnchangedFilesRelativePath.Add(fileRelativePath);
                        }
                        else
                        {
                            // - Modified -
                            FileDiffResultLists.ModifiedFilesRelativePath.Add(fileRelativePath);
                        }
                    }
                }
                else
                {
                    // new 側に無いので Removed 判定
                    // 分類コレクション（Removed）への追加は別ロックで直列化
                    lock (lockCollections)
                    {
                        // - Removed -
                        FileDiffResultLists.RemovedFilesAbsolutePath.Add(oldFileAbsolutePath);
                    }
                }
                // 処理済件数をインクリメントし進捗を報告
                var done = Interlocked.Increment(ref processedFileCount);
                _progressReporter.ReportProgress((double)done * 100.0 / totalFilesRelativePathCount);
            });

            return processedFileCount;
        }

        /// <summary>
        /// IgnoredExtensions を適用して比較対象へ含めるファイル一覧を取得します。必要に応じて無視対象のレポート用情報も記録します。
        /// </summary>
        private List<string> EnumerateIncludedFiles(string rootFolderAbsolutePath, HashSet<string> ignoredExtensions, FileDiffResultLists.IgnoredFileLocation locationFlag)
        {
            var includedFiles = new List<string>();
            foreach (var fileAbsolutePath in Directory.GetFiles(rootFolderAbsolutePath, "*", SearchOption.AllDirectories))
            {
                if (ignoredExtensions.Contains(Path.GetExtension(fileAbsolutePath)))
                {
                    if (_config.ShouldIncludeIgnoredFiles)
                    {
                        var relativePath = Path.GetRelativePath(rootFolderAbsolutePath, fileAbsolutePath);
                        FileDiffResultLists.RecordIgnoredFile(relativePath, locationFlag);
                    }
                    continue;
                }
                includedFiles.Add(fileAbsolutePath);
            }
            return includedFiles;
        }
    }
}
