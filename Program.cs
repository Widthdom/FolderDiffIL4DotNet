using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet
{
    /// <summary>
    /// アプリケーションのエントリーポイント
    /// フォルダ間の差分を比較し、結果をレポートとして出力します。
    /// </summary>
    class Program
    {
        #region private member variables
        /// <summary>
        /// 旧バージョン側（比較元）フォルダの絶対パス
        /// </summary>
        private static string _oldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）フォルダの絶対パス
        /// </summary>
        private static string _newFolderAbsolutePath;

        /// <summary>
        /// レポート出力先の絶対パス
        /// </summary>
        private static string _reportsFolderAbsolutePath;

        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private static ConfigSettings _config;

        /// <summary>
        /// このアプリケーションのバージョン
        /// </summary>
        private static string _thisAppVersion;
        
        /// <summary>
        /// 処理時間（文字列）
        /// </summary>
        private static string _elapsedTimeString;
        #endregion

        /// <summary>
        /// アプリケーションのエントリーポイント
        /// </summary>
        static async Task Main(string[] args)
        {
            try
            {
                #region Loggerの初期化（以降Loggerを使ったログ出力が可能）
                Console.WriteLine("[INFO] Initializing logger...");
                LoggerService.Initialize();
                LoggerService.LogMessage("[INFO] Logger initialized.", shouldOutputMessageToConsole: true);
                #endregion

                // アプリケーションのバージョンを取得
                _thisAppVersion = Utility.GetAppVersion(typeof(Program));
                LoggerService.LogMessage("[INFO] Application version: " + _thisAppVersion, shouldOutputMessageToConsole: true);

                LoggerService.LogMessage("[INFO] Validating command line arguments...", shouldOutputMessageToConsole: true);

                #region コマンドライン引数の過不足およびnull, 空文字, 空白文字チェック
                try
                {
                    if (args == null || args.Length < 3)
                    {
                        throw new ArgumentException("Insufficient arguments.");
                    }
                    if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                    {
                        throw new ArgumentException("One or more required arguments are null or empty.");
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("Invalid arguments. Usage: FolderDiffIL4DotNet <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [--no-pause]", ex);
                }
                #endregion

                #region コマンドライン引数のメンバ変数への展開
                _oldFolderAbsolutePath = args[0];
                _newFolderAbsolutePath = args[1];
                {
                    string reportLabel = args[2];
                    // コマンドライン第3引数「reportLabel」の値がフォルダ名として正しいか検証
                    try
                    {
                        Utility.ValidateFolderNameOrThrow(reportLabel, nameof(reportLabel));
                    }
                    catch (ArgumentException)
                    {
                        LoggerService.LogMessage($"[ERROR] The value '{reportLabel}', provided as the third argument (reportLabel), is invalid as a folder name.", shouldOutputMessageToConsole: true);
                        throw;
                    }
                    // レポート出力先の準備
                    {
                        string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, Constants.REPORTS_ROOT_DIR_NAME);
                        Directory.CreateDirectory(reportsRootDirAbsolutePath);
                        _reportsFolderAbsolutePath = Path.Combine(reportsRootDirAbsolutePath, reportLabel);
                    }
                }
                #endregion

                #region コマンドライン引数に指定されたフォルダの存在確認
                if (!Directory.Exists(_oldFolderAbsolutePath))
                {
                    throw new DirectoryNotFoundException($"The old folder path does not exist: {_oldFolderAbsolutePath}");
                }
                if (!Directory.Exists(_newFolderAbsolutePath))
                {
                    throw new DirectoryNotFoundException($"The new folder path does not exist: {_newFolderAbsolutePath}");
                }
                if (Directory.Exists(_reportsFolderAbsolutePath))
                {
                    throw new ArgumentException($"The report folder already exists: {_reportsFolderAbsolutePath}. Provide a different report label.");
                }
                #endregion

                LoggerService.LogMessage("[INFO] Command line arguments validation completed.", shouldOutputMessageToConsole: true);

                // 今回作成するレポートの出力先の新規作成
                Directory.CreateDirectory(_reportsFolderAbsolutePath);

                #region アプリケーション設定の読み込み
                LoggerService.LogMessage("[INFO] Loading configuration...", shouldOutputMessageToConsole: true);
                _config = await new ConfigService().LoadConfigAsync();
                LoggerService.LogMessage("[INFO] Configuration loaded successfully.", shouldOutputMessageToConsole: true);
                #endregion

                // 古いログファイルの削除（失敗しても警告出力のみで処理継続）
                LoggerService.CleanupOldLogFiles(_config.MaxLogGenerations);

                // タイムスタンプキャッシュを初期化
                Services.Caching.TimestampCache.Clear();

                // 処理開始宣言
                LoggerService.LogMessage("[INFO] Starting FolderDiffIL4DotNet...", shouldOutputMessageToConsole: true);

                #region フォルダ差分比較処理の実行
                {
                    // 処理時間計測開始
                    var stopwatch = Stopwatch.StartNew();
                    // 比較処理
                    await new FolderDiffService(_config, new ProgressReportService(), _oldFolderAbsolutePath, _newFolderAbsolutePath, _reportsFolderAbsolutePath).ExecuteFolderDiffAsync();
                    // 処理時間計測終了
                    stopwatch.Stop();
                    // フォルダ差分比較処理時間のコンソール出力
                    {
                        TimeSpan? lastRunDuration = stopwatch.Elapsed;

                        if (lastRunDuration.HasValue)
                        {
                            string hourString = $"{(int)Math.Floor(lastRunDuration.Value.TotalHours):00}";
                            string minuteString = $"{lastRunDuration.Value.Minutes:00}";
                            string secondString = $"{lastRunDuration.Value.Seconds:00}";
                            string millisecondString = $"{lastRunDuration.Value.Milliseconds:000}";
                            _elapsedTimeString = $"{hourString}:{minuteString}:{secondString}.{millisecondString}";
                            LoggerService.LogMessage($"[INFO] Elapsed Time: {_elapsedTimeString}", shouldOutputMessageToConsole: true);
                        }
                    }
                }
                #endregion

                // 差分結果の集計レポートを生成
                new ReportGenerateService().GenerateDiffReport(
                    _oldFolderAbsolutePath,
                    _newFolderAbsolutePath,
                    _reportsFolderAbsolutePath,
                    _thisAppVersion,
                    _elapsedTimeString,
                    _config);

                // 正常終了メッセージ出力
                LoggerService.LogMessage("[INFO] FolderDiffIL4DotNet finished without errors. See Reports folder for details.", shouldOutputMessageToConsole: true);
            }
            catch (Exception ex)
            {
                // 例外を補足した場合は、stacktraceをログに出力して終了
                LoggerService.LogMessage($"[ERROR] {ex.Message}", shouldOutputMessageToConsole: true, ex);
                LoggerService.LogMessage($"[INFO] Error details logged to: {LoggerService._logFileAbsolutePath}", shouldOutputMessageToConsole: true);
            }
            finally
            {
                /// <see cref="Constants.NO_PAUSE">オプション指定でアプリケーションが起動された場合、または
                // 非対話（リダイレクトされている）の場合は、終了時にキー入力待ちを行わない
                if (args.Any(a => string.Equals(a, Constants.NO_PAUSE, StringComparison.OrdinalIgnoreCase))
                    || Console.IsInputRedirected
                    || Console.IsOutputRedirected
                    || Console.IsErrorRedirected)
                {
                    //do nothing
                }
                else
                {
                    try
                    {
                        Console.WriteLine("[INFO] Press any key to exit...");
                        Console.ReadKey(true);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage($"[ERROR] An error occurred during key prompt.", shouldOutputMessageToConsole: false, ex);
                    }

                }
            }
        }
    }
}
