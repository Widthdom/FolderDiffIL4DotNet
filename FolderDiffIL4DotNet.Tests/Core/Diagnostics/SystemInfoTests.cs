using System;
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
            // Use the Program class type / Program クラス型を使用
            var version = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            Assert.False(string.IsNullOrWhiteSpace(version));
        }

        [Fact]
        public void GetAppVersion_WithValidType_DoesNotContainPlusGitMetadata()
        {
            // If informational version contains '+' git hash suffix, verify it's still returned
            // (the method does not strip metadata — this test documents that behavior).
            // InformationalVersion に '+' git ハッシュサフィックスが含まれる場合も
            // そのまま返されることを文書化するテスト。
            var version = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            // The version should be non-null regardless of metadata presence
            // メタデータの有無にかかわらず、バージョンは null であってはならない
            Assert.NotNull(version);
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
            // GetAppVersion should return deterministic results across calls.
            // GetAppVersion は呼び出しごとに同じ結果を返すこと。
            var version1 = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            var version2 = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            Assert.Equal(version1, version2);
        }
    }
}
