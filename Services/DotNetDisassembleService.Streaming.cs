using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Line-based (streaming) disassembly methods that avoid large single-string allocations.
    /// Reads process stdout line-by-line instead of <c>StreamReader.ReadToEndAsync</c>,
    /// keeping individual line strings small and GC-friendly (avoids Large Object Heap pressure).
    /// プロセスの stdout を行単位で読み取り、巨大な単一文字列割り当てを回避する
    /// ストリーミング逆アセンブルメソッド群。LOH 圧迫を防ぎ GC フレンドリーに動作します。
    /// </summary>
    public sealed partial class DotNetDisassembleService
    {
        /// <inheritdoc />
        public async Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
            string oldDotNetAssemblyFileAbsolutePath,
            string newDotNetAssemblyFileAbsolutePath,
            CancellationToken cancellationToken = default)
        {
            Exception? lastError = null;
            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                if (IsDisassemblerBlacklisted(candidateDisassembleCommand))
                {
                    continue;
                }

                try
                {
                    var oldResult = await TryDisassembleToLinesAsync(candidateDisassembleCommand, oldDotNetAssemblyFileAbsolutePath, allowCache: true, recordUsage: false);
                    if (!oldResult.Success)
                    {
                        if (oldResult.Error != null)
                        {
                            lastError = oldResult.Error;
                        }
                        continue;
                    }

                    var newResult = await TryDisassembleToLinesAsync(candidateDisassembleCommand, newDotNetAssemblyFileAbsolutePath, allowCache: true, recordUsage: false);
                    if (!newResult.Success)
                    {
                        if (newResult.Error != null)
                        {
                            lastError = newResult.Error;
                        }
                        continue;
                    }

                    if (!AreSameDisassemblerVersion(oldResult.Label!, newResult.Label!))
                    {
                        lastError = new InvalidOperationException($"Disassembler version mismatch for command '{candidateDisassembleCommand}'. old='{oldResult.Label}', new='{newResult.Label}'.");
                        continue;
                    }

                    RecordDisassemblerUsage(candidateDisassembleCommand, oldResult.Label!);
                    RecordDisassemblerUsage(candidateDisassembleCommand, newResult.Label!);
                    return (
                        oldResult.IlLines!,
                        oldResult.Label!,
                        newResult.IlLines!,
                        newResult.Label!);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    lastError = ex;
                    _logger.LogMessage(AppLogLevel.Warning, BuildStartDisassemblerToolWarning(candidateDisassembleCommand, ex), shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
            }

            var innerMsg = lastError != null ? $" RootCause: {lastError.Message}" : string.Empty;
            throw new InvalidOperationException(
                $"Failed to execute ildasm with the same disassembler for files: {oldDotNetAssemblyFileAbsolutePath} and {newDotNetAssemblyFileAbsolutePath}. {GUIDANCE_INSTALL_DISASSEMBLER}{innerMsg}",
                lastError);
        }

        /// <summary>
        /// Attempts disassembly returning IL as lines. Creates a temp ASCII path if needed
        /// and tries each argument set in turn.
        /// IL を行リストとして返す逆アセンブル試行。必要に応じて ASCII 一時パスを生成し、
        /// 複数の引数セットを順に試します。
        /// </summary>
        private async Task<(bool Success, IReadOnlyList<string>? IlLines, string? Label, Exception? Error)> TryDisassembleToLinesAsync(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            bool allowCache,
            bool recordUsage)
        {
            Exception? lastError = null;
            string? tempAsciiPath = CreateAsciiTempCopyIfNeeded(dotNetAssemblyFileAbsolutePath);

            try
            {
                foreach (var argset in BuildArgSets(disassembleCommand, dotNetAssemblyFileAbsolutePath, tempAsciiPath))
                {
                    var (success, ilLines, label, error) = await TryDisassembleWithArgumentsToLines(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset, allowCache, recordUsage);
                    if (success)
                    {
                        return (success, ilLines, label, error);
                    }
                    if (error != null)
                    {
                        lastError = error;
                    }
                }
            }
            finally
            {
                CleanupTemporaryPathBestEffort(tempAsciiPath, "ASCII temp assembly copy");
            }

            return (Success: false, IlLines: null, Label: null, Error: lastError);
        }

        /// <summary>
        /// Tries disassembly with a single argument set, returning IL as lines: cache check, process execution (line-by-line stdout), cache store.
        /// 1 つの引数セットで逆アセンブルを試行し IL を行リストで返します。キャッシュ事前チェック→行単位プロセス実行→キャッシュ格納。
        /// </summary>
        private async Task<(bool Success, IReadOnlyList<string>? IlLines, string? Label, Exception? Error)> TryDisassembleWithArgumentsToLines(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            (string workingDirectory, string[] args, string? tempOut) argset,
            bool allowCache,
            bool recordUsage)
        {
            string? label = null;

            if (allowCache)
            {
                // Cache hit: split cached string into lines (avoids re-launching the process).
                // キャッシュヒット: キャッシュ文字列を行に分割（プロセス再起動を回避）。
                var (hit, cachedIl, computedLabel) = await TryCacheHitAsync(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset.args, recordUsage);
                label = computedLabel;
                if (hit)
                {
                    return (Success: true, IlLines: StripDisassemblerStdoutNoticeLines(SplitToLines(cachedIl!)), Label: label, Error: null);
                }
            }

            // Cache miss — launch the process and read stdout line-by-line.
            // キャッシュミス — プロセスを起動して stdout を行単位で読み取る。
            var (exitCode, stdoutLines, stderr, error) = await RunProcessAsLinesAsync(disassembleCommand, argset.workingDirectory, argset.args, _config.DisassemblerTimeoutSeconds);
            if (error != null)
            {
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlLines: null, Label: null, Error: error);
            }

            if (exitCode == 0)
            {
                ResetDisassembleFailure(disassembleCommand);
                var ilLines = await ReadIlLinesAfterSuccessAsync(IsIlspyCommand(disassembleCommand), argset, stdoutLines!);
                if (string.IsNullOrEmpty(label))
                {
                    label = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(ProcessHelper.BuildBaseLabel(disassembleCommand, argset.args));
                }
                // Store to cache as a joined string (cache API uses strings).
                // キャッシュへの格納は文字列結合で行う（キャッシュ API は文字列ベース）。
                await TryStoreToCacheAsync(dotNetAssemblyFileAbsolutePath, label, JoinLines(ilLines), disassembleCommand);
                if (recordUsage)
                {
                    RecordDisassemblerUsage(disassembleCommand, label);
                }
                return (Success: true, IlLines: ilLines, Label: label, Error: null);
            }
            else
            {
                var lastError = new InvalidOperationException(
                    $"ildasm failed (exit {exitCode}) with command: {disassembleCommand} {ProcessHelper.GetUsedArgs(argset.args)} in {argset.workingDirectory}\n" +
                    $"File: {dotNetAssemblyFileAbsolutePath}\nStderr: {stderr}\n" +
                    $"Hint: Common causes include corrupt assemblies, unsupported formats, or tool version incompatibility. If this persists, try updating the disassembler tool or use --skip-il to bypass IL comparison.");
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlLines: null, Label: null, Error: lastError);
            }
        }

        /// <summary>
        /// Reads IL lines after a successful process exit.
        /// ilspycmd reads from a temp file; other tools use the stdout lines already collected.
        /// プロセス正常終了後の IL 行を取得。ilspycmd は一時ファイルから、他ツールは収集済み stdout 行を使用。
        /// </summary>
        private async Task<IReadOnlyList<string>> ReadIlLinesAfterSuccessAsync(
            bool isIlspy,
            (string workingDirectory, string[] args, string? tempOut) argset,
            List<string> stdoutLines)
        {
            if (isIlspy && !string.IsNullOrEmpty(argset.tempOut) && File.Exists(argset.tempOut))
            {
                try
                {
                    return StripDisassemblerStdoutNoticeLines(await File.ReadAllLinesAsync(argset.tempOut));
                }
                finally
                {
                    CleanupTemporaryPathBestEffort(argset.tempOut, "ilspy temporary output");
                }
            }
            return StripDisassemblerStdoutNoticeLines(stdoutLines);
        }

        /// <summary>
        /// Launches the command, reads stdout line-by-line (avoids large single-string allocation on LOH),
        /// and returns exit code / stdout lines / stderr.
        /// コマンドを起動し stdout を行単位で読み取ります（LOH への巨大文字列割り当てを回避）。
        /// 終了コード・stdout 行リスト・stderr を返します。
        /// </summary>
        private static async Task<(int ExitCode, List<string>? StdoutLines, string? Stderr, Exception? Error)> RunProcessAsLinesAsync(string disassembleCommand, string workingDirectoryAbsolutePath, string[] args, int timeoutSeconds = 0)
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

                // Read stdout line-by-line to avoid a single large string allocation.
                // stdout を行単位で読み取り、単一の巨大文字列割り当てを回避する。
                var stdoutLines = new List<string>();
                var readLinesTask = ReadAllLinesFromStreamAsync(process.StandardOutput, stdoutLines);
                var errTask = process.StandardError.ReadToEndAsync();

                if (timeoutSeconds > 0)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout — kill the process / タイムアウト — プロセスを強制終了
                        try
                        {
                            process.Kill(entireProcessTree: true);
                        }
                        catch (Exception ex) when (ex is InvalidOperationException
                            or NotSupportedException
                            or System.ComponentModel.Win32Exception)
                        {
                            // Process already exited, not supported by platform, or native kill failed —
                            // the caller still reports the timeout as the effective error below.
                            // プロセスが既に終了、プラットフォーム非対応、ネイティブ kill 失敗 —
                            // いずれも下の TimeoutException を実効エラーとして返すのでベストエフォート。
                        }
                        return (ExitCode: int.MinValue, StdoutLines: null, Stderr: null,
                            Error: new TimeoutException($"Disassembler process '{disassembleCommand}' timed out after {timeoutSeconds} seconds. To increase the limit, set DisassemblerTimeoutSeconds in config.json (current: {timeoutSeconds})."));
                    }
                }
                else
                {
                    await process.WaitForExitAsync();
                }

                await Task.WhenAll(readLinesTask, errTask);
                var errorOutput = errTask.Result;
                return (ExitCode: process.ExitCode, StdoutLines: stdoutLines, Stderr: errorOutput, Error: null);
            }
            catch (Exception ex) when (ExceptionFilters.IsProcessExecutionRecoverable(ex))
            {
                return (ExitCode: int.MinValue, StdoutLines: null, Stderr: null, Error: ex);
            }
        }

        /// <summary>
        /// Reads all lines from a <see cref="StreamReader"/> asynchronously into the target list.
        /// <see cref="StreamReader"/> から全行を非同期で読み取り対象リストに追加します。
        /// </summary>
        private static async Task ReadAllLinesFromStreamAsync(StreamReader reader, List<string> target)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                target.Add(line);
            }
        }

        /// <summary>
        /// Splits a string into lines using <see cref="StringReader"/> (for cache-hit path).
        /// <see cref="StringReader"/> を使い文字列を行に分割します（キャッシュヒット時に使用）。
        /// </summary>
        internal static List<string> SplitToLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            var lines = new List<string>();
            using var reader = new StringReader(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>
        /// Joins lines with newline separator for cache storage.
        /// キャッシュ格納用に行を改行で結合します。
        /// </summary>
        private static string JoinLines(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
            {
                return string.Empty;
            }
            return string.Join('\n', lines);
        }
    }
}
