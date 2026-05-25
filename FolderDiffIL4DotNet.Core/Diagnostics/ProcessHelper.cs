using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Core.Diagnostics
{
    /// <summary>
    /// Provides process execution and command-line tokenization utilities.
    /// プロセス実行およびコマンドライン処理を提供するクラス。
    /// </summary>
    public static class ProcessHelper
    {
        /// <summary>
        /// Tokenizes a shell command string by whitespace, respecting single/double quotes.
        /// シェルコマンド文字列を簡易にトークン分割（空白区切り・クォート対応）。
        /// </summary>
        public static List<string> TokenizeCommand(string str)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(str))
            {
                return list;
            }

            bool inQuotes = false;
            char quoteChar = '\0';
            var current = new StringBuilder();

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (inQuotes)
                {
                    if (c == quoteChar)
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"' || c == '\'')
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (current.Length > 0)
                        {
                            list.Add(current.ToString());
                            current.Clear();
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }
            return list;
        }

        /// <summary>
        /// Launches a process and returns trimmed stdout (or stderr if stdout is empty) on exit code 0; returns null on failure.
        /// プロセスを起動し、終了コード 0 なら標準出力（空なら標準エラー）をトリムして返します。失敗時は null。
        /// </summary>
        public static async Task<string?> TryGetProcessOutputAsync(string exe, IEnumerable<string>? args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (args != null)
            {
                foreach (var arg in args)
                {
                    processStartInfo.ArgumentList.Add(arg);
                }
            }

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                var stdout = stdoutTask.Result;
                var stderr = stderrTask.Result;
                var outText = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
                return outText?.Trim();
            }
            return null;
        }

        /// <summary>
        /// Concatenates the command and arguments into a base label string.
        /// コマンドと引数を連結してベースラベルを返却します。
        /// </summary>
        public static string BuildBaseLabel(string command, string[] args)
        {
            var usedArgs = GetUsedArgs(args);
            return string.IsNullOrEmpty(usedArgs) ? command : $"{command} {usedArgs}";
        }

        /// <summary>
        /// Joins args into a single string, quoting any that contain spaces.
        /// 引数の配列から使用されている引数を取得し、スペースを含むものはクォートします。
        /// </summary>
        public static string GetUsedArgs(string[] args) => string.Join(" ", args.Select(x => x.Contains(' ') ? $"\"{x}\"" : x));
    }
}
