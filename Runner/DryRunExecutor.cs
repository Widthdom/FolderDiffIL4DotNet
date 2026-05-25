using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Performs a dry-run preview: enumerates files and prints statistics without running comparisons or generating reports.
    /// ドライラン（プレビュー）を実行する: ファイル列挙と統計表示のみを行い、比較やレポート生成は行わない。
    /// </summary>
    internal sealed class DryRunExecutor
    {
        private const string HEADER = "=== Dry Run Preview ===";
        private const string FOOTER = "=== End of Dry Run ===";

        private readonly ILoggerService _logger;

        internal DryRunExecutor(ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Runs the dry-run preview and writes statistics to the console.
        /// ドライランプレビューを実行し、統計情報をコンソールに出力する。
        /// </summary>
        internal void Execute(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var executionContext = RunScopeBuilder.BuildExecutionContext(
                oldFolderAbsolutePath, newFolderAbsolutePath,
                Path.GetTempPath(), config);

            using var runProvider = RunScopeBuilder.Build(config, executionContext, _logger);
            using var scope = runProvider.CreateScope();

            var resultLists = scope.ServiceProvider.GetRequiredService<FileDiffResultLists>();
            var strategy = scope.ServiceProvider.GetRequiredService<IFolderDiffExecutionStrategy>();

            // Enumerate files / ファイル列挙
            var oldFiles = strategy.EnumerateIncludedFiles(oldFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.Old);
            var newFiles = strategy.EnumerateIncludedFiles(newFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.New);

            int unionCount = strategy.ComputeUnionFileCount(oldFiles, newFiles);
            int dotNetCandidates = strategy.CountDotNetAssemblyCandidates(oldFiles, newFiles);

            // Compute extension breakdown / 拡張子別集計
            var extensionCounts = oldFiles.Concat(newFiles)
                .Select(f => Path.GetExtension(f).ToLowerInvariant())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .GroupBy(ext => ext)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Take(15)
                .ToList();

            // Detect network paths / ネットワークパス検出
            bool oldIsNetwork = FileSystemUtility.IsLikelyNetworkPath(oldFolderAbsolutePath);
            bool newIsNetwork = FileSystemUtility.IsLikelyNetworkPath(newFolderAbsolutePath);

            // Output / 出力
            Console.WriteLine();
            Console.WriteLine(HEADER);
            Console.WriteLine();
            Console.WriteLine($"  Old folder : {oldFolderAbsolutePath}");
            Console.WriteLine($"  New folder : {newFolderAbsolutePath}");
            Console.WriteLine();
            Console.WriteLine($"  Files in old folder           : {oldFiles.Count:N0}");
            Console.WriteLine($"  Files in new folder           : {newFiles.Count:N0}");
            Console.WriteLine($"  Union (unique relative paths) : {unionCount:N0}");
            Console.WriteLine($"  .NET assembly candidates      : {dotNetCandidates:N0}");

            if (resultLists.IgnoredFilesRelativePathToLocation.Count > 0)
            {
                Console.WriteLine($"  Ignored files (by extension)  : {resultLists.IgnoredFilesRelativePathToLocation.Count:N0}");
            }

            if (oldIsNetwork || newIsNetwork)
            {
                Console.WriteLine();
                Console.WriteLine("  Network path detected:");
                if (oldIsNetwork) Console.WriteLine("    - Old folder is on a network share");
                if (newIsNetwork) Console.WriteLine("    - New folder is on a network share");
                Console.WriteLine("    Tip: Consider --threads to limit parallelism on slow network shares.");
            }

            if (extensionCounts.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Top file extensions:");
                foreach (var group in extensionCounts)
                {
                    Console.WriteLine($"    {group.Key,-12} {group.Count():N0}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  Parallelism : {strategy.DetermineMaxParallel()} (configured={config.MaxParallelism}, auto={(config.MaxParallelism <= 0 ? "yes" : "no")})");
            Console.WriteLine($"  Skip IL     : {config.SkipIL}");
            Console.WriteLine($"  IL cache    : {config.EnableILCache}");
            Console.WriteLine();
            Console.WriteLine(FOOTER);
        }
    }
}
