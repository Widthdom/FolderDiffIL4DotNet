using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class AssemblyMethodAnalyzerTests
    {
        [Fact]
        public void Analyze_SameAssembly_NoChanges()
        {
            // Compare a real assembly to itself — should report no changes
            // 実アセンブリを自分自身と比較 — 変更なしが期待される
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var result = AssemblyMethodAnalyzer.Analyze(assemblyPath, assemblyPath);

            Assert.NotNull(result);
            Assert.False(result.HasChanges);
            Assert.Empty(result.AddedTypes);
            Assert.Empty(result.RemovedTypes);
            Assert.Empty(result.AddedMethods);
            Assert.Empty(result.RemovedMethods);
            Assert.Empty(result.BodyChangedMethods);
            Assert.Empty(result.AddedProperties);
            Assert.Empty(result.RemovedProperties);
            Assert.Empty(result.AddedFields);
            Assert.Empty(result.RemovedFields);
            Assert.True(result.OldMethodCount > 0);
            Assert.Equal(result.OldMethodCount, result.NewMethodCount);
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
            // These are completely different assemblies, so there should be type/method differences
            // 完全に異なるアセンブリなので、型やメソッドの差異があるはず
            Assert.True(result.AddedTypes.Count > 0 || result.RemovedTypes.Count > 0 ||
                        result.AddedMethods.Count > 0 || result.RemovedMethods.Count > 0);
        }
    }
}
