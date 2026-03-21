using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Shared static helpers for disassembler commands.
    /// Centralises command identification, candidate enumeration, and path-resolution logic
    /// that is used by both <see cref="DotNetDisassembleService"/> and <see cref="ILCachePrefetcher"/>.
    /// 逆アセンブラコマンドに関する共有静的ヘルパー。
    /// <see cref="DotNetDisassembleService"/> と <see cref="ILCachePrefetcher"/> の両方が使用するコマンド判定・
    /// 候補列挙・パス解決ロジックを一箇所にまとめ、重複を防ぎます。
    /// </summary>
    internal static class DisassemblerHelper
    {
        private const string DOTNET_HOME_DIRNAME = ".dotnet";
        private const string DOTNET_TOOLS_DIRNAME = "tools";

        /// <summary>
        /// The user's .NET global tools directory (e.g., <c>~/.dotnet/tools</c> on Unix, <c>%USERPROFILE%\.dotnet\tools</c> on Windows).
        /// ユーザーの .NET グローバルツールディレクトリ（例: ~/.dotnet/tools）。
        /// </summary>
        internal static string UserDotnetToolsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOTNET_HOME_DIRNAME, DOTNET_TOOLS_DIRNAME);

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="command"/> is the dotnet muxer (the bare <c>dotnet</c> executable used to invoke <c>dotnet ildasm</c>).
        /// 指定コマンドが dotnet マルチプレクサー（<c>dotnet</c> 実行ファイル）かを判定します。
        /// </summary>
        internal static bool IsDotnetMuxer(string command) =>
            string.Equals(command, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns <see langword="true"/> if the file-name portion of <paramref name="command"/> matches <c>ilspycmd</c> (case-insensitive).
        /// 指定コマンドが ilspycmd かを判定します。
        /// </summary>
        internal static bool IsIlspyCommand(string command) =>
            string.Equals(Path.GetFileName(command), Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Yields candidate disassembler commands in priority order:
        /// <c>dotnet-ildasm</c> (standalone), per-user install path, <c>dotnet</c> muxer, then <c>ilspycmd</c> (standalone and per-user).
        /// 逆アセンブラ候補コマンドを優先順で列挙します。
        /// </summary>
        internal static IEnumerable<string> CandidateDisassembleCommands()
        {
            yield return Constants.DOTNET_ILDASM;
            yield return Path.Combine(UserDotnetToolsDirectory, Constants.DOTNET_ILDASM);
            yield return Constants.DOTNET_MUXER;
            yield return Constants.ILSPY_CMD;
            yield return Path.Combine(UserDotnetToolsDirectory, Constants.ILSPY_CMD);
        }

        /// <summary>
        /// Resolves an executable name to its absolute path by searching <c>PATH</c>. Returns <see langword="null"/> when not found.
        /// コマンド名から実行ファイルの絶対パスを解決します。解決できない場合は <see langword="null"/>。
        /// </summary>
        internal static string? ResolveExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            if (Path.IsPathRooted(command))
            {
                return File.Exists(command) ? Path.GetFullPath(command) : null;
            }

            if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            {
                var fullPath = Path.GetFullPath(command);
                return File.Exists(fullPath) ? fullPath : null;
            }

            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathVariable))
            {
                return null;
            }

            foreach (var pathEntry in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathEntry))
                {
                    continue;
                }
                foreach (var candidateName in EnumerateExecutableNames(command))
                {
                    var candidateAbsolutePath = Path.Combine(pathEntry, candidateName);
                    if (File.Exists(candidateAbsolutePath))
                    {
                        return Path.GetFullPath(candidateAbsolutePath);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Probes all candidate disassemblers and returns their availability.
        /// Each unique tool name is probed once; duplicate candidates (e.g. <c>dotnet-ildasm</c>
        /// in PATH vs per-user tools directory) are collapsed into a single result.
        /// すべての逆アセンブラ候補をプローブし、利用可否を返します。
        /// 同一ツール名の重複候補は単一の結果にまとめられます。
        /// </summary>
        internal static IReadOnlyList<DisassemblerProbeResult> ProbeAllCandidates()
        {
            var results = new List<DisassemblerProbeResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in CandidateDisassembleCommands())
            {
                var toolName = NormalizeCandidateName(candidate);
                if (!seen.Add(toolName))
                {
                    continue;
                }

                var (available, version, resolvedPath) = ProbeCandidate(candidate);
                results.Add(new DisassemblerProbeResult(toolName, available, version, resolvedPath));
            }

            return results;
        }

        /// <summary>
        /// Probes a single disassembler candidate by attempting to execute <c>--version</c>.
        /// 単一の逆アセンブラ候補を <c>--version</c> 実行でプローブします。
        /// </summary>
        private static (bool Available, string? Version, string? ResolvedPath) ProbeCandidate(string candidate)
        {
            try
            {
                var resolved = ResolveExecutablePath(candidate);
                var isMuxer = IsDotnetMuxer(candidate);

                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                if (isMuxer)
                {
                    psi.ArgumentList.Add(Constants.ILDASM_LABEL);
                }
                psi.ArgumentList.Add("--version");

                using var process = new Process { StartInfo = psi };
                process.Start();

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(millisecondsTimeout: 10_000))
                {
                    try { process.Kill(); } catch { /* best-effort */ }
                    return (false, null, resolved);
                }

                if (process.ExitCode == 0)
                {
                    var versionText = (stdout ?? stderr ?? string.Empty)
                        .Replace("\r", " ").Replace("\n", " ").Trim();
                    return (true, string.IsNullOrEmpty(versionText) ? null : versionText, resolved);
                }

                return (false, null, resolved);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                return (false, null, null);
            }
        }

        /// <summary>
        /// Normalises a candidate command string to a human-readable tool name.
        /// 候補コマンド文字列を人間が読みやすいツール名に正規化します。
        /// </summary>
        private static string NormalizeCandidateName(string candidate)
        {
            if (IsDotnetMuxer(candidate))
            {
                return Constants.DOTNET_ILDASM;
            }

            var fileName = Path.GetFileName(candidate);
            if (string.Equals(fileName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.DOTNET_ILDASM;
            }
            if (string.Equals(fileName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ILSPY_CMD;
            }
            return fileName ?? candidate;
        }

        /// <summary>
        /// Enumerates candidate executable names for the given command, adding OS-specific extensions on Windows (<c>.exe</c>, <c>.cmd</c>, <c>.bat</c>).
        /// OS に応じて実行可能ファイル名候補を列挙します。Windows では <c>.exe</c> / <c>.cmd</c> / <c>.bat</c> の各拡張子を補完します。
        /// </summary>
        internal static IEnumerable<string> EnumerateExecutableNames(string command)
        {
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { command };
            if (OperatingSystem.IsWindows())
            {
                if (!command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".exe");
                }
                if (!command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".cmd");
                }
                if (!command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".bat");
                }
            }
            return hashSet;
        }
    }
}
