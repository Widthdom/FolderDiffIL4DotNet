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
        /// Config解析失敗
        /// </summary>
        private const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse the config file.";

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
                throw new InvalidDataException(ERROR_CONFIG_PARSE_FAILED, ex);
            }
        }
    }
}
