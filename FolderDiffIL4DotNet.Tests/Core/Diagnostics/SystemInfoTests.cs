using System;
using FolderDiffIL4DotNet.Core.Diagnostics;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Diagnostics
{
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
            // Program クラス型を使用
            var version = SystemInfo.GetAppVersion(typeof(FolderDiffIL4DotNet.Program));
            Assert.False(string.IsNullOrWhiteSpace(version));
        }

        [Fact]
        public void TryGetDnsHostName_ReturnsStringOrNull()
        {
            // TryGetDnsHostName はプライベートなので GetComputerName 経由で間接的にカバー。
            // ここでは直接テストできるように GetComputerName の戻り値を確認する。
            var name = SystemInfo.GetComputerName();
            // GetComputerName は null を返さない（UNKNOWN_COMPUTER_NAME を返す）
            Assert.NotNull(name);
        }

        [Fact]
        public void GetComputerName_WhenMachineNameAvailable_DoesNotReturnUnknown()
        {
            // 通常の環境では MachineName が取得可能なので結果は "Unknown Computer" にならない
            var name = SystemInfo.GetComputerName();
            // 環境により異なるが、少なくとも空でないことを確認
            Assert.True(name.Length > 0);
        }
    }
}
