using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ReportGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;

        public ReportGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-report-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            ClearResultLists();
        }

        public void Dispose()
        {
            ClearResultLists();
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors in tests
            }
        }

        [Fact]
        public void GenerateDiffReport_HeaderAlwaysListsThreeDisassemblers()
        {
            // one observed version to verify "known version preferred, unknown tool falls back to tool name"
            FileDiffResultLists.DisassemblerToolVersions["dotnet-ildasm (version: dotnet ildasm 0.12.0)"] = 0;

            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            var reportDir = Path.Combine(_rootDir, "report");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            new FolderDiffIL4DotNet.Services.ReportGenerateService().GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("- IL Disassembler: dotnet-ildasm (version: dotnet ildasm 0.12.0), ildasm, ilspycmd", reportText);
        }

        [Fact]
        public void GenerateDiffReport_IlDiffDetailsIncludeDisassemblerLabel()
        {
            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            var reportDir = Path.Combine(_rootDir, "report");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            FileDiffResultLists.OldFilesAbsolutePath = new List<string> { Path.Combine(oldDir, "a.dll"), Path.Combine(oldDir, "b.dll") };
            FileDiffResultLists.NewFilesAbsolutePath = new List<string> { Path.Combine(newDir, "a.dll"), Path.Combine(newDir, "b.dll") };
            FileDiffResultLists.UnchangedFilesRelativePath = new List<string> { "a.dll" };
            FileDiffResultLists.ModifiedFilesRelativePath = new List<string> { "b.dll" };

            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: dotnet ildasm 0.12.0)");
            FileDiffResultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: dotnet ildasm 0.12.0)");

            var config = CreateConfig();
            new FolderDiffIL4DotNet.Services.ReportGenerateService().GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("`ILMatch`", reportText);
            Assert.Contains("`ILMismatch`", reportText);
            Assert.Contains("`dotnet-ildasm (version: dotnet ildasm 0.12.0)`", reportText);
        }

        private static ConfigSettings CreateConfig() => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false
        };

        private static void ClearResultLists()
        {
            FileDiffResultLists.OldFilesAbsolutePath = new List<string>();
            FileDiffResultLists.NewFilesAbsolutePath = new List<string>();
            FileDiffResultLists.UnchangedFilesRelativePath = new List<string>();
            FileDiffResultLists.AddedFilesAbsolutePath = new List<string>();
            FileDiffResultLists.RemovedFilesAbsolutePath = new List<string>();
            FileDiffResultLists.ModifiedFilesRelativePath = new List<string>();
            FileDiffResultLists.FileRelativePathToDiffDetailDictionary.Clear();
            FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.Clear();
            FileDiffResultLists.IgnoredFilesRelativePathToLocation.Clear();
            FileDiffResultLists.DisassemblerToolVersions.Clear();
            FileDiffResultLists.DisassemblerToolVersionsFromCache.Clear();
        }
    }
}
