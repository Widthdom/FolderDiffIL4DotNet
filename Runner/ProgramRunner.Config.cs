using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet
{
    // Configuration loading, validation, and CLI override methods.
    // 設定読込・バリデーション・CLI オーバーライドメソッド。
    public sealed partial class ProgramRunner
    {
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";

        /// <summary>
        /// Interactive wizard for selective IL cache deletion.
        /// Presents a menu to delete all cache files, filter by tool name, or filter by specific tool version.
        /// IL キャッシュの選択的削除ウィザード。
        /// 全キャッシュ削除、ツール名フィルタ、特定バージョンフィルタのメニューを表示します。
        /// </summary>
        private async Task<int> ClearCacheAsync(string? configPath)
        {
            if (Console.IsInputRedirected)
            {
                Console.Error.WriteLine("--clear-cache requires an interactive terminal (stdin must not be redirected).");
                return (int)ProgramExitCode.InvalidArguments;
            }

            try
            {
                // Resolve cache directory from config (if available) or use default
                // 設定（利用可能な場合）またはデフォルトからキャッシュディレクトリを解決
                string cacheDir = await ResolveCacheDirectoryAsync(configPath);

                if (!Directory.Exists(cacheDir))
                {
                    Console.WriteLine($"IL cache directory does not exist: {cacheDir}");
                    Console.WriteLine("Nothing to clear.");
                    return 0;
                }

                var cacheFiles = Directory.GetFiles(cacheDir, "*.ilcache");
                if (cacheFiles.Length == 0)
                {
                    Console.WriteLine($"IL cache directory is empty: {cacheDir}");
                    Console.WriteLine("Nothing to clear.");
                    return 0;
                }

                // Classify files by tool / ツール別にファイルを分類
                var ildasmFiles = FilterCacheFilesByTool(cacheFiles, CACHE_TOOL_ILDASM);
                var ilspyFiles = FilterCacheFilesByTool(cacheFiles, CACHE_TOOL_ILSPY);
                var otherFiles = cacheFiles.Length - ildasmFiles.Length - ilspyFiles.Length;

                Console.WriteLine();
                Console.WriteLine("=== IL Cache Clear Wizard ===");
                Console.WriteLine();
                Console.WriteLine($"Cache directory: {cacheDir}");
                Console.WriteLine($"Total cache files: {cacheFiles.Length}");
                if (ildasmFiles.Length > 0) Console.WriteLine($"  dotnet-ildasm: {ildasmFiles.Length} file(s)");
                if (ilspyFiles.Length > 0) Console.WriteLine($"  ilspycmd:      {ilspyFiles.Length} file(s)");
                if (otherFiles > 0) Console.WriteLine($"  other/unknown: {otherFiles} file(s)");
                Console.WriteLine();
                Console.WriteLine("Select an option:");
                Console.WriteLine("  1. Delete all cache files");
                if (ilspyFiles.Length > 0) Console.WriteLine($"  2. Delete ilspycmd cache only ({ilspyFiles.Length} file(s))");
                if (ildasmFiles.Length > 0) Console.WriteLine($"  3. Delete dotnet-ildasm cache only ({ildasmFiles.Length} file(s))");
                Console.WriteLine("  4. Delete by specific tool version");
                Console.WriteLine("  5. Cancel");
                Console.WriteLine();
                Console.Write("> ");

                var choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice) || choice == "5")
                {
                    Console.WriteLine("Cancelled.");
                    return 0;
                }

                string[] filesToDelete;
                string description;

                switch (choice)
                {
                    case "1":
                        filesToDelete = cacheFiles;
                        description = "all";
                        break;
                    case "2":
                        filesToDelete = ilspyFiles;
                        description = "ilspycmd";
                        break;
                    case "3":
                        filesToDelete = ildasmFiles;
                        description = "dotnet-ildasm";
                        break;
                    case "4":
                        // Enumerate distinct version labels from cache filenames
                        // キャッシュファイル名から一意なバージョンラベルを列挙
                        var versionLabels = ExtractDistinctToolLabels(cacheFiles);
                        if (versionLabels.Length == 0)
                        {
                            Console.WriteLine("No recognizable tool version labels found in cache files.");
                            return 0;
                        }
                        Console.WriteLine();
                        Console.WriteLine("Available tool versions:");
                        for (int i = 0; i < versionLabels.Length; i++)
                        {
                            var matchCount = FilterCacheFilesByToolLabel(cacheFiles, versionLabels[i]).Length;
                            Console.WriteLine($"  {i + 1}. {versionLabels[i]} ({matchCount} file(s))");
                        }
                        Console.WriteLine($"  {versionLabels.Length + 1}. Cancel");
                        Console.WriteLine();
                        Console.Write("> ");
                        var versionChoice = Console.ReadLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(versionChoice)
                            || !int.TryParse(versionChoice, out int vIdx)
                            || vIdx < 1 || vIdx > versionLabels.Length)
                        {
                            Console.WriteLine("Cancelled.");
                            return 0;
                        }
                        var selectedLabel = versionLabels[vIdx - 1];
                        filesToDelete = FilterCacheFilesByToolLabel(cacheFiles, selectedLabel);
                        description = selectedLabel;
                        break;
                    default:
                        Console.WriteLine("Invalid option.");
                        return 0;
                }

                if (filesToDelete.Length == 0)
                {
                    Console.WriteLine($"No cache files found for: {description}");
                    return 0;
                }

                // Confirm deletion / 削除確認
                Console.Write($"Delete {filesToDelete.Length} {description} cache file(s)? [y/N]: ");
                var confirm = Console.ReadLine()?.Trim();
                if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Cancelled.");
                    return 0;
                }

                int deleted = 0;
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                    deleted++;
                }

                Console.WriteLine($"Cleared {deleted} IL cache file(s) from: {cacheDir}");
                return 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Failed to clear IL cache: {ex.Message}");
                return (int)ProgramExitCode.ExecutionFailed;
            }
        }

        /// <summary>
        /// Resolves the IL cache directory from config or defaults.
        /// 設定またはデフォルトから IL キャッシュディレクトリを解決します。
        /// </summary>
        private async Task<string> ResolveCacheDirectoryAsync(string? configPath)
        {
            try
            {
                var builder = await _configService.LoadConfigBuilderAsync(configPath);
                var config = builder.Build();
                return string.IsNullOrWhiteSpace(config.ILCacheDirectoryAbsolutePath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Common.Constants.APP_DATA_DIR_NAME, Common.Constants.DEFAULT_IL_CACHE_DIR_NAME)
                    : config.ILCacheDirectoryAbsolutePath;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException)
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Common.Constants.APP_DATA_DIR_NAME, Common.Constants.DEFAULT_IL_CACHE_DIR_NAME);
            }
        }

        // Cache file tool identification constants / キャッシュファイルのツール識別定数
        private const string CACHE_TOOL_ILDASM = "dotnet-ildasm";
        private const string CACHE_TOOL_ILSPY = "ilspycmd";
        // SHA256 hex is always 64 characters; tool label follows after the separator underscore
        // SHA256 の16進数は常に64文字; ツールラベルはセパレータのアンダースコアの後に続く
        private const int CACHE_KEY_HASH_LENGTH = 64;

        /// <summary>
        /// Filters cache file paths by tool name (e.g. "dotnet-ildasm" or "ilspycmd").
        /// Matches against the sanitized tool label portion of the filename (after the 64-char SHA256 hash + underscore).
        /// ツール名（例: "dotnet-ildasm"、"ilspycmd"）でキャッシュファイルパスをフィルタリングします。
        /// ファイル名の64文字SHA256ハッシュ+アンダースコア以降のサニタイズ済みツールラベル部分を照合します。
        /// </summary>
        internal static string[] FilterCacheFilesByTool(string[] cacheFiles, string toolName)
        {
            return Array.FindAll(cacheFiles, f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                // After 64-char hash + underscore separator, the tool label begins
                // 64文字ハッシュ + アンダースコアセパレータの後にツールラベルが始まる
                if (name.Length <= CACHE_KEY_HASH_LENGTH + 1) return false;
                var toolPart = name[(CACHE_KEY_HASH_LENGTH + 1)..];
                return toolPart.StartsWith(toolName, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Filters cache file paths by a specific tool version label (e.g. "dotnet-ildasm (version: 0.12.0)").
        /// The label is sanitized the same way as cache key construction (colons and parentheses → underscores).
        /// 特定のツールバージョンラベル（例: "dotnet-ildasm (version: 0.12.0)"）でキャッシュファイルパスをフィルタリングします。
        /// ラベルはキャッシュキー構築と同じ方法でサニタイズされます（コロンと括弧→アンダースコア）。
        /// </summary>
        internal static string[] FilterCacheFilesByToolLabel(string[] cacheFiles, string toolLabel)
        {
            // Sanitize the input label the same way TextSanitizer does for cache keys
            // キャッシュキーと同じ方法で入力ラベルをサニタイズ
            var sanitized = SanitizeForCacheMatch(toolLabel);
            return Array.FindAll(cacheFiles, f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (name.Length <= CACHE_KEY_HASH_LENGTH + 1) return false;
                var toolPart = name[(CACHE_KEY_HASH_LENGTH + 1)..];
                return toolPart.StartsWith(sanitized, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Extracts distinct tool version labels from cache filenames for interactive selection.
        /// E.g. ["dotnet-ildasm (version: 0.12.0)", "ilspycmd (version: 8.2.0)"].
        /// The label is reconstructed by reversing the colon sanitization ('_' → ':' in version pattern).
        /// キャッシュファイル名から一意なツールバージョンラベルを抽出し、対話的選択用に返します。
        /// ラベルはコロンサニタイズの逆変換（バージョンパターン内の '_' → ':'）で復元されます。
        /// </summary>
        internal static string[] ExtractDistinctToolLabels(string[] cacheFiles)
        {
            var labels = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in cacheFiles)
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (name.Length <= CACHE_KEY_HASH_LENGTH + 1) continue;
                var toolPart = name[(CACHE_KEY_HASH_LENGTH + 1)..];
                // Reverse sanitization: "(version_ X.Y.Z)" → "(version: X.Y.Z)"
                // サニタイズの逆変換: "(version_ X.Y.Z)" → "(version: X.Y.Z)"
                var label = UnsanitizeToolLabel(toolPart);
                labels.Add(label);
            }
            var result = new string[labels.Count];
            labels.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// Reverses the filename sanitization to reconstruct a human-readable tool label.
        /// Converts patterns like "dotnet-ildasm (version_ 0.12.0)" back to "dotnet-ildasm (version: 0.12.0)".
        /// Only ':' is sanitized to '_' by TextSanitizer.ToSafeFileName; parentheses are preserved.
        /// ファイル名サニタイズを逆変換し、人間が読めるツールラベルを復元します。
        /// TextSanitizer.ToSafeFileName では ':' のみ '_' に変換され、括弧はそのまま保持されます。
        /// </summary>
        internal static string UnsanitizeToolLabel(string sanitized)
        {
            // Common pattern: "toolname (version_ X.Y.Z)"
            // → "toolname (version: X.Y.Z)"
            // 共通パターン: "toolname (version_ X.Y.Z)"
            // → "toolname (version: X.Y.Z)"
            if (sanitized.Contains("(version_ "))
            {
                return sanitized.Replace("(version_ ", "(version: ");
            }
            // Fallback: return as-is if no version pattern found
            // フォールバック: バージョンパターンがない場合はそのまま返す
            return sanitized;
        }

        /// <summary>
        /// Sanitizes a tool label for matching against cache filenames.
        /// Replaces colons and characters invalid in filenames with underscores,
        /// matching the behavior of <see cref="FolderDiffIL4DotNet.Core.Text.TextSanitizer.ToSafeFileName"/>.
        /// キャッシュファイル名との照合用にツールラベルをサニタイズします。
        /// </summary>
        internal static string SanitizeForCacheMatch(string label)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(label.Length);
            foreach (var ch in label)
            {
                if (ch == ':' || Array.IndexOf(invalidChars, ch) >= 0)
                    sb.Append('_');
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Validates the configuration (JSON load + environment variable overrides + semantic validation) and reports results.
        /// 設定のバリデーション（JSON 読込 + 環境変数オーバーライド + セマンティック検証）を行い結果を報告します。
        /// </summary>
        private async Task<int> ValidateConfigAsync(string? configPath)
        {
            try
            {
                var builder = await _configService.LoadConfigBuilderAsync(configPath);
                var validationResult = builder.Validate();
                if (!validationResult.IsValid)
                {
                    Console.Error.WriteLine("Configuration validation failed:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.Error.WriteLine($"  - {error}");
                    }
                    return (int)ProgramExitCode.ConfigurationError;
                }

                // Build to verify immutable object construction succeeds
                // イミュータブルオブジェクトの構築が成功することを検証
                builder.Build();
                Console.WriteLine("Configuration is valid.");
                return 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return (int)ProgramExitCode.ConfigurationError;
            }
        }

        /// <summary>
        /// Prints the effective configuration (after JSON load + environment variable overrides) to stdout as JSON.
        /// 有効な設定（JSON 読込 + 環境変数オーバーライド適用後）を JSON として標準出力に書き出します。
        /// </summary>
        private async Task<int> PrintConfigAsync(string? configPath, CliOptions opts)
        {
            try
            {
                var builder = await _configService.LoadConfigBuilderAsync(configPath);
                ApplyCliOverrides(builder, opts);
                Console.WriteLine(JsonSerializer.Serialize(builder, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return (int)ProgramExitCode.ConfigurationError;
            }
        }

        /// <summary>
        /// Returns the configuration builder loading phase as a typed result.
        /// 設定ビルダー読込フェーズを型付き結果として返します。
        /// </summary>
        private async Task<StepResult<ConfigSettingsBuilder>> TryLoadConfigBuilderAsync(string? configPath)
        {
            try
            {
                var builder = await LoadConfigBuilderAsync(configPath);
                return StepResult<ConfigSettingsBuilder>.FromValue(builder);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return StepResult<ConfigSettingsBuilder>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
        }

        /// <summary>
        /// Validates and builds the immutable <see cref="ConfigSettings"/> from the builder.
        /// ビルダーを検証し、イミュータブルな <see cref="ConfigSettings"/> を構築します。
        /// </summary>
        private StepResult<ConfigSettings> TryBuildConfig(ConfigSettingsBuilder builder)
        {
            try
            {
                var validationResult = builder.Validate();
                if (!validationResult.IsValid)
                {
                    var details = string.Join(System.Environment.NewLine, validationResult.Errors);
                    throw new InvalidDataException($"{ConfigService.ERROR_CONFIG_VALIDATION_PREFIX}{System.Environment.NewLine}{details}");
                }

                return StepResult<ConfigSettings>.FromValue(builder.Build());
            }
            catch (Exception ex) when (ex is InvalidDataException)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
        }

        private async Task<ConfigSettingsBuilder> LoadConfigBuilderAsync(string? configPath)
        {
            _logger.LogMessage(AppLogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
            var builder = await _configService.LoadConfigBuilderAsync(configPath);
            _logger.LogMessage(AppLogLevel.Info, LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);
            _logger.CleanupOldLogFiles(builder.MaxLogGenerations);
            TimestampCache.Clear();
            _logger.LogMessage(AppLogLevel.Info, LOG_APP_STARTING, shouldOutputMessageToConsole: true);
            return builder;
        }

        /// <summary>
        /// Overrides <see cref="ConfigSettingsBuilder"/> values with CLI options, giving CLI flags priority over config.json.
        /// Delegates to <see cref="CliOverrideApplier"/> for the actual override logic.
        /// CLI オプションの値で <see cref="ConfigSettingsBuilder"/> を上書きします。config.json よりも CLI フラグを優先させます。
        /// 実際のオーバーライドロジックは <see cref="CliOverrideApplier"/> に委譲します。
        /// </summary>
        private static void ApplyCliOverrides(ConfigSettingsBuilder builder, CliOptions opts)
            => CliOverrideApplier.Apply(builder, opts);

        /// <summary>
        /// Easter egg message shown when multiple spinner theme flags (e.g. --coffee --beer) are
        /// specified simultaneously. Falls back to the matcha theme.
        /// Kept for backward compatibility with tests; canonical definition is in <see cref="SpinnerThemes"/>.
        /// 複数のスピナーテーマフラグ（例: --coffee --beer）が同時指定されたときに表示するイースターエッグメッセージ。
        /// テストとの後方互換のため保持; 正規定義は <see cref="SpinnerThemes"/> にあります。
        /// </summary>
        internal const string MULTIPLE_SPINNERS_MESSAGE = SpinnerThemes.MULTIPLE_SPINNERS_MESSAGE;
    }
}
