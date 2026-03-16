using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 逆アセンブラコマンドに関する共有静的ヘルパー。
    /// <see cref="DotNetDisassembleService"/> と <see cref="ILCachePrefetcher"/> の両方が使用するコマンド判定・
    /// 候補列挙・パス解決ロジックを一箇所にまとめ、重複を防ぎます。
    /// <para>
    /// Shared static helpers for disassembler commands.
    /// Centralises command identification, candidate enumeration, and path-resolution logic
    /// that is used by both <see cref="DotNetDisassembleService"/> and <see cref="ILCachePrefetcher"/>.
    /// </para>
    /// </summary>
    internal static class DisassemblerHelper
    {
        /// <summary>
        /// ユーザープロファイル直下の .NET ホームディレクトリ名。
        /// </summary>
        private const string DOTNET_HOME_DIRNAME = ".dotnet";

        /// <summary>
        /// .NET グローバルツールのサブディレクトリ名。
        /// </summary>
        private const string DOTNET_TOOLS_DIRNAME = "tools";

        /// <summary>
        /// ユーザーの .NET グローバルツールディレクトリ（例: ~/.dotnet/tools）。
        /// <para>
        /// The user's .NET global tools directory (e.g., <c>~/.dotnet/tools</c> on Unix,
        /// <c>%USERPROFILE%\.dotnet\tools</c> on Windows).
        /// </para>
        /// </summary>
        internal static string UserDotnetToolsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOTNET_HOME_DIRNAME, DOTNET_TOOLS_DIRNAME);

        /// <summary>
        /// 指定コマンドが dotnet マルチプレクサー（<c>dotnet</c> 実行ファイル）かを判定します。
        /// <para>
        /// Returns <see langword="true"/> if <paramref name="command"/> is the dotnet muxer
        /// (i.e., the bare <c>dotnet</c> executable used to invoke <c>dotnet ildasm</c>).
        /// </para>
        /// </summary>
        /// <param name="command">判定対象のコマンド文字列。</param>
        internal static bool IsDotnetMuxer(string command) =>
            string.Equals(command, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 指定コマンドが ilspycmd かを判定します。
        /// <para>
        /// Returns <see langword="true"/> if the file-name portion of <paramref name="command"/>
        /// matches <c>ilspycmd</c> (case-insensitive).
        /// </para>
        /// </summary>
        /// <param name="command">判定対象のコマンド文字列。</param>
        internal static bool IsIlspyCommand(string command) =>
            string.Equals(Path.GetFileName(command), Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 逆アセンブラ候補コマンドを優先順で列挙します。
        /// <para>
        /// Yields candidate disassembler commands in priority order:
        /// <c>dotnet-ildasm</c> (standalone), then the per-user install path,
        /// then the <c>dotnet</c> muxer, then <c>ilspycmd</c> (standalone and per-user).
        /// </para>
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
        /// コマンド名から実行ファイルの絶対パスを解決します。解決できない場合は <see langword="null"/>。
        /// <para>
        /// Resolves an executable name to its absolute path by searching <c>PATH</c>.
        /// Returns <see langword="null"/> when the executable cannot be found.
        /// </para>
        /// </summary>
        /// <param name="command">解決対象のコマンド名または絶対パス。</param>
        internal static string ResolveExecutablePath(string command)
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
        /// OS に応じて実行可能ファイル名候補を列挙します。
        /// Windows では <c>.exe</c> / <c>.cmd</c> / <c>.bat</c> の各拡張子を補完します。
        /// <para>
        /// Enumerates candidate executable names for the given command, adding OS-specific
        /// extensions on Windows (<c>.exe</c>, <c>.cmd</c>, <c>.bat</c>).
        /// </para>
        /// </summary>
        /// <param name="command">拡張子なしのコマンド名。</param>
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
