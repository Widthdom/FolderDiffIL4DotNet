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
        /// Returns the user-facing version string for the assembly containing the given type.
        /// 実行アセンブリの表示用バージョン文字列を取得します。
        /// </summary>
        /// <exception cref="InvalidOperationException">どのバージョン情報も取得できなかった場合。</exception>
        public static string GetAppVersion(Type programType)
        {
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
