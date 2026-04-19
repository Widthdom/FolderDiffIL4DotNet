using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Built-in <see cref="IDisassemblerProvider"/> implementation for .NET assemblies.
    /// Delegates to the existing <see cref="IDotNetDisassembleService"/> infrastructure
    /// (dotnet-ildasm / ilspycmd fallback chain with caching and blacklisting).
    /// <para>
    /// .NET アセンブリ用の組み込み <see cref="IDisassemblerProvider"/> 実装。
    /// 既存の <see cref="IDotNetDisassembleService"/> インフラ
    /// （キャッシュとブラックリスト付き dotnet-ildasm / ilspycmd フォールバックチェーン）に委譲します。
    /// </para>
    /// </summary>
    internal sealed class DotNetDisassemblerProvider : IDisassemblerProvider
    {
        private readonly IDotNetDisassembleService _disassembleService;
        private readonly IFileComparisonService _fileComparisonService;
        private readonly ILoggerService _logger;

        internal DotNetDisassemblerProvider(
            IDotNetDisassembleService disassembleService,
            IFileComparisonService fileComparisonService,
            ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(disassembleService);
            ArgumentNullException.ThrowIfNull(fileComparisonService);
            ArgumentNullException.ThrowIfNull(logger);
            _disassembleService = disassembleService;
            _fileComparisonService = fileComparisonService;
            _logger = logger;
        }

        /// <summary>
        /// Built-in .NET provider has highest priority (lowest value).
        /// 組み込み .NET プロバイダは最高優先度（最小値）。
        /// </summary>
        public int Priority => 0;

        /// <inheritdoc />
        public string DisplayName => "dotnet-ildasm/ilspycmd (.NET)";

        /// <summary>
        /// Returns <see langword="true"/> for .NET managed assemblies (.dll, .exe with PE/COFF + CLI metadata).
        /// .NET マネージドアセンブリ（PE/COFF + CLI メタデータ付き .dll, .exe）の場合に <see langword="true"/> を返します。
        /// </summary>
        public bool CanHandle(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            try
            {
                var extension = Path.GetExtension(filePath);
                if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var result = _fileComparisonService.DetectDotNetExecutable(filePath);
                return result.IsDotNetExecutable;
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                var extension = Path.GetExtension(filePath);
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Built-in .NET disassembler provider '{DisplayName}' skipped '{filePath}' (Extension='{extension}', {DescribePathStateForDiagnostics(filePath, ex)}) because managed-assembly detection failed ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: false,
                    ex);
                return false;
            }
        }

        private static string DescribePathStateForDiagnostics(string path, Exception? exception)
        {
            bool existsAsFile = File.Exists(path);
            bool existsAsDirectory = Directory.Exists(path);
            if (existsAsFile || existsAsDirectory || exception == null)
            {
                return $"File={existsAsFile}, Directory={existsAsDirectory}";
            }

            return "File=Unknown, Directory=Unknown";
        }

        /// <summary>
        /// Disassembles a single .NET assembly by delegating to the underlying service's single-file path.
        /// Since <see cref="IDotNetDisassembleService"/> operates on pairs for version consistency,
        /// this method uses the pair API with the same file for both parameters, extracting the first result.
        /// <para>
        /// 基盤サービスのシングルファイルパスに委譲して単一の .NET アセンブリを逆アセンブルします。
        /// <see cref="IDotNetDisassembleService"/> はバージョン整合性のためペア操作を行うため、
        /// このメソッドはペア API に同一ファイルを渡し、最初の結果を抽出します。
        /// </para>
        /// </summary>
        public async Task<DisassemblyResult> DisassembleAsync(string filePath, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            var extension = Path.GetExtension(filePath);
            try
            {
                // Use the pair API with the same file for both old/new to get a single disassembly result.
                // ペア API に同一ファイルを渡して単一の逆アセンブル結果を取得する。
                var (ilText, commandString, _, _) =
                    await _disassembleService.DisassemblePairWithSameDisassemblerAsync(
                        filePath, filePath, cancellationToken);

                return new DisassemblyResult
                {
                    Success = !string.IsNullOrEmpty(ilText),
                    Text = ilText ?? string.Empty,
                    CommandString = commandString ?? string.Empty,
                    VersionLabel = commandString ?? string.Empty
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Built-in .NET disassembler provider '{DisplayName}' failed for '{filePath}' (Extension='{extension}', {ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: false,
                    ex);
                return new DisassemblyResult
                {
                    Success = false,
                    Text = string.Empty,
                    CommandString = $"Error ({ex.GetType().Name}): {ex.Message}",
                    VersionLabel = $"Error ({ex.GetType().Name})"
                };
            }
        }
    }
}
