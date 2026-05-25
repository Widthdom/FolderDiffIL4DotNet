using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="FileDiffService"/> using fake I/O collaborators (no real disk access).
    /// フェイク I/O 協力オブジェクトを使用した <see cref="FileDiffService"/> のユニットテスト（実ディスクアクセスなし）。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed partial class FileDiffServiceUnitTests
    {
        // Factory method and fake collaborators for all partial files.
        // 全 partial ファイル共通のファクトリメソッドおよびフェイク協力オブジェクト。

        private static FileDiffService CreateService(
            FakeFileComparisonService fileComparisonService,
            FakeILOutputService ilOutputService,
            FileDiffResultLists resultLists,
            TestLogger logger,
            bool optimizeForNetworkShares = false,
            Action<ConfigSettingsBuilder>? configure = null)
        {
            var builder = new ConfigSettingsBuilder
            {
                TextFileExtensions = new List<string> { ".txt" },
                IgnoredExtensions = new List<string>(),
                ShouldOutputILText = false,
                EnableILCache = false,
                OptimizeForNetworkShares = optimizeForNetworkShares,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffChunkSizeKilobytes,
                TextDiffParallelMemoryLimitMegabytes = 0,
                ShouldTreatTextByteDifferencesAsMismatch = false
            };
            configure?.Invoke(builder);
            var config = builder.Build();

            var executionContext = new DiffExecutionContext(
                "/virtual/old",
                "/virtual/new",
                "/virtual/report",
                optimizeForNetworkShares: optimizeForNetworkShares,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            return new FileDiffService(config, ilOutputService, executionContext, resultLists, logger, fileComparisonService);
        }

        /// <summary>
        /// Fake file comparison service that returns preconfigured results without touching real files.
        /// 実ファイルにアクセスせず事前設定された結果を返すフェイク比較サービス。
        /// </summary>
        private sealed class FakeFileComparisonService : IFileComparisonService
        {
            private readonly Dictionary<string, byte[]> _fileContentsByPath = new(StringComparer.OrdinalIgnoreCase);

            public bool HashResult { get; set; }

            public Exception HashException { get; set; }

            public bool TextDiffResult { get; set; }

            public Exception TextDiffException { get; set; }

            public Exception ReadChunkException { get; set; }

            /// <summary>
            /// When set to true, overrides the Hash1Hex/Hash2Hex return values with <see cref="Hash1HexOverride"/>/<see cref="Hash2HexOverride"/>.
            /// true に設定すると、Hash1Hex/Hash2Hex の戻り値を <see cref="Hash1HexOverride"/>/<see cref="Hash2HexOverride"/> で上書きします。
            /// </summary>
            public bool UseHashHexOverride { get; set; }

            /// <summary>
            /// Custom Hash1Hex value returned when <see cref="UseHashHexOverride"/> is true. Null triggers the null-check path.
            /// <see cref="UseHashHexOverride"/> が true の場合に返すカスタム Hash1Hex 値。null は null チェックパスを起動します。
            /// </summary>
            public string? Hash1HexOverride { get; set; }

            /// <summary>
            /// Custom Hash2Hex value returned when <see cref="UseHashHexOverride"/> is true. Null triggers the null-check path.
            /// <see cref="UseHashHexOverride"/> が true の場合に返すカスタム Hash2Hex 値。null は null チェックパスを起動します。
            /// </summary>
            public string? Hash2HexOverride { get; set; }

            public DotNetExecutableDetectionResult DotNetDetectionResult { get; set; } =
                new(DotNetExecutableDetectionStatus.NotDotNetExecutable);

            // Thread-safe: these may be called from Parallel.ForEachAsync in FolderDiffService / スレッドセーフ: FolderDiffService の Parallel.ForEachAsync から呼ばれる可能性がある
            public ConcurrentBag<(string File1, string File2)> HashCalls { get; } = new();

            public ConcurrentBag<string> DotNetDetectionCalls { get; } = new();

            public ConcurrentBag<(string File1, string File2)> TextDiffCalls { get; } = new();

            public ConcurrentBag<(string Path, long Offset, int Length)> ReadChunkCalls { get; } = new();

            public void SetFileContent(string path, string content)
                => _fileContentsByPath[path] = System.Text.Encoding.UTF8.GetBytes(content);

            public Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
            {
                HashCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (HashException != null)
                {
                    throw HashException;
                }
                return Task.FromResult(HashResult);
            }

            public Task<(bool AreEqual, string? Hash1Hex, string? Hash2Hex)> DiffFilesByHashWithHexAsync(
                string file1AbsolutePath, string file2AbsolutePath)
            {
                HashCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (HashException != null)
                {
                    throw HashException;
                }
                string? hash1;
                string? hash2;
                if (UseHashHexOverride)
                {
                    hash1 = Hash1HexOverride;
                    hash2 = Hash2HexOverride;
                }
                else
                {
                    hash1 = HashResult ? "a".PadRight(64, '0') : "a".PadRight(64, '0');
                    hash2 = HashResult ? "a".PadRight(64, '0') : "b".PadRight(64, '0');
                }
                return Task.FromResult((HashResult, hash1, hash2));
            }

            public Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
            {
                TextDiffCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (TextDiffException != null)
                {
                    throw TextDiffException;
                }
                return Task.FromResult(TextDiffResult);
            }

            public DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath)
            {
                DotNetDetectionCalls.Add(fileAbsolutePath);
                return DotNetDetectionResult;
            }

            public bool FileExists(string fileAbsolutePath)
                => _fileContentsByPath.ContainsKey(fileAbsolutePath);

            public long GetFileLength(string fileAbsolutePath)
            {
                if (_fileContentsByPath.TryGetValue(fileAbsolutePath, out var content))
                {
                    return content.LongLength;
                }

                throw new FileNotFoundException($"File not found: {fileAbsolutePath}", fileAbsolutePath);
            }

            public Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken)
            {
                ReadChunkCalls.Add((fileAbsolutePath, offset, buffer.Length));
                if (ReadChunkException != null)
                {
                    throw ReadChunkException;
                }
                if (!_fileContentsByPath.TryGetValue(fileAbsolutePath, out var content))
                {
                    throw new FileNotFoundException($"File not found: {fileAbsolutePath}", fileAbsolutePath);
                }

                int start = checked((int)offset);
                if (start >= content.Length)
                {
                    return Task.FromResult(0);
                }

                int count = Math.Min(buffer.Length, content.Length - start);
                content.AsMemory(start, count).CopyTo(buffer);
                return Task.FromResult(count);
            }
        }

        /// <summary>
        /// Fake IL output service that records calls and returns preconfigured results.
        /// 呼び出しを記録し事前設定された結果を返すフェイク IL 出力サービス。
        /// </summary>
        private sealed class FakeILOutputService : IILOutputService
        {
            public (bool AreEqual, string? DisassemblerLabel) DiffResult { get; set; }

            public Exception DiffException { get; set; }

            // Thread-safe: called from Parallel.ForEachAsync via FileDiffService / スレッドセーフ: FileDiffService 経由で Parallel.ForEachAsync から呼ばれる
            public ConcurrentBag<DiffCall> DiffCalls { get; } = new();

            public int PrecomputeCallCount { get; private set; }

            // Thread-safe + order-preserving: PreSeedFileHash may be called from parallel context, assertions need FIFO order / スレッドセーフ＋順序保持: 並列コンテキストから呼ばれ、アサーションで FIFO 順序が必要
            public ConcurrentQueue<(string Path, string Hash)> PreSeedCalls { get; } = new();

            public Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default)
            {
                PrecomputeCallCount++;
                return Task.CompletedTask;
            }

            public void PreSeedFileHash(string fileAbsolutePath, string sha256Hex)
            {
                PreSeedCalls.Enqueue((fileAbsolutePath, sha256Hex));
            }

            public Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText, CancellationToken cancellationToken = default)
            {
                DiffCalls.Add(new DiffCall(fileRelativePath, oldFolderAbsolutePath, newFolderAbsolutePath, shouldOutputIlText));
                if (DiffException != null)
                {
                    throw DiffException;
                }
                return Task.FromResult(DiffResult);
            }
        }

        private sealed record DiffCall(string FileRelativePath, string OldFolderAbsolutePath, string NewFolderAbsolutePath, bool ShouldOutputIlText);
    }
}
