using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// config.jsonの読み込みと設定の提供を行うサービス
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>
        /// 設定ファイル名
        /// </summary>
        private const string CONFIG_FILE_NAME = "config.json";

        /// <summary>
        /// Config 解析失敗（JSON 書式エラー）のメッセージプレフィックス
        /// </summary>
        private const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse config.json — JSON syntax error";

        /// <summary>
        /// JSON 書式ミス（トレイリングカンマ等）に対するヒント文言
        /// </summary>
        private const string ERROR_CONFIG_PARSE_HINT =
            " Hint: standard JSON does not allow trailing commas after the last property or array element" +
            " (e.g. remove the comma in \"Key\": \"value\",}).";

        /// <summary>
        /// Configバリデーション失敗のメッセージプレフィックス
        /// </summary>
        internal const string ERROR_CONFIG_VALIDATION_PREFIX = "config.json contains invalid settings:";

        /// <summary>
        /// config.jsonファイルから設定情報を非同期で読み込みます。
        /// このメソッドは、指定されたパス（または既定のアプリケーションベースディレクトリ）にある
        /// JSONファイルを読み取り、その内容を<see cref="ConfigSettings"/>オブジェクトにデシリアライズして返します。
        /// デシリアライズ後に設定値の整合性を検証します。
        /// </summary>
        /// <param name="configFilePath">
        /// 読み込む config.json の絶対パス。null または空文字列の場合は、アプリケーション実行ディレクトリ直下の
        /// config.json を使用します。
        /// </param>
        /// <returns>設定データを含む<see cref="ConfigSettings"/>オブジェクト</returns>
        /// <exception cref="FileNotFoundException">config.jsonファイルが指定された場所に存在しない場合にスローされます。</exception>
        /// <exception cref="InvalidDataException">config.jsonファイルが無効なJSON形式のため解析できない場合、または設定値が不正な場合にスローされます。</exception>
        /// <exception cref="IOException">config.jsonファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public async Task<ConfigSettings> LoadConfigAsync(string configFilePath = null)
        {
            try
            {
                string configFileAbsolutePath = string.IsNullOrWhiteSpace(configFilePath)
                    ? Path.Combine(AppContext.BaseDirectory, CONFIG_FILE_NAME)
                    : configFilePath;
                if (!File.Exists(configFileAbsolutePath))
                {
                    throw new FileNotFoundException($"Config file not found: {configFileAbsolutePath}");
                }

                string json = await File.ReadAllTextAsync(configFileAbsolutePath);
                var config = JsonSerializer.Deserialize<ConfigSettings>(json)
                    ?? throw new InvalidDataException(ERROR_CONFIG_PARSE_FAILED);

                var validationResult = config.Validate();
                if (!validationResult.IsValid)
                {
                    var details = string.Join(Environment.NewLine, validationResult.Errors);
                    throw new InvalidDataException($"{ERROR_CONFIG_VALIDATION_PREFIX}{Environment.NewLine}{details}");
                }

                return config;
            }
            catch (JsonException ex)
            {
                // 行番号・バイト位置を付与し、トレイリングカンマ等の典型的なミスへのヒントを添える。
                // Include line/position from JsonException and append a trailing-comma hint.
                var location = ex.LineNumber.HasValue
                    ? $" (line {ex.LineNumber.Value + 1}, position {(ex.BytePositionInLine ?? 0) + 1})"
                    : string.Empty;
                throw new InvalidDataException(
                    $"{ERROR_CONFIG_PARSE_FAILED}{location}: {ex.Message}{ERROR_CONFIG_PARSE_HINT}", ex);
            }
        }
    }
}
