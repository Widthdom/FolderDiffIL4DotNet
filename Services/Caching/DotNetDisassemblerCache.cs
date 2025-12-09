using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// 逆アセンブラ（ildasm/ilspycmd 等）のバージョン文字列を取得し、プロセス起動コストを避けるためにキャッシュするコンポーネント。
    /// </summary>
    public static class DotNetDisassemblerCache
    {
        #region constants
        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際の共通メッセージ
        /// </summary>
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION = "Failed to determine disassembler version";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（ラベル未指定）
        /// </summary>
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_EMPTY = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + ": empty command label.";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（ラベル付き）
        /// </summary>
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_FOR_LABEL = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + " for label: '{0}'.";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（無効なラベル）
        /// </summary>
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_INVALID = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + ": invalid command label '{0}'.";

        /// <summary>
        /// バージョン文字列取得失敗時の共通メッセージ
        /// </summary>
        private const string ERROR_FAILED_TO_GET_VERSION = "Failed to obtain version string for";

        /// <summary>
        /// バージョン取得失敗ログ詳細
        /// </summary>
        private const string LOG_FAILED_TO_GET_VERSION_DETAIL = "Failed to get version ({0}='{1}', {2}='{3}'): {4}";

        /// <summary>
        /// 共通フラグ: バージョン表示（ロング）
        /// </summary>
        private const string FLAG_VERSION_LONG = "--version";

        /// <summary>
        /// 共通フラグ: バージョン表示（ショート）
        /// </summary>
        private const string FLAG_VERSION_SHORT = "-v";

        /// <summary>
        /// 共通フラグ: ヘルプ（ショート）
        /// </summary>
        private const string FLAG_HELP_SHORT = "-h";
        #endregion

        /// <summary>
        /// 対応する逆アセンブラ種別（Unknown/dotnet-ildasm/ildasm/ilspy）。
        /// </summary>
        private enum DisassemblerKind
        {
            Unknown = 0,
            DotnetIldasm,
            Ildasm,
            Ilspy
        }

        /// <summary>
        /// コマンドラベルをキーにした逆アセンブラのバージョン文字列キャッシュ。
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> disassemblerVersionCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// コマンドラベル（例: "dotnet ildasm file.dll"）から逆アセンブラ種別を判定し、バージョン文字列を取得して返す（キャッシュあり）。
        /// </summary>
        /// <exception cref="InvalidOperationException">コマンドラベルが無効、またはバージョン取得に失敗した場合。</exception>
        public static async Task<string> GetDisassemblerVersionAsync(string disassembleCommandWithArguments)
        {
            // 入力の妥当性をまずチェック。空なら即例外。
            if (string.IsNullOrWhiteSpace(disassembleCommandWithArguments))
            {
                throw new InvalidOperationException(ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_EMPTY);
            }

            // コマンドから逆アセンブラ種別とキャッシュキー、実行ファイル名を抽出。
            var (disassemblerKind, disassemblerVersionCacheKey, disassemblerExe) = GetDisassemblerInfo(disassembleCommandWithArguments);
            // 種別に応じてバージョン取得ロジックを切り替え。該当が無ければエラーとして扱う。
            return disassemblerKind switch
            {
                DisassemblerKind.DotnetIldasm => await GetVersionForDotnetIldasmAsync(disassemblerVersionCacheKey, disassemblerExe),
                DisassemblerKind.Ildasm => await GetVersionForIldasmAsync(disassemblerVersionCacheKey, disassemblerExe),
                DisassemblerKind.Ilspy => await GetVersionForIlspyAsync(disassemblerVersionCacheKey, disassemblerExe),
                _ => throw new InvalidOperationException(string.Format(ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_FOR_LABEL, disassembleCommandWithArguments))
            };
        }

        /// <summary>
        /// コマンドラベル（例: "dotnet ildasm file.dll"）から逆アセンブラ種別を判定し、バージョン文字列を取得して返す（キャッシュあり）。
        /// </summary>
        private static (DisassemblerKind disassemblerKind, string disassemblerVersionCacheKey, string disassemblerExe) GetDisassemblerInfo(string disassembleCommandWithArguments)
        {
            // コマンド文字列をトークン化し、先頭のコマンド名やラベルを抽出する。
            var tokens = Utility.TokenizeCommand(disassembleCommandWithArguments);
            if (tokens.Count == 0)
            {
                throw new InvalidOperationException(string.Format(ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_INVALID, disassembleCommandWithArguments));
            }

            // "dotnet ildasm ..." 形式かどうかを確認。dotnet 経由の場合は muxer を実行ファイルとする。
            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count >= 2 && string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
                {
                    return (disassemblerKind: DisassemblerKind.DotnetIldasm, disassemblerVersionCacheKey: $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM}", disassemblerExe: Constants.DOTNET_MUXER);
                }
            }

            // それ以外は先頭トークンのファイル名で直接判定する。
            var exe = tokens[0];
            return Path.GetFileName(exe)?.ToLowerInvariant() switch
            {
                Constants.DOTNET_ILDASM => (disassemblerKind: DisassemblerKind.Ildasm, disassemblerVersionCacheKey: Constants.DOTNET_ILDASM, disassemblerExe: exe),
                Constants.ILSPY_CMD => (disassemblerKind: DisassemblerKind.Ilspy, disassemblerVersionCacheKey: Constants.ILSPY_CMD, disassemblerExe: exe),
                _ => (disassemblerKind: DisassemblerKind.Unknown, disassemblerVersionCacheKey: null, disassemblerExe: null)
            };
        }

        /// <summary>
        /// dotnet ildasm のバージョン情報を取得（キャッシュ）します。
        /// </summary>
        private static async Task<string> GetVersionForDotnetIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            // dotnet-ildasmはサブコマンド付きで --version / -v を順に試す。
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([Constants.DOTNET_ILDASM, FLAG_VERSION_LONG], false),
                ([Constants.DOTNET_ILDASM, FLAG_VERSION_SHORT], false)
            };
            return await GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.DOTNET_ILDASM);
        }

        /// <summary>
        /// ildasm のバージョン情報を取得（キャッシュ）します。
        /// </summary>
        private static async Task<string> GetVersionForIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            // ildasm は単純に --version と -v の二段構え。
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([FLAG_VERSION_LONG], false),
                ([FLAG_VERSION_SHORT], false)
            };
            return await GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.DOTNET_ILDASM);
        }

        /// <summary>
        /// ilspycmd のバージョン文字列を取得してキャッシュします（--version/-v/-h の順に試行）。
        /// </summary>
        private static async Task<string> GetVersionForIlspyAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            // ilspycmd は --version / -v が失敗する環境向けに -h の先頭行をフォールバックとして使用。
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([FLAG_VERSION_LONG], false),
                ([FLAG_VERSION_SHORT], false),
                ([FLAG_HELP_SHORT], true)
            };
            return await GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.ILSPY_CMD);
        }

        /// <summary>
        /// 複数パターンの引数でバージョン取得を試み、成功した結果をキャッシュおよび返却するユーティリティ。
        /// </summary>
        private static async Task<string> GetVersionWithFallbacksAsync(
            string disassemblerVersionCacheKey,
            string disassemblerExe,
            IEnumerable<(string[] args, bool useFirstLine)> attempts,
            string toolName)
        {
            // 指定された順にバージョン取得の試行を行う。成功した時点で結果を返し、キャッシュにも保存。
            foreach (var (args, useFirstLine) in attempts)
            {
                var rawVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, args);
                if (string.IsNullOrWhiteSpace(rawVersion))
                {
                    continue;
                }

                var processedVersion = useFirstLine
                    ? rawVersion.Split('\n').FirstOrDefault()?.Trim()
                    : rawVersion;

                if (!string.IsNullOrWhiteSpace(processedVersion))
                {
                    disassemblerVersionCache[disassemblerVersionCacheKey] = processedVersion;
                    return processedVersion;
                }
            }

            // すべて失敗した場合でも、過去に成功した結果が残っていればそちらを返す。
            if (disassemblerVersionCache.TryGetValue(disassemblerVersionCacheKey, out var cachedDisassemblerVersion))
            {
                return cachedDisassemblerVersion;
            }

            // キャッシュにも存在しない場合は例外を投げて呼び出し元へ通知。
            throw new InvalidOperationException($"{ERROR_FAILED_TO_GET_VERSION} '{toolName}' ({nameof(disassemblerVersionCacheKey)}='{disassemblerVersionCacheKey}').");
        }

        /// <summary>
        /// 指定された逆アセンブラのバージョン情報を取得します。
        /// </summary>
        private static async Task<string> TryGetDisassemblerVersionAsync(string disassemblerVersionCacheKey, string disassemblerExe, string[] args)
        {
            try
            {
                return await Utility.TryGetProcessOutputAsync(disassemblerExe, args);
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage(
                    LoggerService.LogLevel.Warning,
                    string.Format(
                        LOG_FAILED_TO_GET_VERSION_DETAIL,
                        nameof(disassemblerVersionCacheKey),
                        disassemblerVersionCacheKey,
                        nameof(disassemblerExe),
                        disassemblerExe,
                        ex.Message),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            return null;
        }
    }
}
