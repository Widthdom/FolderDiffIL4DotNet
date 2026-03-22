using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class AssemblyMethodAnalyzerTests : IDisposable
    {
        private readonly string _tempDir;

        public AssemblyMethodAnalyzerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"AsmAnalyzerTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void Analyze_SameAssembly_NoChanges()
        {
            // Compare a real assembly to itself — should report no changes
            // 実アセンブリを自分自身と比較 — 変更なしが期待される
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var result = AssemblyMethodAnalyzer.Analyze(assemblyPath, assemblyPath);

            Assert.NotNull(result);
            Assert.False(result.HasChanges);
            Assert.Empty(result.Entries);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(0, result.ModifiedCount);
        }

        [Fact]
        public void Analyze_NonExistentFile_ReturnsNull()
        {
            // Attempting to analyse a missing file should gracefully return null
            // 存在しないファイルの解析は null を返すべき
            var result = AssemblyMethodAnalyzer.Analyze("/nonexistent/old.dll", "/nonexistent/new.dll");
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_InvalidFile_ReturnsNull()
        {
            // Attempting to analyse a non-PE file should gracefully return null
            // PE でないファイルの解析は null を返すべき
            var textFile = typeof(AssemblyMethodAnalyzerTests).Assembly.Location + ".runtimeconfig.json";
            if (!System.IO.File.Exists(textFile)) return; // skip if runtime config not available
            var result = AssemblyMethodAnalyzer.Analyze(textFile, textFile);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_DifferentAssemblies_DetectsChanges()
        {
            // Compare test assembly to main assembly — should detect differences
            // テストアセンブリとメインアセンブリを比較 — 差異が検出されるべき
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            Assert.True(result.HasChanges);
            Assert.True(result.Entries.Count > 0);
        }

        [Fact]
        public void Analyze_DifferentAssemblies_EntriesHaveStructuredData()
        {
            // Entries should contain structured MemberChangeEntry data
            // エントリには構造化された MemberChangeEntry データが含まれるべき
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var firstEntry = result.Entries.First();
            Assert.False(string.IsNullOrEmpty(firstEntry.Change));
            Assert.False(string.IsNullOrEmpty(firstEntry.TypeName));
            Assert.False(string.IsNullOrEmpty(firstEntry.MemberKind));
            Assert.Contains(firstEntry.Change, new[] { "Added", "Removed", "Modified" });
            Assert.Contains(firstEntry.MemberKind, new[] { "Class", "Record", "Struct", "Interface", "Enum", "Constructor", "StaticConstructor", "Method", "Property", "Field" });
        }

        [Fact]
        public void Analyze_DifferentAssemblies_ModifiedEntriesIfPresentHaveValidChangeKind()
        {
            // When comparing different assemblies, if any Modified entries exist,
            // they should have Change="Modified" and a valid MemberKind.
            // 異なるアセンブリ比較時、Modified エントリが存在する場合、
            // Change="Modified" と有効な MemberKind を持つべき。
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var modifiedEntries = result.Entries.Where(e => e.Change == "Modified").ToList();
            // Modified entries may or may not exist between unrelated assemblies,
            // but if they do, they must have valid structure.
            foreach (var entry in modifiedEntries)
            {
                Assert.Equal("Modified", entry.Change);
                Assert.False(string.IsNullOrEmpty(entry.TypeName));
                Assert.Contains(entry.MemberKind, new[] { "Constructor", "StaticConstructor", "Method", "Property", "Field" });
            }
        }

        [Fact]
        public void Analyze_DifferentAssemblies_AllEntriesHavePopulatedAccessField()
        {
            // All entries (Added/Removed/Modified) should have the Access field populated
            // for methods, properties, and fields.
            // すべてのエントリ（Added/Removed/Modified）で、メソッド・プロパティ・フィールドの
            // Access フィールドが設定されているべき。
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var memberEntries = result.Entries
                .Where(e => e.MemberKind is "Method" or "Property" or "Field"
                         or "Constructor" or "StaticConstructor")
                .ToList();

            Assert.True(memberEntries.Count > 0, "Expected at least one member entry between two different assemblies");
            // Every member-level entry should have a non-empty Access value
            // (or "old → new" for Modified entries with access changes)
            Assert.True(memberEntries.All(m => !string.IsNullOrEmpty(m.Access)),
                "All member entries should have a non-empty Access field");
        }

        [Fact]
        public void Analyze_TruncatedPEFile_ReturnsNull()
        {
            // A file with a valid MZ header but truncated PE data should trigger the
            // catch-all fallback and return null instead of throwing.
            // 有効な MZ ヘッダーを持つが PE データが切り詰められたファイルは
            // catch-all フォールバックで null を返すべき。
            var truncatedPath = Path.Combine(_tempDir, "truncated.dll");
            // MZ header (first two bytes) followed by garbage — enough to pass initial
            // File.Open but fail during metadata parsing.
            var bytes = new byte[64];
            bytes[0] = 0x4D; // 'M'
            bytes[1] = 0x5A; // 'Z'
            File.WriteAllBytes(truncatedPath, bytes);

            var result = AssemblyMethodAnalyzer.Analyze(truncatedPath, truncatedPath);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_EmptyFile_ReturnsNull()
        {
            // A zero-byte file should trigger the catch-all and return null.
            // 0 バイトファイルは catch-all でnull を返すべき。
            var emptyPath = Path.Combine(_tempDir, "empty.dll");
            File.WriteAllBytes(emptyPath, Array.Empty<byte>());

            var result = AssemblyMethodAnalyzer.Analyze(emptyPath, emptyPath);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_CorruptPEWithValidHeader_ReturnsNull()
        {
            // A file with a plausible PE header but corrupted metadata tables should
            // trigger the catch-all fallback path in AssemblyMethodAnalyzer.Analyze.
            // もっともらしい PE ヘッダーを持つが破損したメタデータテーブルのファイルは
            // AssemblyMethodAnalyzer.Analyze の catch-all フォールバックを発火させるべき。
            var corruptPath = Path.Combine(_tempDir, "corrupt.dll");

            // Build a minimal DOS header → PE signature → COFF header → optional header
            // but with invalid metadata RVA so System.Reflection.Metadata will fail.
            // Copy a real assembly then corrupt the metadata section.
            var realAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var assemblyBytes = File.ReadAllBytes(realAssembly);

            // Corrupt bytes in the middle of the assembly (metadata tables region)
            // to trigger an exception during ReadAssemblySnapshot.
            var random = new Random(42);
            int corruptStart = Math.Min(256, assemblyBytes.Length / 2);
            int corruptEnd = Math.Min(corruptStart + 512, assemblyBytes.Length);
            for (int i = corruptStart; i < corruptEnd; i++)
            {
                assemblyBytes[i] = (byte)random.Next(256);
            }
            File.WriteAllBytes(corruptPath, assemblyBytes);

            var result = AssemblyMethodAnalyzer.Analyze(corruptPath, corruptPath);
            // Should return null (catch-all) rather than throwing
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_OneValidOneCorrupt_ReturnsNull()
        {
            // When one assembly is valid but the other is corrupt, the catch-all
            // should still gracefully return null.
            // 一方が有効で他方が破損している場合でも catch-all で null を返すべき。
            var validPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var corruptPath = Path.Combine(_tempDir, "one-corrupt.dll");
            File.WriteAllBytes(corruptPath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });

            var result = AssemblyMethodAnalyzer.Analyze(validPath, corruptPath);
            Assert.Null(result);
        }
    }
}
