using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FolderDiffIL4DotNet.Core.Diagnostics
{
    /// <summary>
    /// Provides system information and application metadata retrieval.
    /// システム情報およびアプリケーションメタデータの取得を提供するクラス。
    /// </summary>
    public static class SystemInfo
    {
        private const string UNKNOWN_COMPUTER_NAME = "Unknown Computer";
        private const string ERROR_VERSION_STRING_EMPTY = "Version string is empty.";
        /// <summary>
        /// Retrieves the computer name on a best-effort basis; falls back to <see cref="UNKNOWN_COMPUTER_NAME"/>.
        /// 実行中のコンピュータ名をベストエフォートで取得します。
        /// </summary>
        public static string GetComputerName()
        {
            var machineName = TryGetEnvironmentMachineName();
            if (!string.IsNullOrWhiteSpace(machineName))
            {
                return machineName;
            }

            var hostName = TryGetDnsHostName();
            if (!string.IsNullOrWhiteSpace(hostName))
            {
                return hostName;
            }

            var envHost = Environment.GetEnvironmentVariable("HOSTNAME");
            if (!string.IsNullOrWhiteSpace(envHost))
            {
                return envHost;
            }

            var envComputer = Environment.GetEnvironmentVariable("COMPUTERNAME");
            if (!string.IsNullOrWhiteSpace(envComputer))
            {
                return envComputer;
            }

            return UNKNOWN_COMPUTER_NAME;
        }

        /// <summary>
        /// Returns the public three-part SemVer for the assembly containing the given type.
        /// 指定した型を含むアセンブリの公開用 3 要素 SemVer を返します。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="programType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="programType"/> が <see langword="null"/> の場合。</exception>
        /// <exception cref="InvalidOperationException">No usable public version is available.</exception>
        /// <exception cref="InvalidOperationException">公開用バージョンを取得できない場合。</exception>
        public static string GetAppVersion(Type programType)
        {
            ArgumentNullException.ThrowIfNull(programType);

            var assembly = programType.Assembly;
            var fileVersionAttribute = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>(assembly);
            if (Version.TryParse(fileVersionAttribute?.Version, out var fileVersion) && fileVersion.Build >= 0)
            {
                return $"{fileVersion.Major}.{fileVersion.Minor}.{fileVersion.Build}";
            }

            var fallbackVersion = assembly.GetName().Version;
            if (fallbackVersion == null || fallbackVersion.Build < 0)
            {
                throw new InvalidOperationException(ERROR_VERSION_STRING_EMPTY);
            }

            return $"{fallbackVersion.Major}.{fallbackVersion.Minor}.{fallbackVersion.Build}";
        }

        /// <summary>
        /// Returns the detailed build version for diagnostics, including commit metadata when available.
        /// コミットメタデータを利用できる場合はそれを含む、診断用の詳細ビルドバージョンを返します。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="programType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="programType"/> が <see langword="null"/> の場合。</exception>
        /// <exception cref="InvalidOperationException">No diagnostic version is available.</exception>
        /// <exception cref="InvalidOperationException">診断用バージョンを取得できない場合。</exception>
        public static string GetDiagnosticAppVersion(Type programType)
        {
            ArgumentNullException.ThrowIfNull(programType);

            var assembly = programType.Assembly;
            var infoAttr = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly);
            var infoVer = infoAttr?.InformationalVersion;
            var fileVer = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            var verToShow = string.IsNullOrWhiteSpace(infoVer) ? fileVer : infoVer;
            if (string.IsNullOrWhiteSpace(verToShow))
            {
                throw new InvalidOperationException(ERROR_VERSION_STRING_EMPTY);
            }

            return verToShow;
        }

        private static string? TryGetEnvironmentMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static string? TryGetDnsHostName()
        {
            try
            {
                return Dns.GetHostName();
            }
            catch (SocketException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
