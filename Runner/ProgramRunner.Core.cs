using System;
using System.Diagnostics;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet
{
    /// <summary>
    /// Shared constants, dependencies, and constructors for <see cref="ProgramRunner"/>.
    /// <see cref="ProgramRunner"/> の共有定数・依存関係・コンストラクターをまとめた partial です。
    /// </summary>
    public sealed partial class ProgramRunner
    {
        private const string INITIALIZING_LOGGER = "Initializing logger...";
        private const string LOGGER_INITIALIZED = "Logger initialized.";
        private const string VALIDATING_ARGS = "Validating command line arguments...";
        private const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";
        private const string PRESS_ANY_KEY = "Press any key to exit...";
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more modified files in 'new' have older timestamps than the corresponding files in 'old'. See diff_report for details.";
        private const string WARNING_IL_FILTER_STRINGS_TOO_SHORT = "One or more ILIgnoreLineContainingStrings entries are very short and may inadvertently exclude legitimate IL lines. See diff_report Warnings section for details.";
        private const string TIP_PRINT_CONFIG = "Tip: Run with --print-config to display the effective configuration as JSON.";
        private const string INFO_AUTO_GENERATED_REPORT_LABEL = "Report label was not specified. Using auto-generated label: ";

        private readonly ILoggerService _logger;
        private readonly ConfigService _configService;
        private readonly Action<ProcessStartInfo> _openFolderAction;
        private readonly UpdateNotificationService _updateNotificationService;

        /// <summary>
        /// Initializes a new instance of <see cref="ProgramRunner"/>.
        /// <see cref="ProgramRunner"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        /// <param name="configService">Service for loading configuration files. / 設定ファイル読込サービス。</param>
        public ProgramRunner(ILoggerService logger, ConfigService configService)
            : this(
                logger,
                configService,
                static processStartInfo => Process.Start(processStartInfo),
                new UpdateNotificationService())
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ProgramRunner"/> with a replaceable folder-open action for tests.
        /// テスト用に差し替え可能なフォルダ開放アクション付きで <see cref="ProgramRunner"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        /// <param name="configService">Service for loading configuration files. / 設定ファイル読込サービス。</param>
        /// <param name="openFolderAction">Action used by `--open-*` commands to launch the folder. / `--open-*` コマンドでフォルダを起動するためのアクション。</param>
        internal ProgramRunner(ILoggerService logger, ConfigService configService, Action<ProcessStartInfo> openFolderAction)
            : this(logger, configService, openFolderAction, new UpdateNotificationService())
        {
        }

        /// <summary>
        /// Initializes a testable runner with replaceable folder-open and update-notification services.
        /// フォルダ開放処理と更新通知サービスを差し替え可能にした、テスト向けランナーを初期化します。
        /// </summary>
        internal ProgramRunner(
            ILoggerService logger,
            ConfigService configService,
            Action<ProcessStartInfo> openFolderAction,
            UpdateNotificationService updateNotificationService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(openFolderAction);
            ArgumentNullException.ThrowIfNull(updateNotificationService);

            _logger = logger;
            _configService = configService;
            _openFolderAction = openFolderAction;
            _updateNotificationService = updateNotificationService;
        }
    }
}
