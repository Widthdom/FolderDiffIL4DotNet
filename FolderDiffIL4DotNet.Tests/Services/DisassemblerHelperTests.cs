using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class DisassemblerHelperTests
    {
        // ── IsDotnetMuxer ──────────────────────────────────────────────────────

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

        [Fact]
        public void ResolveExecutablePath_RelativePathWithSeparator_NonExistent_ReturnsNull()
        {
            // Relative path with directory separator but non-existent file should return null
            // ディレクトリ区切り文字を含む相対パスでファイルが存在しない場合は null を返す
            var relPath = Path.Combine("nonexistent_subdir_xyz", "nonexistent_tool_abc");
            Assert.Null(DisassemblerHelper.ResolveExecutablePath(relPath));
        }

        [Fact]
        public void ResolveExecutablePath_RelativePathWithSeparator_Existing_ReturnsFullPath()
        {
            // Relative path with directory separator and existing file should return an absolute path
            // ディレクトリ区切り文字を含む相対パスでファイルが存在する場合は絶対パスを返す
            var tempDir = Path.Combine(Path.GetTempPath(), $"fdi4dn-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var fileName = $"tool-{Guid.NewGuid():N}";
            var fullPath = Path.Combine(tempDir, fileName);
            File.WriteAllText(fullPath, "dummy");
            try
            {
                var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath);
                // Skip if relative path contains no separator (rare, e.g. same directory)
                // 相対パスにセパレータが含まれない場合（稀）はスキップ
                if (!relativePath.Contains(Path.DirectorySeparatorChar)
                    && !relativePath.Contains(Path.AltDirectorySeparatorChar))
                {
                    return;
                }
                var result = DisassemblerHelper.ResolveExecutablePath(relativePath);
                Assert.NotNull(result);
                Assert.True(File.Exists(result));
            }
            finally
            {
                File.Delete(fullPath);
                Directory.Delete(tempDir);
            }
        }

        [Fact]
        public void ResolveExecutablePath_WhitespacePathVariable_ReturnsNull()
        {
            // When PATH contains only whitespace, should return null without searching
            // PATH 環境変数が空白のみの場合、検索せずに null を返す
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                Environment.SetEnvironmentVariable("PATH", "   ");
                Assert.Null(DisassemblerHelper.ResolveExecutablePath("some-nonexistent-tool-xyz"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }

        [Fact]
        public void ResolveExecutablePath_PathWithEmptyEntries_SkipsEmptyAndReturnsNull()
        {
            // Empty entries in PATH should be skipped; returns null when command is not found
            // PATH 内の空エントリをスキップし、コマンドが見つからなければ null を返す
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            var sep = Path.PathSeparator.ToString();
            try
            {
                Environment.SetEnvironmentVariable("PATH", $"{sep}{sep}/nonexistent-dir-xyz{sep}");
                Assert.Null(DisassemblerHelper.ResolveExecutablePath("definitely-nonexistent-tool-xyz123"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }

        [Fact]
        public void ResolveExecutablePath_CommandFoundInPath_ReturnsAbsolutePath()
        {
            // When a matching file exists in a PATH directory, should return its absolute path
            // PATH ディレクトリ内にコマンド名と一致するファイルがある場合、絶対パスを返す
            var tempDir = Path.Combine(Path.GetTempPath(), $"fdi4dn-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var commandName = $"test-cmd-{Guid.NewGuid():N}";
            var commandFile = Path.Combine(tempDir, commandName);
            File.WriteAllText(commandFile, "dummy");
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + originalPath);
                var result = DisassemblerHelper.ResolveExecutablePath(commandName);
                Assert.NotNull(result);
                Assert.True(File.Exists(result));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                File.Delete(commandFile);
                Directory.Delete(tempDir);
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

        [Fact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void EnumerateExecutableNames_OnWindows_CommandWithExeSuffix_DoesNotDuplicateExe()
        {
            if (!OperatingSystem.IsWindows()) return;
            var names = DisassemblerHelper.EnumerateExecutableNames("mytool.exe").ToList();
            Assert.Equal(1, names.Count(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void EnumerateExecutableNames_OnWindows_CommandWithCmdSuffix_DoesNotDuplicateCmd()
        {
            if (!OperatingSystem.IsWindows()) return;
            var names = DisassemblerHelper.EnumerateExecutableNames("mytool.cmd").ToList();
            Assert.Equal(1, names.Count(n => n.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void EnumerateExecutableNames_OnWindows_CommandWithBatSuffix_DoesNotDuplicateBat()
        {
            if (!OperatingSystem.IsWindows()) return;
            var names = DisassemblerHelper.EnumerateExecutableNames("mytool.bat").ToList();
            Assert.Equal(1, names.Count(n => n.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)));
        }
        // ── ProbeAllCandidates / 全候補プローブ ────────────────────────────────

        /// <summary>
        /// Verifies that ProbeAllCandidates returns a non-empty list with unique tool names.
        /// ProbeAllCandidates が一意のツール名を持つ空でないリストを返すことを確認する。
        /// </summary>
        [Fact]
        public void ProbeAllCandidates_ReturnsNonEmptyList_WithUniqueToolNames()
        {
            var results = DisassemblerHelper.ProbeAllCandidates();

            Assert.NotNull(results);
            Assert.NotEmpty(results);

            var toolNames = results.Select(r => r.ToolName).ToList();
            Assert.Equal(toolNames.Count, toolNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        /// <summary>
        /// Verifies that ProbeAllCandidates includes both dotnet-ildasm and ilspycmd candidates.
        /// ProbeAllCandidates が dotnet-ildasm と ilspycmd の両方を含むことを確認する。
        /// </summary>
        [Fact]
        public void ProbeAllCandidates_IncludesExpectedToolNames()
        {
            var results = DisassemblerHelper.ProbeAllCandidates();

            var toolNames = results.Select(r => r.ToolName).ToList();
            Assert.Contains(FolderDiffIL4DotNet.Common.Constants.DOTNET_ILDASM, toolNames);
            Assert.Contains(FolderDiffIL4DotNet.Common.Constants.ILSPY_CMD, toolNames);
        }

        /// <summary>
        /// Verifies that each ProbeAllCandidates result has a non-empty ToolName.
        /// 各プローブ結果が空でない ToolName を持つことを確認する。
        /// </summary>
        [Fact]
        public void ProbeAllCandidates_AllResultsHaveNonEmptyToolName()
        {
            var results = DisassemblerHelper.ProbeAllCandidates();

            foreach (var result in results)
            {
                Assert.False(string.IsNullOrWhiteSpace(result.ToolName),
                    $"ToolName should not be null or whitespace, got: '{result.ToolName}'");
            }
        }
    }
}
