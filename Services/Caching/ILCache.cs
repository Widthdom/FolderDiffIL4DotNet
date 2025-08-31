using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// IL 逆アセンブル結果をメモリ + 任意のディスクにキャッシュするクラス。
    /// キー: ファイル内容の MD5 + 使用ツールラベル
    /// </summary>
    /// <remarks>
    /// このキャッシュは、同じバイナリを何度も逆アセンブルしないための仕組みです。
    /// - キーは「ファイル内容のMD5」+「使用した逆アセンブラとそのバージョン（toolLabel）」の組み合わせです。
    /// - メモリ上のキャッシュ（高速）と、任意でディスク上のキャッシュ（プロセスをまたいで有効）を併用します。
    /// - TTL（有効期限）と LRU（最近使われていないものから削除）でメモリ使用量を制御します。
    /// - ディスク上ではファイル名に使用できない文字をサニタイズし、必要に応じて短縮します。
    ///
    /// 典型的な使い方（擬似コード）:
    /// 1) PrecomputeAsync で対象ファイル群の MD5 を先読み（任意）
    /// 2) TryGetAsync で IL テキストのキャッシュを検索
    ///    - 見つかればそれを利用
    ///    - 見つからなければ逆アセンブルを実行
    /// 3) 逆アセンブル結果を SetAsync でキャッシュへ保存
    ///
    /// スレッド安全性:
    /// - 複数スレッドから同時に呼び出される前提で、ConcurrentDictionary とロックを併用して安全に動作します。
    /// </remarks>
    public sealed class ILCache
    {
        #region constants
        /// <summary>
        /// キャッシュキーの区切り
        /// </summary>
        private const string KEY_SEPARATOR = "_";
        #endregion

        #region private read-only variables
        /// <summary>
        /// メモリ上の IL キャッシュ本体。
        /// Key ：ファイル内容MD5 + ツールラベル)
        /// Value ：IL文字列, 最終アクセス日時（協定世界時）, 生成日時（協定世界時）
        /// </summary>
        private readonly ConcurrentDictionary<string, (string ILText, DateTime LastAccessUtc, DateTime CreatedUtc)> _memoryILCacheDictionary = new(StringComparer.Ordinal);

        /// <summary>
        /// ファイルパスに対するMD5 の計算結果をキャッシュする辞書
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _md5HashCacheDictionary = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// ディスクキャッシュ用ルートディレクトリ（未指定 or 作成失敗時 null 同等）
        /// </summary>
        private readonly string _ilCacheDirectoryAbsolutePath;

        /// <summary>
        /// ディスクキャッシュが有効かどうか
        /// </summary>
        private readonly bool _isDiskCacheEnabled;

        /// <summary>
        /// メモリキャッシュの最大保持エントリ数（超過時に古いものを削除）
        /// </summary>
        private readonly int _ilCacheMaxMemoryEntries;

        /// <summary>
        /// 各エントリの有効期間 (Time To Live)。null の場合は無期限。
        /// </summary>
        private readonly TimeSpan? _timeToLive;

        /// <summary>
        /// LRU（Least Recently Used）方式での削除処理の同期用ロック
        /// </summary>
        private readonly object _lruLock = new();

        /// <summary>
        /// ディスクキャッシュに保持できる最大ファイル数。
        /// 0 以下の場合は無制限。上限超過時は最終アクセスが古い順に削除します。
        /// </summary>
        private readonly int _ilCacheMaxDiskFileCount; // 0 = unlimited

        /// <summary>
        /// ディスクキャッシュの総サイズ上限（バイト単位）。
        /// 0 以下の場合は無制限。上限超過時はサイズが下回るまで古い順に削除します。
        /// </summary>
        private readonly long _ilCacheMaxDiskBytes;   // 0 = unlimited

        /// <summary>
        /// ディスククォータ適用処理（件数・サイズ制御）の同期用ロックオブジェクト。
        /// </summary>
        private readonly object _diskQuotaLock = new();
        #endregion

        #region private member variables
        /// <summary>
        /// LRU（Least Recently Used）方式で削除(退避)された累計件数
        /// </summary>
        private long _evictedCount = 0;

        /// <summary>
        /// TTL 失効により削除された累計件数
        /// </summary>
        private long _expiredCount = 0;

        /// <summary>
        /// 統計ログのためのアクセスヒットカウンタ（FolderDiffService 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalHits = 0;

        /// <summary>
        /// 統計ログのためのアクセス保存カウンタ（FolderDiffService 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalStores = 0;

        /// <summary>
        /// 内部キャッシュ統計ログ（ヒット率など）を出力する最小間隔。
        /// コンストラクタの statsLogIntervalSeconds で上書きされ、既定は 60 秒です。
        /// </summary>
        private static TimeSpan StatsLogInterval = TimeSpan.FromSeconds(60);
        #endregion

        #region public properties
        /// <summary>
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)
        /// </summary>
        public (long Evicted, long Expired) Stats => (_evictedCount, _expiredCount);
        #endregion

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="ilCacheDirectoryAbsolutePath">ディスクキャッシュ格納ディレクトリ（null/空で無効。作成に失敗した場合も無効）</param>
        /// <param name="ilCacheMaxMemoryEntries">メモリキャッシュ最大件数（0 以下は既定 1000 を採用。超過時は LRU で削除）</param>
        /// <param name="timeToLive">各エントリのTime-To-Live（有効期間）（null で無期限。期限切れは参照時にパージ）</param>
        /// <param name="statsLogIntervalSeconds">内部統計ログ（ヒット率など）の出力間隔（秒）。0 以下で既定の 60 秒。</param>
        /// <param name="ilCacheMaxDiskFileCount">ディスクキャッシュの最大ファイル数（0 以下で無制限。超過時は古い順に削除）</param>
        /// <param name="ilCacheMaxDiskMegabytes">ディスクキャッシュのサイズ上限（MB）。0 以下で無制限。超過時は古い順に削除</param>
        /// <remarks>
        /// - directory が有効かつ作成に成功した場合のみディスクキャッシュ（_diskEnabled）が有効になります。
        /// - 統計ログの出力間隔は静的フィールド（全インスタンス共通）の <see cref="StatsLogInterval"/> に反映されます。
        /// - ディスクキャッシュの件数／サイズ制御は書き込み後に <see cref="EnforceDiskQuota"/> で適用されます。
        /// </remarks>
        public ILCache(string ilCacheDirectoryAbsolutePath, int ilCacheMaxMemoryEntries = 1000, TimeSpan? timeToLive = null, int statsLogIntervalSeconds = 60, int ilCacheMaxDiskFileCount = 0, long ilCacheMaxDiskMegabytes = 0)
        {
            // メモリキャッシュ容量（0 以下なら既定の 1000 件）
            _ilCacheMaxMemoryEntries = ilCacheMaxMemoryEntries <= 0 ? 1000 : ilCacheMaxMemoryEntries;
            // TTL（null で無期限。失効チェックは参照時に実施）
            _timeToLive = timeToLive;
            // 統計ログの出力間隔（0 以下は 60 秒に補正）
            if (statsLogIntervalSeconds <= 0)
            {
                statsLogIntervalSeconds = 60;
            }
            StatsLogInterval = TimeSpan.FromSeconds(statsLogIntervalSeconds);
    
            _ilCacheMaxDiskFileCount = ilCacheMaxDiskFileCount;
            _ilCacheMaxDiskBytes = ilCacheMaxDiskMegabytes > 0 ? ilCacheMaxDiskMegabytes * 1024L * 1024L : 0;
            // ディスクキャッシュ用ディレクトリの初期化（作成成功時のみ有効化）
            if (!string.IsNullOrWhiteSpace(ilCacheDirectoryAbsolutePath))
            {
                _ilCacheDirectoryAbsolutePath = ilCacheDirectoryAbsolutePath;
                try
                {
                    Utility.ValidateAbsolutePathLengthOrThrow(_ilCacheDirectoryAbsolutePath);
                    Directory.CreateDirectory(_ilCacheDirectoryAbsolutePath);
                    _isDiskCacheEnabled = true;
                }
                catch (Exception ex)
                {
                    _isDiskCacheEnabled = false;
                    LoggerService.LogMessage($"[WARNING] Failed to create IL cache directory '{_ilCacheDirectoryAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
        }

        /// <summary>
        /// 対象ファイル群の MD5 を並列プリウォーム（後続キャッシュキー生成の I/O レイテンシを平準化）
        /// </summary>
        /// <param name="fileAbsolutePaths">対象ファイル絶対パス群</param>
        /// <param name="maxParallel">最大並列度（呼び出し側で決定済みの値を想定）</param>
        /// <returns>Task</returns>
        /// <exception cref="ArgumentOutOfRangeException">maxParallel が 0 以下の場合にスローされます。</exception>
        public Task PrecomputeAsync(IEnumerable<string> fileAbsolutePaths, int maxParallel)
        {
            if (fileAbsolutePaths == null)
            {
                return Task.CompletedTask;
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, "The maximum degree of parallelism must be 1 or greater.");
            }

            try
            {
                // 対象ファイル群に対し、MD5 の計算だけを先に並列実行しておきます。
                // こうすることで、後続の TryGet/Set でキャッシュキー生成にかかる I/O 待ちを平準化できます。
                var files = fileAbsolutePaths as ICollection<string> ?? fileAbsolutePaths.ToList();
                if (files.Count == 0)
                {
                    return Task.CompletedTask;
                }
                LoggerService.LogMessage($"[INFO] Precompute MD5: starting for {files.Count} files (maxParallel={maxParallel})", shouldOutputMessageToConsole: true);

                int processed = 0;
                long lastLogTicks = DateTime.UtcNow.Ticks;

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, fileAbsolutePath =>
                {
                    try
                    {
                        // 個々のファイルの MD5 を計算し、内部キャッシュに投入（GetOrAdd）
                        GetFileHash(fileAbsolutePath);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage($"[WARNING] Failed to precompute MD5 for file '{fileAbsolutePath}'. This file will be skipped in the cache.", shouldOutputMessageToConsole: true, ex);
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref processed);
                        // 2秒に1回、または500件ごとに進捗ログを出す（初回は直ちに出力）
                        var nowTicks = DateTime.UtcNow.Ticks;
                        var prev = Interlocked.Read(ref lastLogTicks);
                        bool timeElapsed = new TimeSpan(nowTicks - prev).TotalSeconds >= 2;
                        bool countStep = done % 500 == 0 || done == files.Count;
                        if (timeElapsed || countStep)
                        {
                            if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, prev) == prev)
                            {
                                int percent = (int)(done * 100.0 / files.Count);
                                LoggerService.LogMessage($"[INFO] Precompute MD5: {done}/{files.Count} ({percent}%)", shouldOutputMessageToConsole: true);
                            }
                        }
                    }
                });

                LoggerService.LogMessage($"[INFO] Precompute MD5: completed for {files.Count} files", shouldOutputMessageToConsole: true);
            }
            catch
            {
                // ignore
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// キャッシュから IL を取得
        /// まずメモリキャッシュを確認（期限も確認）し、次にディスクキャッシュを確認します。
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <returns>IL 文字列（未ヒット時 null）</returns>
        public async Task<string> TryGetILAsync(string fileAbsolutePath, string toolLabel)
        {
            // 1) キーを生成（MD5 + toolLabel）
            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            if (_memoryILCacheDictionary.TryGetValue(ilCacheKey, out var ilCache))
            {
                // 2) メモリキャッシュを最初に確認。time to live が設定されている場合は期限切れをチェック
                if (_timeToLive.HasValue && (DateTime.UtcNow - ilCache.CreatedUtc) > _timeToLive.Value)
                {
                    // 期限切れならメモリから削除
                    _memoryILCacheDictionary.TryRemove(ilCacheKey, out _);

                    Interlocked.Increment(ref _expiredCount);
                }
                else
                {
                    // 期限切れでない場合は、最終アクセス日時（協定世界時）をシステム日時で更新
                    _memoryILCacheDictionary[ilCacheKey] = (ilCache.ILText, DateTime.UtcNow, ilCache.CreatedUtc);

                    Interlocked.Increment(ref _internalHits);
                    return ilCache.ILText;
                }
            }
            if (_isDiskCacheEnabled)
            {
                // 3) ディスクキャッシュが有効ならファイルの存在を確認
                var diskILCacheFileAbsolutePath = BuildILCacheFileAbsolutePath(ilCacheKey);
                if (File.Exists(diskILCacheFileAbsolutePath))
                {
                    try
                    {
                        // ヒットしたらファイルから内容(IL)を読み込み、
                        // メモリキャッシュに登録。最終アクセス日時（協定世界時）と生成日時（協定世界時）をシステム日時とする
                        var ilText = await File.ReadAllTextAsync(diskILCacheFileAbsolutePath);
                        _memoryILCacheDictionary[ilCacheKey] = (ilText, DateTime.UtcNow, DateTime.UtcNow);

                        Interlocked.Increment(ref _internalHits);
                        return ilText;
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage($"[WARNING] Failed to read IL cache file '{diskILCacheFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    }
                }
            }
            // 4) どこにも無ければ呼び出し元で逆アセンブル実行し、成功後 SetAsync で登録
            return null;
        }

        /// <summary>
        /// キャッシュへ IL を保存
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <param name="ilText">IL 文字列</param>
        /// <returns>Task</returns>
        public async Task SetILAsync(string fileAbsolutePath, string toolLabel, string ilText)
        {
            if (string.IsNullOrEmpty(ilText))
            {
                return;
            }
            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            // 挿入前に容量超過なら最古アクセスを削除
            if (_memoryILCacheDictionary.Count >= _ilCacheMaxMemoryEntries)
            {
                lock (_lruLock)
                {
                    if (_memoryILCacheDictionary.Count >= _ilCacheMaxMemoryEntries)
                    {
                        // メモリ上のエントリを全走査して、最終アクセスが最も古いものを 1 件見つける（O(n)）
                        string oldestILCacheKey = null;
                        DateTime oldestLastAccessUtc = DateTime.MaxValue;
                        foreach (var keyAndValue in _memoryILCacheDictionary)
                        {
                            if (keyAndValue.Value.LastAccessUtc < oldestLastAccessUtc)
                            {
                                oldestLastAccessUtc = keyAndValue.Value.LastAccessUtc;
                                oldestILCacheKey = keyAndValue.Key;
                            }
                        }
                        if (oldestILCacheKey != null && _memoryILCacheDictionary.TryRemove(oldestILCacheKey, out _))
                        {
                            // LRU（Least Recently Used）方式でメモリから退避。統計カウントを加算
                            Interlocked.Increment(ref _evictedCount);
                            try
                            {
                                if (_isDiskCacheEnabled)
                                {
                                    // 可能ならディスク側の対応ファイルも削除（容量抑制）
                                    var diskILCacheFileToRemoveAbsolutePath = BuildILCacheFileAbsolutePath(oldestILCacheKey);
                                    if (File.Exists(diskILCacheFileToRemoveAbsolutePath))
                                    {
                                        File.Delete(diskILCacheFileToRemoveAbsolutePath);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogMessage($"[WARNING] Failed to remove disk cache file '{BuildILCacheFileAbsolutePath(oldestILCacheKey)}' during LRU eviction.", shouldOutputMessageToConsole: true, ex);
                            }
                        }
                    }
                }
            }
            // メモリへ格納（最終アクセス日時（協定世界時）と生成日時（協定世界時）をシステム日時とする）
            _memoryILCacheDictionary[ilCacheKey] = (ilText, DateTime.UtcNow, DateTime.UtcNow);
            if (_isDiskCacheEnabled)
            {
                var diskILCacheFileToWriteAbsolutePath = BuildILCacheFileAbsolutePath(ilCacheKey);
                try
                {
                    // ディスクへも書き込み（プロセスをまたいだ再利用を可能に）
                    await File.WriteAllTextAsync(diskILCacheFileToWriteAbsolutePath, ilText);
                    // 書き込み後にディスククォータ（件数・サイズ）を適用
                    EnforceDiskQuota();
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage($"[WARNING] Failed to write IL cache file '{diskILCacheFileToWriteAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
            // ストア統計を加算
            Interlocked.Increment(ref _internalStores);
        }

        #region private methods
        /// <summary>
        /// 指定ファイルの MD5 を取得（内部キャッシュ使用）。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイル絶対パス</param>
        /// <returns>MD5 (hex lowercase)</returns>
        private string GetFileHash(string fileAbsolutePath)
        {
            // 同じパスに対しては MD5 を再計算しないよう、ConcurrentDictionary でキャッシュします。
            // 注意: ファイル内容が後から変更された場合でも、ここでは自動で無効化されません。
            //       呼び出し側では通常「比較対象のスナップショット」を処理する想定です。
            return _md5HashCacheDictionary.GetOrAdd(fileAbsolutePath, static path => Utility.ComputeFileMd5Hex(path));
        }

        /// <summary>
        /// キャッシュのキー文字列を生成。
        /// MD5 + <see cref="KEY_SEPARATOR"/> + toolLabel
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル（コマンド+バージョン）</param>
        /// <returns>キー</returns>
        // toolLabel にはコマンド名とバージョンが含まれます。
        // 後段のディスク保存では、このキーをサニタイズして安全なファイル名に変換します。
        private string BuildILCacheKey(string fileAbsolutePath, string toolLabel) => GetFileHash(fileAbsolutePath) + KEY_SEPARATOR + toolLabel;

        /// <summary>
        /// ディスクキャッシュファイルのパスを生成。
        /// </summary>
        /// <param name="ilCacheKey">キャッシュキー</param>
        /// <returns>パス（無効時は null）</returns>
        private string BuildILCacheFileAbsolutePath(string ilCacheKey)
        {
            if (_ilCacheDirectoryAbsolutePath == null)
            {
                return null;
            }
            // キャッシュキーをそのままファイル名に使用できない可能性が高いため、変換（サニタイズあるいは短縮）したうえで、拡張子を付けて返却します。
            return Path.Combine(_ilCacheDirectoryAbsolutePath, Utility.ToSafeFileName(ilCacheKey) + Constants.IL_CACHE_EXTENSION);
        }

        /// <summary>
        /// ディスクキャッシュ容量制御（サイズと件数）。SetAsync で新規書き込み後に実行。
        /// </summary>
        private void EnforceDiskQuota()
        {
            if (!_isDiskCacheEnabled)
            {
                return;
            }
            if (_ilCacheMaxDiskFileCount <= 0 && _ilCacheMaxDiskBytes <= 0)
            {
                return;
            }
            lock (_diskQuotaLock)
            {
                var ilCacheDirectoryInfo = new DirectoryInfo(_ilCacheDirectoryAbsolutePath);
                if (!ilCacheDirectoryInfo.Exists)
                {
                    return;
                }
                // ディレクトリ内のキャッシュファイルを最終更新時刻が古い順に並べ替えて列挙
                var ilCacheFiles = ilCacheDirectoryInfo.GetFiles($"*{Constants.IL_CACHE_EXTENSION}", SearchOption.TopDirectoryOnly)
                    .Select(f => new { File = f, LastAccess = f.LastWriteTimeUtc })
                    .OrderBy(f => f.LastAccess)
                    .ToList();
                long ilCacheFilesTotalBytes = ilCacheFiles.Sum(f => f.File.Length);
                int ilCacheFilesCount = ilCacheFiles.Count;
                bool changed = false;
                int removed = 0;
                foreach (var ilCacheFile in ilCacheFiles)
                {
                    // 件数または総容量の上限を超えている間は、古いものから削除
                    if ((_ilCacheMaxDiskFileCount > 0 && ilCacheFilesCount > _ilCacheMaxDiskFileCount) || (_ilCacheMaxDiskBytes > 0 && ilCacheFilesTotalBytes > _ilCacheMaxDiskBytes))
                    {
                        try
                        {
                            long ilCacheFileBytes = ilCacheFile.File.Length;
                            ilCacheFile.File.Delete();
                            ilCacheFilesTotalBytes -= ilCacheFileBytes;
                            ilCacheFilesCount--;
                            removed++;
                            changed = true;
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogMessage($"[WARNING] Failed to delete cache file: {ilCacheFile.File.FullName}", true, ex);
                        }
                    }
                    else
                    {
                        break; // 条件を満たさなくなったら終了
                    }
                }
                if (changed && removed > 0)
                {
                    // どれだけ削除され、残数・残容量がいくつかをコンソール・ログ出力
                    LoggerService.LogMessage($"[INFO] Disk quota trim: removed={removed}, remain={ilCacheFilesCount}, bytes={ilCacheFilesTotalBytes}", true);
                }
            }
        }
        #endregion
    }
}
