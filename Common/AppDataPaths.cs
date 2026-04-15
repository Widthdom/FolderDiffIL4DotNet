using System;
using System.IO;

namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// Resolves the application's user-local data paths for reports, logs, config, and IL cache.
    /// Tests can override the LocalApplicationData root via <see cref="AppContext.SetData(string, object?)"/>.
    /// レポート、ログ、設定、IL キャッシュ向けのユーザーローカルデータパスを解決します。
    /// テストでは <see cref="AppContext.SetData(string, object?)"/> で LocalApplicationData ルートを上書きできます。
    /// </summary>
    internal static class AppDataPaths
    {
        internal const string LOCAL_APP_DATA_OVERRIDE_KEY = "FolderDiffIL4DotNet.LocalApplicationDataOverride";

        private const string REPORTS_DIRECTORY_NAME = "Reports";
        private const string LOGS_DIRECTORY_NAME = "Logs";
        private const string CONFIG_FILE_NAME = "config.json";
        private const string ERROR_LOCAL_APP_DATA_UNRESOLVED = "LocalApplicationData could not be resolved.";

        /// <summary>
        /// Returns true when the exception represents this class's explicit LocalApplicationData fail-fast.
        /// このクラスが明示的に投げる LocalApplicationData fail-fast 例外であれば true を返します。
        /// </summary>
        internal static bool IsLocalApplicationDataResolutionFailure(Exception ex)
            => ex is InvalidOperationException invalidOperationException
                && string.Equals(invalidOperationException.Message, ERROR_LOCAL_APP_DATA_UNRESOLVED, StringComparison.Ordinal);

        /// <summary>Gets the OS user-local data root or the active test override. / OS 標準のユーザーローカルデータルート、または有効なテスト上書き値を返します。</summary>
        internal static string GetLocalApplicationDataRootAbsolutePath()
        {
            if (AppContext.GetData(LOCAL_APP_DATA_OVERRIDE_KEY) is string overridePath)
            {
                if (string.IsNullOrWhiteSpace(overridePath))
                {
                    throw new InvalidOperationException(ERROR_LOCAL_APP_DATA_UNRESOLVED);
                }

                return Path.GetFullPath(overridePath);
            }

            string localApplicationDataRoot = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);

            if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
            {
                throw new InvalidOperationException(ERROR_LOCAL_APP_DATA_UNRESOLVED);
            }

            return Path.GetFullPath(localApplicationDataRoot);
        }

        /// <summary>Gets the application root directory under user-local data. / ユーザーローカルデータ配下のアプリケーションルートディレクトリを返します。</summary>
        internal static string GetApplicationDataRootAbsolutePath()
            => Path.Combine(GetLocalApplicationDataRootAbsolutePath(), Constants.APP_DATA_DIR_NAME);

        /// <summary>Gets the default reports root directory. / 既定のレポートルートディレクトリを返します。</summary>
        internal static string GetDefaultReportsRootDirectoryAbsolutePath()
            => Path.Combine(GetApplicationDataRootAbsolutePath(), REPORTS_DIRECTORY_NAME);

        /// <summary>Gets the default logs directory. / 既定のログディレクトリを返します。</summary>
        internal static string GetDefaultLogsDirectoryAbsolutePath()
            => Path.Combine(GetApplicationDataRootAbsolutePath(), LOGS_DIRECTORY_NAME);

        /// <summary>Gets the default user config directory. / 既定のユーザー設定ディレクトリを返します。</summary>
        internal static string GetDefaultConfigDirectoryAbsolutePath()
            => GetApplicationDataRootAbsolutePath();

        /// <summary>Gets the default user config file path. / 既定のユーザー設定ファイルパスを返します。</summary>
        internal static string GetDefaultUserConfigFileAbsolutePath()
            => Path.Combine(GetDefaultConfigDirectoryAbsolutePath(), CONFIG_FILE_NAME);

        /// <summary>Gets the bundled fallback config file path next to the executable. / 実行ファイル隣にある同梱フォールバック設定ファイルパスを返します。</summary>
        internal static string GetBundledConfigFileAbsolutePath()
            => Path.Combine(AppContext.BaseDirectory, CONFIG_FILE_NAME);

        /// <summary>Gets the default IL cache directory. / 既定の IL キャッシュディレクトリを返します。</summary>
        internal static string GetDefaultIlCacheDirectoryAbsolutePath()
            => Path.Combine(GetApplicationDataRootAbsolutePath(), Constants.DEFAULT_IL_CACHE_DIR_NAME);
    }
}
