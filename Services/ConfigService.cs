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
        /// <summary>
        /// config.jsonファイルから設定情報を非同期で読み込みます。
        /// このメソッドは、アプリケーションのベースディレクトリにあるJSONファイルを読み取り、
        /// その内容をConfigSettingsオブジェクトにデシリアライズして返します。
        /// </summary>
        /// <returns>設定データを含むConfigSettingsオブジェクト</returns>
        /// <exception cref="FileNotFoundException">config.jsonファイルが指定された場所に存在しない場合にスローされます。</exception>
        /// <exception cref="InvalidDataException">config.jsonファイルが無効なJSON形式のため解析できない場合にスローされます。</exception>
        /// <exception cref="IOException">config.jsonファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public async Task<ConfigSettings> LoadConfigAsync()
        {
            try
            {
                string configFileAbsolutePath = Path.Combine(AppContext.BaseDirectory, Constants.CONFIG_FILE_NAME);
                if (!File.Exists(configFileAbsolutePath))
                {
                    throw new FileNotFoundException($"Config file not found: {configFileAbsolutePath}");
                }

                string json = await File.ReadAllTextAsync(configFileAbsolutePath);
                return JsonSerializer.Deserialize<ConfigSettings>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Failed to parse the config file.", ex);
            }
        }
    }
}
