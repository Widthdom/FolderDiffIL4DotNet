using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for SbomGenerateService with special characters and edge case inputs.
    /// 特殊文字やエッジケース入力に対する SbomGenerateService のテスト。
    /// </summary>
    public sealed partial class SbomGenerateServiceTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateSbom_UnicodeFilePath_ProducesValidJson()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("unicode-path");

            // Add a file with Japanese characters / 日本語文字を含むファイルを追加
            _resultLists.AddModifiedFileRelativePath("日本語フォルダ/テスト.dll");
            _resultLists.RecordDiffDetail("日本語フォルダ/テスト.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));

            // Verify JSON is valid and parseable / JSON が有効で解析可能であることを検証
            var json = File.ReadAllText(sbomPath);
            var doc = JsonDocument.Parse(json);
            Assert.Contains("テスト.dll", json);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateSbom_FilePathWithQuotes_ProducesValidJson()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("quotes-path");

            // File path containing characters that need JSON escaping
            // JSON エスケープが必要な文字を含むファイルパス
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "file\"with\"quotes.config"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));

            // JSON must still be valid even with quotes in file path
            // ファイルパスにクォートがあっても JSON は有効でなければならない
            var json = File.ReadAllText(sbomPath);
            var doc = JsonDocument.Parse(json); // Would throw if invalid JSON / 無効な JSON なら例外
            Assert.NotNull(doc);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateSbom_EmptyResultLists_ProducesValidJsonWithEmptyComponents()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("empty-full");

            // No files added — empty result lists / ファイル追加なし — 空の結果リスト
            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));

            var json = File.ReadAllText(sbomPath);
            var doc = JsonDocument.Parse(json);

            // Components array should exist and be empty / コンポーネント配列は存在し空であるべき
            var components = doc.RootElement.GetProperty("components");
            Assert.Equal(0, components.GetArrayLength());
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateSbom_SPDX_EmptyResultLists_ProducesValidJson()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("spdx-empty-full");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir,
                shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));

            var json = File.ReadAllText(sbomPath);
            var doc = JsonDocument.Parse(json);

            var packages = doc.RootElement.GetProperty("packages");
            Assert.Equal(0, packages.GetArrayLength());
        }
    }
}
