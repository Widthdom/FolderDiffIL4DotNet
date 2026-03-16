using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// <see cref="DisassemblerHelper"/> の単体テスト。
    /// </summary>
    public sealed class DisassemblerHelperTests
    {
        // ── IsDotnetMuxer ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("dotnet")]
        [InlineData("DOTNET")]
        [InlineData("Dotnet")]
        public void IsDotnetMuxer_DotnetVariants_ReturnsTrue(string command)
        {
            Assert.True(DisassemblerHelper.IsDotnetMuxer(command));
        }

        [Theory]
        [InlineData("dotnet-ildasm")]
        [InlineData("ilspycmd")]
        [InlineData("ildasm")]
        [InlineData("")]
        public void IsDotnetMuxer_NonMuxer_ReturnsFalse(string command)
        {
            Assert.False(DisassemblerHelper.IsDotnetMuxer(command));
        }

        // ── IsIlspyCommand ────────────────────────────────────────────────────

        [Theory]
        [InlineData("ilspycmd")]
        [InlineData("ILSPYCMD")]
        [InlineData("/usr/local/bin/ilspycmd")]
        public void IsIlspyCommand_IlspyVariants_ReturnsTrue(string command)
        {
            Assert.True(DisassemblerHelper.IsIlspyCommand(command));
        }

        [Fact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void IsIlspyCommand_WindowsAbsolutePath_ReturnsTrue()
        {
            if (!OperatingSystem.IsWindows()) return;
            Assert.True(DisassemblerHelper.IsIlspyCommand("C:\\tools\\ilspycmd"));
        }

        [Theory]
        [InlineData("dotnet-ildasm")]
        [InlineData("dotnet")]
        [InlineData("ildasm")]
        public void IsIlspyCommand_NonIlspy_ReturnsFalse(string command)
        {
            Assert.False(DisassemblerHelper.IsIlspyCommand(command));
        }

        // ── CandidateDisassembleCommands ──────────────────────────────────────

        [Fact]
        public void CandidateDisassembleCommands_ContainsDotnetIldasm()
        {
            var candidates = DisassemblerHelper.CandidateDisassembleCommands().ToList();
            Assert.Contains(Constants.DOTNET_ILDASM, candidates);
        }

        [Fact]
        public void CandidateDisassembleCommands_ContainsDotnetMuxer()
        {
            var candidates = DisassemblerHelper.CandidateDisassembleCommands().ToList();
            Assert.Contains(Constants.DOTNET_MUXER, candidates);
        }

        [Fact]
        public void CandidateDisassembleCommands_ContainsIlspy()
        {
            var candidates = DisassemblerHelper.CandidateDisassembleCommands().ToList();
            Assert.Contains(Constants.ILSPY_CMD, candidates);
        }

        [Fact]
        public void CandidateDisassembleCommands_DotnetIldasmBeforeMuxer()
        {
            var candidates = DisassemblerHelper.CandidateDisassembleCommands().ToList();
            var ildasmIndex = candidates.IndexOf(Constants.DOTNET_ILDASM);
            var muxerIndex = candidates.IndexOf(Constants.DOTNET_MUXER);
            Assert.True(ildasmIndex >= 0, "dotnet-ildasm should be in the list");
            Assert.True(muxerIndex >= 0, "dotnet muxer should be in the list");
            Assert.True(ildasmIndex < muxerIndex, "dotnet-ildasm should appear before dotnet muxer");
        }

        // ── UserDotnetToolsDirectory ──────────────────────────────────────────

        [Fact]
        public void UserDotnetToolsDirectory_ContainsDotnetAndTools()
        {
            var dir = DisassemblerHelper.UserDotnetToolsDirectory;
            Assert.Contains(".dotnet", dir);
            Assert.Contains("tools", dir);
        }

        // ── ResolveExecutablePath ─────────────────────────────────────────────

        [Fact]
        public void ResolveExecutablePath_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(DisassemblerHelper.ResolveExecutablePath(null));
            Assert.Null(DisassemblerHelper.ResolveExecutablePath(""));
            Assert.Null(DisassemblerHelper.ResolveExecutablePath("   "));
        }

        [Fact]
        public void ResolveExecutablePath_RootedNonExistentPath_ReturnsNull()
        {
            var nonExistent = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.exe");
            Assert.Null(DisassemblerHelper.ResolveExecutablePath(nonExistent));
        }

        [Fact]
        public void ResolveExecutablePath_RootedExistingPath_ReturnsFullPath()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test-resolve-{Guid.NewGuid():N}.dll");
            try
            {
                File.WriteAllText(tempFile, "dummy");
                var resolved = DisassemblerHelper.ResolveExecutablePath(tempFile);
                Assert.NotNull(resolved);
                Assert.True(File.Exists(resolved));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        // ── EnumerateExecutableNames ──────────────────────────────────────────

        [Fact]
        public void EnumerateExecutableNames_AlwaysContainsOriginalName()
        {
            var names = DisassemblerHelper.EnumerateExecutableNames("mytool").ToList();
            Assert.Contains("mytool", names);
        }

        [Fact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void EnumerateExecutableNames_OnWindows_ContainsExeVariant()
        {
            if (!OperatingSystem.IsWindows()) return;
            var names = DisassemblerHelper.EnumerateExecutableNames("mytool").ToList();
            Assert.Contains("mytool.exe", names, StringComparer.OrdinalIgnoreCase);
        }
    }
}
