using System;
using System.IO;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet
{
    // Interactive wizard mode: prompts for oldFolder, newFolder, reportLabel.
    // 対話ウィザードモード: oldFolder、newFolder、reportLabel を対話入力。
    public sealed partial class ProgramRunner
    {
        private const string WIZARD_HEADER = "=== FolderDiffIL4DotNet Interactive Wizard ===";
        private const string WIZARD_PROMPT_OLD_FOLDER = "Enter the path to the OLD (baseline) folder:";
        private const string WIZARD_PROMPT_NEW_FOLDER = "Enter the path to the NEW (comparison) folder:";
        private const string WIZARD_PROMPT_REPORT_LABEL = "Enter the report label (subfolder name under Reports/):";
        private const string WIZARD_PROMPT_INDICATOR = "> ";
        private const string WIZARD_INPUT_EMPTY = "Input cannot be empty. Please try again.";
        private const string WIZARD_CONFIRM_HEADER = "--- Confirm settings ---";
        private const string WIZARD_CONFIRM_PROMPT = "Proceed? [Y/n]: ";
        private const string WIZARD_CANCELLED = "Wizard cancelled.";

        /// <summary>
        /// Runs the interactive wizard to collect oldFolder, newFolder, and reportLabel from the user,
        /// then proceeds with the normal diff pipeline.
        /// 対話ウィザードを実行して oldFolder, newFolder, reportLabel をユーザーから収集し、
        /// 通常の差分パイプラインに進みます。
        /// </summary>
        private async Task<int> RunWizardAsync(Runner.CliOptions opts)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine("--wizard requires an interactive terminal (stdin must not be redirected).");
                Console.Error.WriteLine("--wizard は対話端末が必要です（stdin がリダイレクトされていないこと）。");
                return (int)ProgramExitCode.InvalidArguments;
            }

            Console.WriteLine();
            Console.WriteLine(WIZARD_HEADER);
            Console.WriteLine();

            var oldFolder = PromptForInput(WIZARD_PROMPT_OLD_FOLDER);
            if (oldFolder == null) { Console.WriteLine(WIZARD_CANCELLED); return (int)ProgramExitCode.InvalidArguments; }

            var newFolder = PromptForInput(WIZARD_PROMPT_NEW_FOLDER);
            if (newFolder == null) { Console.WriteLine(WIZARD_CANCELLED); return (int)ProgramExitCode.InvalidArguments; }

            var reportLabel = PromptForInput(WIZARD_PROMPT_REPORT_LABEL);
            if (reportLabel == null) { Console.WriteLine(WIZARD_CANCELLED); return (int)ProgramExitCode.InvalidArguments; }

            // Show confirmation / 確認表示
            Console.WriteLine();
            Console.WriteLine(WIZARD_CONFIRM_HEADER);
            Console.WriteLine($"  Old folder:    {oldFolder}");
            Console.WriteLine($"  New folder:    {newFolder}");
            Console.WriteLine($"  Report label:  {reportLabel}");
            Console.Write(WIZARD_CONFIRM_PROMPT);

            var confirm = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(confirm)
                && !confirm.Equals("y", StringComparison.OrdinalIgnoreCase)
                && !confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(WIZARD_CANCELLED);
                return (int)ProgramExitCode.InvalidArguments;
            }

            Console.WriteLine();

            // Build synthetic args and delegate to normal pipeline
            // 合成引数を構築し通常パイプラインに委譲
            var syntheticArgs = new[] { oldFolder, newFolder, reportLabel };
            var result = await RunWithResultAsync(syntheticArgs, opts);
            OutputCompletionWarnings(result.HasSha256MismatchWarnings, result.HasTimestampRegressionWarnings);
            return (int)result.ExitCode;
        }

        /// <summary>
        /// Prompts the user for a non-empty input string. Returns null on EOF (Ctrl+D / Ctrl+Z).
        /// 非空の入力文字列をユーザーに要求します。EOF（Ctrl+D / Ctrl+Z）時は null を返します。
        /// </summary>
        private static string? PromptForInput(string prompt)
        {
            while (true)
            {
                Console.WriteLine(prompt);
                Console.Write(WIZARD_PROMPT_INDICATOR);
                var input = Console.ReadLine();
                if (input == null)
                {
                    return null; // EOF
                }
                input = input.Trim();
                if (input.Length > 0)
                {
                    return input;
                }
                Console.WriteLine(WIZARD_INPUT_EMPTY);
                Console.WriteLine();
            }
        }
    }
}
