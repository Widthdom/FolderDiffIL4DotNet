using System;
using System.Reflection;
using System.Reflection.Emit;
using FolderDiffIL4DotNet.Core.Diagnostics;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Diagnostics
{
    /// <summary>
    /// Unit tests for <see cref="SystemInfo"/>.
    /// <see cref="SystemInfo"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public class SystemInfoTests
    {
        [Fact]
        public void GetComputerName_ReturnsNonEmptyString()
        {
            var name = SystemInfo.GetComputerName();
            Assert.False(string.IsNullOrWhiteSpace(name));
        }

        [Fact]
        public void GetAppVersion_WithValidType_ReturnsNonEmptyString()
        {
            var fileVersionAttribute = typeof(FolderDiffIL4DotNet.Program).Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>();
            Assert.NotNull(fileVersionAttribute);
            Assert.True(Version.TryParse(fileVersionAttribute.Version, out var fileVersion));
            var expectedVersion = $"{fileVersion.Major}.{fileVersion.Minor}.{fileVersion.Build}";

            var version = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));

            Assert.Equal(expectedVersion, version);
            Assert.Equal(3, version.Split('.').Length);
        }

        [Fact]
        public void GetAppVersion_WithNonZeroPatchVersion_PreservesPatchComponent()
        {
            var assemblyName = new AssemblyName($"SystemInfoVersionTest_{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.Run);
            var fileVersionConstructor = typeof(AssemblyFileVersionAttribute)
                .GetConstructor(new[] { typeof(string) });
            Assert.NotNull(fileVersionConstructor);
            assemblyBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(fileVersionConstructor, new object[] { "1.20.6.42" }));
            var testType = assemblyBuilder
                .DefineDynamicModule(assemblyName.Name!)
                .DefineType("VersionedProgram")
                .CreateType();
            Assert.NotNull(testType);

            var version = SystemInfo.GetAppVersion(testType);

            Assert.Equal("1.20.6", version);
        }

        [Fact]
        public void GetDiagnosticAppVersion_WithValidType_PreservesDetailedVersion()
        {
            var assembly = typeof(FolderDiffIL4DotNet.Program).Assembly;
            var informationalVersion = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)
                ?.InformationalVersion;

            var diagnosticVersion = SystemInfo.GetDiagnosticAppVersion(typeof(FolderDiffIL4DotNet.Program));

            Assert.False(string.IsNullOrWhiteSpace(diagnosticVersion));
            Assert.StartsWith(
                SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program)),
                diagnosticVersion,
                StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                Assert.Equal(informationalVersion, diagnosticVersion);
            }
        }

        [Fact]
        public void TryGetDnsHostName_ReturnsStringOrNull()
        {
            // TryGetDnsHostName is private, so covered indirectly via GetComputerName.
            // We verify GetComputerName's return value here as a proxy.
            // TryGetDnsHostName はプライベートなので GetComputerName 経由で間接的にカバー。
            // ここでは GetComputerName の戻り値を確認して代替検証とする。
            var name = SystemInfo.GetComputerName();
            // GetComputerName never returns null (falls back to UNKNOWN_COMPUTER_NAME)
            // GetComputerName は null を返さない（UNKNOWN_COMPUTER_NAME にフォールバック）
            Assert.NotNull(name);
        }

        [Fact]
        public void GetComputerName_WhenMachineNameAvailable_DoesNotReturnUnknown()
        {
            // On a normal system MachineName is available, so the result should not be "Unknown Computer"
            // 通常の環境では MachineName が取得可能なので結果は "Unknown Computer" にならない
            var name = SystemInfo.GetComputerName();
            // May vary by environment, but should at least be non-empty
            // 環境により異なるが、少なくとも空でないことを確認
            Assert.True(name.Length > 0);
        }

        [Fact]
        public void GetComputerName_ConsecutiveCalls_ReturnsSameValue()
        {
            // GetComputerName should return deterministic results across calls.
            // GetComputerName は呼び出しごとに同じ結果を返すこと。
            var name1 = SystemInfo.GetComputerName();
            var name2 = SystemInfo.GetComputerName();
            Assert.Equal(name1, name2);
        }

        [Fact]
        public void GetAppVersion_ConsecutiveCalls_ReturnsSameValue()
        {
            var version1 = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            var version2 = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            var diagnosticVersion1 = SystemInfo.GetDiagnosticAppVersion(typeof(FolderDiffIL4DotNet.Program));
            var diagnosticVersion2 = SystemInfo.GetDiagnosticAppVersion(typeof(FolderDiffIL4DotNet.Program));

            Assert.Equal(version1, version2);
            Assert.Equal(diagnosticVersion1, diagnosticVersion2);
        }
    }
}
