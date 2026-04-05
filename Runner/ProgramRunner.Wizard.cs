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
        private const string WIZARD_PROMPT_OLD_FOLDER = "Enter the path to the OLD (baseline) folder (drag & drop OK):";
        private const string WIZARD_PROMPT_NEW_FOLDER = "Enter the path to the NEW (comparison) folder (drag & drop OK):";
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
            OutputCompletionWarnings(result.HasSha256MismatchWarnings, result.HasTimestampRegressionWarnings, result.HasILFilterWarnings);
            return (int)result.ExitCode;
        }

        /// <summary>
        /// Prompts the user for a non-empty input string. Returns null on EOF (Ctrl+D / Ctrl+Z).
        /// Automatically strips surrounding quotes (single/double) and <c>file://</c> URI prefixes
        /// that terminals or file managers insert during drag-and-drop operations.
        /// 非空の入力文字列をユーザーに要求します。EOF（Ctrl+D / Ctrl+Z）時は null を返します。
        /// ターミナルやファイルマネージャがドラッグ＆ドロップ時に挿入する囲みクォート
        /// （シングル/ダブル）および <c>file://</c> URI プレフィックスを自動除去します。
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
                input = NormalizeDragDropPath(input);
                if (input.Length > 0)
                {
                    // Resolve to absolute path (handles relative paths and normalizes separators)
                    // 絶対パスに解決（相対パスの処理とセパレータ正規化）
                    return Path.GetFullPath(input);
                }
                Console.WriteLine(WIZARD_INPUT_EMPTY);
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Normalize a path string from drag-and-drop input by stripping surrounding quotes,
        /// <c>file://</c> URI prefixes, and trailing path separators that appear inside quotes.
        /// Handles: <c>"C:\folder\"</c>, <c>'~/folder'</c>, <c>file:///home/user/folder</c>,
        /// and backslash-escaped spaces (<c>path\ with\ spaces</c> → <c>path with spaces</c>).
        /// ドラッグ＆ドロップ入力のパス文字列を正規化します。囲みクォート、<c>file://</c>
        /// URI プレフィックス、クォート内末尾パス区切りを除去します。
        /// </summary>
        internal static string NormalizeDragDropPath(string input)
        {
            input = input.Trim();

            // Strip matching surrounding quotes (double or single)
            // 一致する囲みクォート（ダブルまたはシングル）を除去
            if (input.Length >= 2)
            {
                char first = input[0];
                char last = input[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    input = input[1..^1];
                }
            }

            // Also strip unmatched leading/trailing quotes from mixed D&D scenarios
            // 混合 D&D シナリオの不一致な先頭/末尾クォートも除去
            input = input.Trim('"', '\'');

            // Strip file:// or file:/// URI prefix (some file managers produce this on D&D)
            // file:// または file:/// URI プレフィックスを除去（一部ファイルマネージャが D&D 時に生成）
            if (input.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                input = input[7..]; // "file:///home/..." → "/home/..." (Unix), "file:///C:/..." → "C:/..." (Windows via Path.GetFullPath)
                // On Windows, file:///C:/path has an extra leading slash → C:/path
                // Path.GetFullPath will normalize this correctly
            }
            else if (input.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                input = input[7..]; // "file://server/share" → "server/share"
            }

            // Unescape backslash-escaped spaces (common in Unix terminal D&D without quotes)
            // バックスラッシュエスケープされたスペースを復元（Unix ターミナルでクォートなし D&D 時に一般的）
            if (input.Contains("\\ "))
            {
                input = input.Replace("\\ ", " ");
            }

            // Unescape URI percent-encoding for common characters (space, Japanese chars)
            // URI パーセントエンコーディングの一般的な文字をデコード（スペース、日本語文字）
            if (input.Contains('%'))
            {
                try
                {
                    input = Uri.UnescapeDataString(input);
                }
#pragma warning disable CA1031 // ベストエフォートの URI デコード / Best-effort URI decode
                catch
                {
                    // If UnescapeDataString fails (malformed %), keep the original
                    // UnescapeDataString が失敗した場合（不正な %）、元の文字列を保持
                }
#pragma warning restore CA1031
            }

            return input.Trim();
        }
    }
}
