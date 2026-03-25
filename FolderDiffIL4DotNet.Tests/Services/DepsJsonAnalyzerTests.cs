using System;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="DepsJsonAnalyzer"/>.
    /// <see cref="DepsJsonAnalyzer"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed partial class DepsJsonAnalyzerTests : IDisposable
    {
        private readonly string _tempDir;

        public DepsJsonAnalyzerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "fd-depsjson-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup / ベストエフォートクリーンアップ */ }
        }

        [Fact]
        public void Analyze_DetectsAddedPackage()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{},""PackageB/2.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal("PackageB", result.Entries[0].PackageName);
            Assert.Equal("Added", result.Entries[0].Change);
            Assert.Equal("", result.Entries[0].OldVersion);
            Assert.Equal("2.0.0", result.Entries[0].NewVersion);
        }

        [Fact]
        public void Analyze_DetectsRemovedPackage()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{},""PackageB/2.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(1, result.RemovedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal("PackageB", result.Entries[0].PackageName);
            Assert.Equal("Removed", result.Entries[0].Change);
        }

        [Fact]
        public void Analyze_DetectsUpdatedPackage()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""Serilog/3.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""Serilog/4.1.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(1, result.UpdatedCount);
            var entry = result.Entries[0];
            Assert.Equal("Updated", entry.Change);
            Assert.Equal("Serilog", entry.PackageName);
            Assert.Equal("3.0.0", entry.OldVersion);
            Assert.Equal("4.1.0", entry.NewVersion);
        }

        [Fact]
        public void Analyze_DetectsMultipleChanges()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""A/1.0.0"":{},""B/2.0.0"":{},""C/3.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""A/1.1.0"":{},""C/3.0.0"":{},""D/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(1, result.AddedCount);   // D added
            Assert.Equal(1, result.RemovedCount);  // B removed
            Assert.Equal(1, result.UpdatedCount);  // A updated
        }

        [Fact]
        public void Analyze_NoChanges_ReturnsEmptySummary()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.False(result!.HasChanges);
            Assert.Empty(result.Entries);
        }

        [Fact]
        public void Analyze_EmptyLibraries_ReturnsEmptySummary()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.False(result!.HasChanges);
        }

        [Fact]
        public void Analyze_NoLibrariesProperty_ReturnsEmptySummary()
        {
            var oldFile = CreateDepsJson("old.deps.json", @"{""runtimeTarget"":{""name"":"".NETCoreApp,Version=v8.0""}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""runtimeTarget"":{""name"":"".NETCoreApp,Version=v8.0""}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.False(result!.HasChanges);
        }

        [Fact]
        public void Analyze_InvalidJson_ReturnsNull()
        {
            var oldFile = CreateDepsJson("old.deps.json", "not valid json");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""A/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.Null(result);
        }

        [Fact]
        public void Analyze_MissingFile_ReturnsNull()
        {
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""A/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(Path.Combine(_tempDir, "nonexistent.deps.json"), newFile);

            Assert.Null(result);
        }

        [Fact]
        public void ClassifyImportance_Removed_IsHigh()
        {
            var entry = new DependencyChangeEntry("Removed", "PackageA", "1.0.0", "");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_Added_IsMedium()
        {
            var entry = new DependencyChangeEntry("Added", "PackageA", "", "1.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Medium, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MajorVersionUpdate_IsHigh()
        {
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.0.0", "2.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MinorVersionUpdate_IsMedium()
        {
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.0.0", "1.1.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Medium, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_PatchVersionUpdate_IsLow()
        {
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.0.0", "1.0.1");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_PreReleaseVersion_ParsedCorrectly()
        {
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.0.0-preview.1", "2.0.0-rc.1");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void Analyze_CaseInsensitivePackageNames()
        {
            // .deps.json library keys are case-insensitive / .deps.json のライブラリキーは大文字小文字を区別しない
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""MyPackage/1.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""mypackage/2.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            // Should detect as Updated, not Added+Removed / Updated として検出されるべき
            Assert.Equal(1, result!.UpdatedCount);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
        }

        [Fact]
        public void Analyze_SkipsInvalidLibraryKeys()
        {
            // Library keys without "/" should be skipped / "/" のないライブラリキーはスキップされる
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""InvalidKey"":{},""Good/1.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""Good/2.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.Equal(1, result!.UpdatedCount);
        }

        [Fact]
        public void DependencyChangeSummary_MaxImportance_ReturnsHighest()
        {
            var summary = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Updated", "A", "1.0.0", "1.0.1", ChangeImportance.Low),
                    new DependencyChangeEntry("Removed", "B", "2.0.0", "", ChangeImportance.High),
                    new DependencyChangeEntry("Added", "C", "", "1.0.0", ChangeImportance.Medium),
                }
            };

            Assert.Equal(ChangeImportance.High, summary.MaxImportance);
            Assert.Equal(1, summary.HighImportanceCount);
            Assert.Equal(1, summary.MediumImportanceCount);
            Assert.Equal(1, summary.LowImportanceCount);
        }

        [Fact]
        public void DependencyChangeSummary_EntriesByImportance_SortedCorrectly()
        {
            var summary = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Updated", "Z", "1.0.0", "1.0.1", ChangeImportance.Low),
                    new DependencyChangeEntry("Removed", "A", "2.0.0", "", ChangeImportance.High),
                    new DependencyChangeEntry("Added", "B", "", "1.0.0", ChangeImportance.Medium),
                }
            };

            var sorted = summary.EntriesByImportance;

            // Added first, then Removed, then Updated / Added → Removed → Updated の順
            Assert.Equal("Added", sorted[0].Change);
            Assert.Equal("Removed", sorted[1].Change);
            Assert.Equal("Updated", sorted[2].Change);
        }

        [Fact]
        public void ExtractLibraryVersions_RealisticDepsJson()
        {
            // Realistic .deps.json structure with runtime/compile targets
            // 実際の .deps.json 構造（runtime/compile ターゲット付き）
            var file = CreateDepsJson("realistic.deps.json", @"{
  ""runtimeTarget"": {
    ""name"": "".NETCoreApp,Version=v8.0""
  },
  ""libraries"": {
    ""Microsoft.Extensions.Logging/9.0.0"": {
      ""type"": ""package"",
      ""serviceable"": true,
      ""sha512"": ""sha512-abc123""
    },
    ""Serilog/4.1.0"": {
      ""type"": ""package"",
      ""serviceable"": true,
      ""sha512"": ""sha512-def456""
    },
    ""MyApp/1.0.0"": {
      ""type"": ""project""
    }
  }
}");

            var versions = DepsJsonAnalyzer.ExtractLibraryVersions(file);

            Assert.Equal(3, versions.Count);
            Assert.Equal("9.0.0", versions["Microsoft.Extensions.Logging"]);
            Assert.Equal("4.1.0", versions["Serilog"]);
            Assert.Equal("1.0.0", versions["MyApp"]);
        }

        private string CreateDepsJson(string fileName, string content)
        {
            var path = Path.Combine(_tempDir, fileName);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
