namespace FolderDiffIL4DotNet
{
    // Nested types used by ProgramRunner: exit codes, result models, and value records.
    // ProgramRunner が使用するネスト型: 終了コード・結果モデル・値レコード。
    public sealed partial class ProgramRunner
    {
        private sealed record RunArguments(string OldFolderAbsolutePath, string NewFolderAbsolutePath, string ReportsFolderAbsolutePath);

        private sealed record RunCompletionState(bool HasMd5MismatchWarnings, bool HasTimestampRegressionWarnings);

        /// <summary>
        /// Defines the public exit codes for the console application.
        /// コンソールアプリの公開終了コードを定義します。
        /// </summary>
        private enum ProgramExitCode
        {
            /// <summary>
            /// Successful completion. / 正常終了です。
            /// </summary>
            Success = 0,

            /// <summary>
            /// Invalid CLI arguments or input paths. / CLI 引数または入力パスが不正です。
            /// </summary>
            InvalidArguments = 2,

            /// <summary>
            /// Configuration file error or load failure. / 設定ファイルの不備または読込失敗です。
            /// </summary>
            ConfigurationError = 3,

            /// <summary>
            /// Diff execution or report generation failed. / 差分実行またはレポート生成に失敗しました。
            /// </summary>
            ExecutionFailed = 4,

            /// <summary>
            /// Unclassifiable unexpected error. / 分類不能な想定外エラーです。
            /// </summary>
            UnexpectedError = 1
        }

        /// <summary>
        /// Result model representing overall success or failure of a run.
        /// 実行全体の成功/失敗を表す結果モデルです。
        /// </summary>
        private sealed class ProgramRunResult
        {
            private static readonly RunCompletionState _noWarnings = new(false, false);

            public ProgramExitCode ExitCode { get; }
            public bool HasMd5MismatchWarnings { get; }
            public bool HasTimestampRegressionWarnings { get; }

            public static ProgramRunResult Success(RunCompletionState completionState)
                => new(ProgramExitCode.Success, completionState);

            public static ProgramRunResult Failure(ProgramExitCode exitCode)
                => new(exitCode, _noWarnings);

            private ProgramRunResult(ProgramExitCode exitCode, RunCompletionState completionState)
            {
                ExitCode = exitCode;
                HasMd5MismatchWarnings = completionState.HasMd5MismatchWarnings;
                HasTimestampRegressionWarnings = completionState.HasTimestampRegressionWarnings;
            }
        }

        /// <summary>
        /// A lightweight Result type that holds either a success value or a failure result for each execution phase.
        /// 各実行フェーズの成功値または失敗結果を保持する簡易 Result 型です。
        /// </summary>
        /// <typeparam name="TValue">The type of the success value. / 成功時の値型。</typeparam>
        private sealed class StepResult<TValue>
        {
            public bool IsSuccess { get; }
            public TValue Value { get; }
            public ProgramRunResult Failure { get; }

            public static StepResult<TValue> FromValue(TValue value)
                => new(true, value, null);

            public static StepResult<TValue> FromFailure(ProgramRunResult failure)
                => new(false, default, failure);

            private StepResult(bool isSuccess, TValue value, ProgramRunResult failure)
            {
                IsSuccess = isSuccess;
                Value = value;
                Failure = failure;
            }
        }
    }
}
