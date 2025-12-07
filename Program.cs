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
                Console.WriteLine(Constants.INFO_INITIALIZING_LOGGER);
                LoggerService.Initialize();
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_LOGGER_INITIALIZED, shouldOutputMessageToConsole: true);
                #endregion

                // アプリケーションのバージョンを取得
                _thisAppVersion = Utility.GetAppVersion(typeof(Program));
                LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(Constants.LOG_APPLICATION_VERSION, _thisAppVersion), shouldOutputMessageToConsole: true);

                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_VALIDATING_ARGS, shouldOutputMessageToConsole: true);

                #region コマンドライン引数の過不足およびnull, 空文字, 空白文字チェック
                try
                {
                    if (args == null || args.Length < 3)
                    {
                        throw new ArgumentException(Constants.ERROR_INSUFFICIENT_ARGUMENTS);
                    }
                    if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                    {
                        throw new ArgumentException(Constants.ERROR_ARGUMENTS_NULL_OR_EMPTY);
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException(Constants.ERROR_INVALID_ARGUMENTS_USAGE, ex);
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
                        LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(Constants.ERROR_INVALID_REPORT_LABEL, reportLabel), shouldOutputMessageToConsole: true);
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
                    throw new DirectoryNotFoundException(string.Format(Constants.ERROR_OLD_FOLDER_NOT_FOUND, _oldFolderAbsolutePath));
                }
                if (!Directory.Exists(_newFolderAbsolutePath))
                {
                    throw new DirectoryNotFoundException(string.Format(Constants.ERROR_NEW_FOLDER_NOT_FOUND, _newFolderAbsolutePath));
                }
                if (Directory.Exists(_reportsFolderAbsolutePath))
                {
                    throw new ArgumentException(string.Format(Constants.ERROR_REPORT_FOLDER_EXISTS, _reportsFolderAbsolutePath));
                }
                #endregion

                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_ARGS_VALIDATION_COMPLETED, shouldOutputMessageToConsole: true);

                // 今回作成するレポートの出力先の新規作成
                Directory.CreateDirectory(_reportsFolderAbsolutePath);

                #region アプリケーション設定の読み込み
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
                _config = await new ConfigService().LoadConfigAsync();
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);
                #endregion

                // 古いログファイルの削除（失敗しても警告出力のみで処理継続）
                LoggerService.CleanupOldLogFiles(_config.MaxLogGenerations);

                // タイムスタンプキャッシュを初期化
                Services.Caching.TimestampCache.Clear();

                // 処理開始宣言
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_APP_STARTING, shouldOutputMessageToConsole: true);

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
                            LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(Constants.LOG_ELAPSED_TIME, _elapsedTimeString), shouldOutputMessageToConsole: true);
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
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_APP_FINISHED, shouldOutputMessageToConsole: true);
            }
            catch (Exception ex)
            {
                // 例外を補足した場合は、stacktraceをログに出力して終了
                LoggerService.LogMessage(LoggerService.LogLevel.Error, ex.Message, shouldOutputMessageToConsole: true, ex);
                LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(Constants.LOG_ERROR_DETAILS_PATH, LoggerService._logFileAbsolutePath), shouldOutputMessageToConsole: true);
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
                        Console.WriteLine(Constants.INFO_PRESS_ANY_KEY);
                        Console.ReadKey(true);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage(LoggerService.LogLevel.Error, Constants.ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
                    }

                }
            }
        }
    }
}
