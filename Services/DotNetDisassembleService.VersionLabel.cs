using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Version/label management, tool fingerprinting, argument construction,
    /// process execution, and disassembler usage recording.
    /// バージョン/ラベル管理、ツールフィンガープリント、引数構築、
    /// プロセス実行、逆アセンブラ使用記録。
    /// </summary>
    public sealed partial class DotNetDisassembleService
    {
        private const string VERSION_LABEL_PREFIX = " (version: ";
        private const string UNKNOWN_VERSION_FINGERPRINT_PREFIX = "unavailable; fingerprint: ";
        private const string RUN_FINGERPRINT_PREFIX = "run:";

        private static bool IsDotnetMuxer(string command) => DisassemblerHelper.IsDotnetMuxer(command);
        private static bool IsIlspyCommand(string command) => DisassemblerHelper.IsIlspyCommand(command);

        /// <summary>
        /// Returns a temp ASCII-path copy when the path contains non-ASCII characters; null otherwise.
        /// パスに非ASCII文字がある場合に ASCII 一時パスへコピーしたファイルのパスを返します。該当しなければ null。
        /// </summary>
        private string? CreateAsciiTempCopyIfNeeded(string dotNetAssemblyFileAbsolutePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(dotNetAssemblyFileAbsolutePath) && TextSanitizer.ContainsNonAscii(dotNetAssemblyFileAbsolutePath))
                {
                    var tempAsciiPath = Path.Combine(Path.GetTempPath(), $"ildasm_input_{Guid.NewGuid():N}.dll");
                    File.Copy(dotNetAssemblyFileAbsolutePath, tempAsciiPath, overwrite: true);
                    return tempAsciiPath;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            return null;
        }

        /// <summary>
        /// Enumerates argument sets to try, based on the command type.
        /// コマンド種別に応じた試行用の引数セットを列挙します。
        /// </summary>
        private static IEnumerable<(string workingDirectory, string[] args, string? tempOut)> BuildArgSets(string disassembleCommand, string disassemblerFileAbsolutePath, string? tempAsciiPath)
        {
            var disassemblerFileDirectoryAbsolutePath = Path.GetDirectoryName(disassemblerFileAbsolutePath) ?? Environment.CurrentDirectory;
            var disassemblerFileNameOnly = Path.GetFileName(disassemblerFileAbsolutePath);
            var isDotnetMuxer = IsDotnetMuxer(disassembleCommand);
            var isIlspy = IsIlspyCommand(disassembleCommand);

            var argSets = new List<(string workingDirectory, string[] args, string? tempOut)>();
            if (!isIlspy)
            {
                argSets.Add((disassemblerFileDirectoryAbsolutePath, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileNameOnly] : [disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileAbsolutePath] : [disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, tempAsciiPath] : [tempAsciiPath], null));
                }
            }
            else
            {
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [ILSPY_FLAG_IL, disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, tempAsciiPath], null));
                }
                static string MakeTempOut() => Path.Combine(Path.GetTempPath(), $"ilspy_out_{Guid.NewGuid():N}.il");
                var out1 = MakeTempOut();
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out1, disassemblerFileNameOnly], out1));
                var out2 = MakeTempOut();
                argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out2, disassemblerFileAbsolutePath], out2));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    var out3 = MakeTempOut();
                    argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out3, tempAsciiPath], out3));
                }
            }
            return argSets;
        }

        /// <summary>
        /// Appends the tool version to the base label. Returns the base label as-is on failure.
        /// ベースラベルにツールバージョンを付加して返します。取得失敗時はそのまま返します。
        /// </summary>
        private async Task<string> GetDisassembleCommandAndItsVersionWithArgumentsAsync(string disassembleCommandWithArguments)
        {
            try
            {
                var disassemblerVersion = await _dotNetDisassemblerCache.GetDisassemblerVersionAsync(disassembleCommandWithArguments);
                var disassemblerVersionOneLine = disassemblerVersion.Replace("\r", " ").Replace("\n", " ").Trim();
                return string.IsNullOrEmpty(disassemblerVersionOneLine) ? disassembleCommandWithArguments : $"{disassembleCommandWithArguments} (version: {disassemblerVersionOneLine})";
            }
            catch (InvalidOperationException)
            {
                var fingerprint = BuildToolFingerprint(disassembleCommandWithArguments);
                return $"{disassembleCommandWithArguments} (version: {UNKNOWN_VERSION_FINGERPRINT_PREFIX}{fingerprint})";
            }
        }

        /// <summary>
        /// Builds a fingerprint for the disassembler binary from the command string.
        /// Falls back to the per-run identifier when no binary can be resolved.
        /// コマンド文字列から逆アセンブラ実体のフィンガープリントを構築します。
        /// 取得できる実体がない場合は実行単位識別子を返します。
        /// </summary>
        private static string BuildToolFingerprint(string disassembleCommandWithArguments)
        {
            var tokens = ProcessHelper.TokenizeCommand(disassembleCommandWithArguments);
            if (tokens.Count == 0)
            {
                return RUN_FINGERPRINT_PREFIX + _runFingerprint;
            }

            var executableCandidates = new List<string>();
            var head = Path.GetFileName(tokens[0]) ?? tokens[0];
            if (string.Equals(head, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase) &&
                tokens.Count >= 2 &&
                string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                executableCandidates.Add(tokens[0]);
                executableCandidates.Add(Constants.DOTNET_ILDASM);
                executableCandidates.Add(Path.Combine(UserDotnetToolsDirectory, Constants.DOTNET_ILDASM));
            }
            else
            {
                executableCandidates.Add(tokens[0]);
            }

            var fingerprints = new List<string>();
            foreach (var executableCandidate in executableCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var resolved = ResolveExecutablePath(executableCandidate);
                if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                {
                    continue;
                }

                var fileInfo = new FileInfo(resolved);
                fingerprints.Add($"{Path.GetFileName(resolved)}@{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}");
            }

            if (fingerprints.Count == 0)
            {
                return RUN_FINGERPRINT_PREFIX + _runFingerprint;
            }

            return string.Join("|", fingerprints.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        private static string? ResolveExecutablePath(string command) => DisassemblerHelper.ResolveExecutablePath(command);

        /// <summary>
        /// Launches the command, waits for exit, and returns exit code / stdout / stderr.
        /// Returns the exception instead of throwing when the process cannot start.
        /// 指定コマンドを起動して終了を待ち、終了コードと標準出力/標準エラーを返します。
        /// 起動失敗時は例外をタプルに含めて返します。
        /// </summary>
        private static async Task<(int ExitCode, string? Stdout, string? Stderr, Exception? Error)> RunProcessAsync(string disassembleCommand, string workingDirectoryAbsolutePath, string[] args)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = disassembleCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectoryAbsolutePath
                };
                foreach (var arg in args)
                {
                    processStartInfo.ArgumentList.Add(arg);
                }

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                await process.WaitForExitAsync();
                var stdOutput = outTask.Result;
                var errorOutput = errTask.Result;
                return (ExitCode: process.ExitCode, Stdout: stdOutput, Stderr: errorOutput, Error: null);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
        }

        private static string UserDotnetToolsDirectory => DisassemblerHelper.UserDotnetToolsDirectory;

        private void RecordDisassemblerUsage(string disassembleCommand, string disassembleCommandAndItsVersionWithArguments, bool fromCache = false)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandAndItsVersionWithArguments))
            {
                return;
            }

            var toolName = NormalizeDisassemblerName(disassembleCommand);
            var version = ExtractVersionFromLabel(disassembleCommandAndItsVersionWithArguments);
            _fileDiffResultLists.RecordDisassemblerToolVersion(toolName, version, fromCache);
        }

        private static string NormalizeDisassemblerName(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return disassembleCommand;
            }

            var fileName = Path.GetFileName(disassembleCommand);
            if (string.Equals(fileName, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.DOTNET_ILDASM;
            }
            if (string.Equals(fileName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.DOTNET_ILDASM;
            }
            if (string.Equals(fileName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ILSPY_CMD;
            }
            if (string.Equals(fileName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ILDASM_LABEL;
            }
            return fileName ?? disassembleCommand;
        }

        private static string? ExtractVersionFromLabel(string disassembleCommandAndItsVersionWithArguments)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandAndItsVersionWithArguments))
            {
                return null;
            }

            if (!disassembleCommandAndItsVersionWithArguments.EndsWith(")", StringComparison.Ordinal))
            {
                return null;
            }

            var prefixIndex = disassembleCommandAndItsVersionWithArguments.LastIndexOf(VERSION_LABEL_PREFIX, StringComparison.Ordinal);
            if (prefixIndex < 0)
            {
                return null;
            }

            var start = prefixIndex + VERSION_LABEL_PREFIX.Length;
            var end = disassembleCommandAndItsVersionWithArguments.Length - 1;
            if (start >= end)
            {
                return null;
            }

            return disassembleCommandAndItsVersionWithArguments.Substring(start, end - start).Trim();
        }

        private static bool AreSameDisassemblerVersion(string oldLabel, string newLabel)
        {
            var oldVersion = ExtractVersionFromLabel(oldLabel) ?? string.Empty;
            var newVersion = ExtractVersionFromLabel(newLabel) ?? string.Empty;
            return string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
