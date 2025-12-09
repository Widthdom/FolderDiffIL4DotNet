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
        #region constants
        /// <summary>
        /// レポートを出力するルートディレクトリ名
        /// </summary>
        private const string REPORTS_ROOT_DIR_NAME = "Reports";

        /// <summary>
        /// ロガー初期化メッセージ
        /// </summary>
        private const string INITIALIZING_LOGGER = "Initializing logger...";

        /// <summary>
        /// Logger initialized
        /// </summary>
        private const string LOGGER_INITIALIZED = "Logger initialized.";

        /// <summary>
        /// アプリバージョン
        /// </summary>
        private const string APPLICATION_VERSION = "Application version: {0}";

        /// <summary>
        /// 引数検証開始
        /// </summary>
        private const string VALIDATING_ARGS = "Validating command line arguments...";

        /// <summary>
        /// 引数不足
        /// </summary>
        private const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";

        /// <summary>
        /// 引数null/空
        /// </summary>
        private const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";

        /// <summary>
        /// コマンドラインの使用例
        /// </summary>
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + $" <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [{NO_PAUSE}]";

        /// <summary>
        /// reportLabelエラー
        /// </summary>
        private const string ERROR_INVALID_REPORT_LABEL = "The value '{0}', provided as the third argument (reportLabel), is invalid as a folder name.";

        /// <summary>
        /// 旧フォルダ存在せず
        /// </summary>
        private const string ERROR_OLD_FOLDER_NOT_FOUND = "The old folder path does not exist: {0}";

        /// <summary>
        /// 新フォルダ存在せず
        /// </summary>
        private const string ERROR_NEW_FOLDER_NOT_FOUND = "The new folder path does not exist: {0}";

        /// <summary>
        /// レポートフォルダ既存
        /// </summary>
        private const string ERROR_REPORT_FOLDER_EXISTS = "The report folder already exists: {0}. Provide a different report label.";

        /// <summary>
        /// 引数検証完了
        /// </summary>
        private const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";

        /// <summary>
        /// 設定読み込み開始
        /// </summary>
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";

        /// <summary>
        /// 設定読み込み完了
        /// </summary>
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";

        /// <summary>
        /// アプリ開始ログ
        /// </summary>
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";

        /// <summary>
        /// アプリ正常終了
        /// </summary>
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";

        /// <summary>
        /// エラーログパス
        /// </summary>
        private const string LOG_ERROR_DETAILS_PATH = "Error details logged to: {0}";

        /// <summary>
        /// CI 等向けスイッチ
        /// </summary>
        private const string NO_PAUSE = "--no-pause";

        /// <summary>
        /// 終了キープロンプト
        /// </summary>
        private const string PRESS_ANY_KEY = "Press any key to exit...";

        /// <summary>
        /// キープロンプトエラー
        /// </summary>
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        #endregion

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
                Console.WriteLine(INITIALIZING_LOGGER);
                LoggerService.Initialize();
                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOGGER_INITIALIZED, shouldOutputMessageToConsole: true);
                #endregion

                // アプリケーションのバージョンを取得
                _thisAppVersion = Utility.GetAppVersion(typeof(Program));
                LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(APPLICATION_VERSION, _thisAppVersion), shouldOutputMessageToConsole: true);

                LoggerService.LogMessage(LoggerService.LogLevel.Info, VALIDATING_ARGS, shouldOutputMessageToConsole: true);

                #region コマンドライン引数の過不足およびnull, 空文字, 空白文字チェック
                try
                {
                    if (args == null || args.Length < 3)
                    {
                        throw new ArgumentException(ERROR_INSUFFICIENT_ARGUMENTS);
                    }
                    if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                    {
                        throw new ArgumentException(ERROR_ARGUMENTS_NULL_OR_EMPTY);
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException(ERROR_INVALID_ARGUMENTS_USAGE, ex);
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
                        LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(ERROR_INVALID_REPORT_LABEL, reportLabel), shouldOutputMessageToConsole: true);
                        throw;
                    }
                    // レポート出力先の準備
                    {
                        string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
                        Directory.CreateDirectory(reportsRootDirAbsolutePath);
                        _reportsFolderAbsolutePath = Path.Combine(reportsRootDirAbsolutePath, reportLabel);
                    }
                }
                #endregion

                #region コマンドライン引数に指定されたフォルダの存在確認
                if (!Directory.Exists(_oldFolderAbsolutePath))
                {
                    throw new DirectoryNotFoundException(string.Format(ERROR_OLD_FOLDER_NOT_FOUND, _oldFolderAbsolutePath));
                }
                if (!Directory.Exists(_newFolderAbsolutePath))
                {
                    throw new DirectoryNotFoundException(string.Format(ERROR_NEW_FOLDER_NOT_FOUND, _newFolderAbsolutePath));
                }
                if (Directory.Exists(_reportsFolderAbsolutePath))
                {
                    throw new ArgumentException(string.Format(ERROR_REPORT_FOLDER_EXISTS, _reportsFolderAbsolutePath));
                }
                #endregion

                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOG_ARGS_VALIDATION_COMPLETED, shouldOutputMessageToConsole: true);

                // 今回作成するレポートの出力先の新規作成
                Directory.CreateDirectory(_reportsFolderAbsolutePath);

                #region アプリケーション設定の読み込み
                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
                _config = await new ConfigService().LoadConfigAsync();
                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);
                #endregion

                // 古いログファイルの削除（失敗しても警告出力のみで処理継続）
                LoggerService.CleanupOldLogFiles(_config.MaxLogGenerations);

                // タイムスタンプキャッシュを初期化
                Services.Caching.TimestampCache.Clear();

                // 処理開始宣言
                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOG_APP_STARTING, shouldOutputMessageToConsole: true);

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
                LoggerService.LogMessage(LoggerService.LogLevel.Info, LOG_APP_FINISHED, shouldOutputMessageToConsole: true);
            }
            catch (Exception ex)
            {
                // 例外を捕捉した場合は、stacktraceをログに出力して終了
                LoggerService.LogMessage(LoggerService.LogLevel.Error, ex.Message, shouldOutputMessageToConsole: true, ex);
                LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(LOG_ERROR_DETAILS_PATH, LoggerService._logFileAbsolutePath), shouldOutputMessageToConsole: true);
            }
            finally
            {
                /// <see cref="NO_PAUSE">オプション指定でアプリケーションが起動された場合、または
                // 非対話（リダイレクトされている）の場合は、終了時にキー入力待ちを行わない
                if (args.Any(arg => string.Equals(arg, NO_PAUSE, StringComparison.OrdinalIgnoreCase))
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
                        Console.WriteLine(PRESS_ANY_KEY);
                        Console.ReadKey(true);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage(LoggerService.LogLevel.Error, ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
                    }

                }
            }
        }
    }
}
