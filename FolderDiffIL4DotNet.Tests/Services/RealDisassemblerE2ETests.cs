using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Trait("Category", "E2E")]
    public sealed class RealDisassemblerE2ETests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();

        public RealDisassemblerE2ETests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-real-disasm-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _resultLists.ResetAll();
        }

        public void Dispose()
        {
            _resultLists.ResetAll();
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        // Compare two non-deterministically rebuilt copies of the same assembly via dotnet-ildasm and verify IL match
        // dotnet-ildasm で非決定的に再ビルドした同一アセンブリを比較し、IL 一致として扱えることを確認する
        [SkippableFact]
        public async Task FilesAreEqualAsync_WhenDotNetIldasmComparesNonDeterministicRebuilds_ReturnsIlMatch()
        {
            Skip.IfNot(IsE2EEnabled(), "Set FOLDERDIFF_RUN_E2E=true to run E2E tests.");
            Skip.If(!CanRunDotNetIldasm(), "dotnet-ildasm is not available in this environment.");
            var previousRollForward = Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD");
            Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", "Major");
            try
            {
                var oldDir = Path.Combine(_rootDir, "old");
                var newDir = Path.Combine(_rootDir, "new");
                Directory.CreateDirectory(oldDir);
                Directory.CreateDirectory(newDir);

                var oldAssemblyBuiltPath = await BuildLibraryAsync(Path.Combine(_rootDir, "old-build"), "SampleLibrary");
                var newAssemblyBuiltPath = await BuildLibraryAsync(Path.Combine(_rootDir, "new-build"), "SampleLibrary");

                var oldAssemblyPath = Path.Combine(oldDir, "SampleLibrary.dll");
                var newAssemblyPath = Path.Combine(newDir, "SampleLibrary.dll");
                File.Copy(oldAssemblyBuiltPath, oldAssemblyPath, overwrite: true);
                File.Copy(newAssemblyBuiltPath, newAssemblyPath, overwrite: true);

                var fileComparisonService = new FileComparisonService();
                Assert.False(await fileComparisonService.DiffFilesByHashAsync(oldAssemblyPath, newAssemblyPath));

                var config = new ConfigSettings
                {
                    TextFileExtensions = new List<string> { ".txt" },
                    IgnoredExtensions = new List<string>(),
                    ShouldOutputILText = false,
                    EnableILCache = false,
                    ShouldIgnoreILLinesContainingConfiguredStrings = false,
                    ILIgnoreLineContainingStrings = new List<string>(),
                    OptimizeForNetworkShares = false
                };

                var executionContext = new DiffExecutionContext(
                    oldDir,
                    newDir,
                    Path.Combine(_rootDir, "report"),
                    optimizeForNetworkShares: false,
                    detectedNetworkOld: false,
                    detectedNetworkNew: false);

                var ilTextOutputService = new ILTextOutputService(executionContext, _logger);
                var dotNetDisassembleService = new DotNetDisassembleService(config, ilCache: null, _resultLists, _logger, new DotNetDisassemblerCache(_logger));
                var ilOutputService = new ILOutputService(config, executionContext, ilTextOutputService, dotNetDisassembleService, ilCache: null, _logger);
                var service = new FileDiffService(config, ilOutputService, executionContext, _resultLists, _logger);

                var areEqual = await service.FilesAreEqualAsync("SampleLibrary.dll");

                Assert.True(areEqual);
                Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMatch, _resultLists.FileRelativePathToDiffDetailDictionary["SampleLibrary.dll"]);
                Assert.True(_resultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue("SampleLibrary.dll", out var disassemblerLabel));
                Assert.Contains(Constants.DOTNET_ILDASM, disassemblerLabel, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_ROLL_FORWARD", previousRollForward);
            }
        }

        // Check whether E2E tests are opted-in via environment variable
        // 環境変数で E2E テストが有効化されているかを確認する
        private static bool IsE2EEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("FOLDERDIFF_RUN_E2E"), "true", StringComparison.OrdinalIgnoreCase);

        private static bool CanRunDotNetIldasm()
            => CanRunCommand(Constants.DOTNET_ILDASM, "--version")
                || CanRunCommand(Constants.DOTNET_MUXER, Constants.ILDASM_LABEL, "--version");

        // Build a non-deterministic class library with at least one method and return the DLL path
        // 少なくとも 1 メソッドを持つ非決定的クラスライブラリをビルドし、DLL パスを返す
        private static async Task<string> BuildLibraryAsync(string projectDir, string assemblyName)
        {
            Directory.CreateDirectory(projectDir);

            var projectFilePath = Path.Combine(projectDir, $"{assemblyName}.csproj");
            await File.WriteAllTextAsync(projectFilePath, $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{assemblyName}</AssemblyName>
    <Deterministic>false</Deterministic>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>
</Project>
""");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "SampleType.cs"), """
namespace SampleLibraryNamespace
{
    public static class SampleType
    {
        public static int Compute()
        {
            return 42;
        }
    }
}
""");

            await RunProcessAsync("dotnet", projectDir, "build", projectFilePath, "--configuration", "Release", "--nologo");
            return Path.Combine(projectDir, "bin", "Release", "net8.0", $"{assemblyName}.dll");
        }

        private static bool CanRunCommand(string fileName, params string[] arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore kill failures when probing availability / 利用可能性チェック時の kill 失敗を無視
                    }

                    return false;
                }
                return process.HasExited && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task RunProcessAsync(string fileName, string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process '{fileName}' failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }
        }
    }
}
