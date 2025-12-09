using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// config.jsonの読み込みと設定の提供を行うサービス
    /// </summary>
    public sealed class ConfigService
    {
        #region constants
        /// <summary>
        /// 設定ファイル名
        /// </summary>
        private const string CONFIG_FILE_NAME = "config.json";

        /// <summary>
        /// Configファイル無し
        /// </summary>
        private const string ERROR_CONFIG_NOT_FOUND = "Config file not found: {0}";

        /// <summary>
        /// Config解析失敗
        /// </summary>
        private const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse the config file.";
        #endregion

        /// <summary>
        /// config.jsonファイルから設定情報を非同期で読み込みます。
        /// このメソッドは、アプリケーションのベースディレクトリにあるJSONファイルを読み取り、
        /// その内容を<see cref="ConfigSettings"/>オブジェクトにデシリアライズして返します。
        /// </summary>
        /// <returns>設定データを含む<see cref="ConfigSettings"/>オブジェクト</returns>
        /// <exception cref="FileNotFoundException">config.jsonファイルが指定された場所に存在しない場合にスローされます。</exception>
        /// <exception cref="InvalidDataException">config.jsonファイルが無効なJSON形式のため解析できない場合にスローされます。</exception>
        /// <exception cref="IOException">config.jsonファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public async Task<ConfigSettings> LoadConfigAsync()
        {
            try
            {
                string configFileAbsolutePath = Path.Combine(AppContext.BaseDirectory, CONFIG_FILE_NAME);
                if (!File.Exists(configFileAbsolutePath))
                {
                    throw new FileNotFoundException(string.Format(ERROR_CONFIG_NOT_FOUND, configFileAbsolutePath));
                }

                string json = await File.ReadAllTextAsync(configFileAbsolutePath);
                return JsonSerializer.Deserialize<ConfigSettings>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(ERROR_CONFIG_PARSE_FAILED, ex);
            }
        }
    }
}
