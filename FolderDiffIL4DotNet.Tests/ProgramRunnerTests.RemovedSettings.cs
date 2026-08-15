using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        [Theory]
        [InlineData("--validate-config")]
        [InlineData("--print-config")]
        public async Task RunAsync_RemovedShouldIgnoreMvidSetting_ReturnsMigrationError(string command)
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalOut = Console.Out;
            var originalError = Console.Error;
            using var outputWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outputWriter);
            Console.SetError(errorWriter);

            try
            {
                await WithConfigFileAsync("""{ "ShouldIgnoreMVID": false }""", async () =>
                {
                    int exitCode = await runner.RunAsync(new[] { command });

                    Assert.Equal(3, exitCode);
                    Assert.Contains("'ShouldIgnoreMVID' setting has been removed", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.Contains("always excluded", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.DoesNotContain("\"ShouldIgnoreMVID\"", outputWriter.ToString(), StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        [Theory]
        [InlineData("ShouldIgnoreMVID")]
        [InlineData("shouldignoremvid")]
        public async Task RunAsync_ClearCacheFlag_WithRemovedShouldIgnoreMvidSetting_ReturnsMigrationError(string propertyName)
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                await WithConfigFileAsync($"{{ \"{propertyName}\": false }}", async () =>
                {
                    int exitCode = await runner.RunAsync(new[] { "--clear-cache" });

                    Assert.Equal(3, exitCode);
                    Assert.Contains("'ShouldIgnoreMVID' setting has been removed", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.Contains("always excluded", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.DoesNotContain("--clear-cache requires an interactive terminal", errorWriter.ToString(), StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        [Fact]
        public async Task RunAsync_ClearCacheFlag_WithRemovedShouldIgnoreMvidEnvironmentVariable_ReturnsMigrationError()
        {
            const string removedEnvironmentVariable = "FOLDERDIFF_SHOULDIGNOREMVID";
            string? originalValue = Environment.GetEnvironmentVariable(removedEnvironmentVariable);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                Environment.SetEnvironmentVariable(removedEnvironmentVariable, "false");
                await WithConfigFileAsync("{}", async () =>
                {
                    int exitCode = await runner.RunAsync(new[] { "--clear-cache" });

                    Assert.Equal(3, exitCode);
                    Assert.Contains($"'{removedEnvironmentVariable}' has been removed", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.Contains("always excluded", errorWriter.ToString(), StringComparison.Ordinal);
                    Assert.DoesNotContain("--clear-cache requires an interactive terminal", errorWriter.ToString(), StringComparison.Ordinal);
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable(removedEnvironmentVariable, originalValue);
                Console.SetError(originalError);
            }
        }
    }
}
