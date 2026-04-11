using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Generates an SBOM (Software Bill of Materials) in CycloneDX or SPDX format,
    /// listing all components found in the compared folders.
    /// 比較対象フォルダに含まれるコンポーネント一覧を CycloneDX または SPDX 形式で出力する
    /// SBOM（ソフトウェア部品表）生成サービス。
    /// </summary>
    public sealed class SbomGenerateService
    {
        internal const string CYCLONEDX_FILE_NAME = "sbom.cdx.json";
        internal const string SPDX_FILE_NAME = "sbom.spdx.json";

        private static readonly string[] s_assemblyExtensions = { ".dll", ".exe" };

        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="SbomGenerateService"/>.
        /// <see cref="SbomGenerateService"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="fileDiffResultLists">Comparison results to extract components from. / コンポーネント抽出元の比較結果。</param>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        public SbomGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Generates an SBOM file using the specified <paramref name="context"/>.
        /// No-op when <see cref="IReadOnlyConfigSettings.ShouldGenerateSbom"/> is <see langword="false"/>.
        /// 指定された <paramref name="context"/> を使って SBOM ファイルを生成します。
        /// <see cref="IReadOnlyConfigSettings.ShouldGenerateSbom"/> が <see langword="false"/> の場合は何もしません。
        /// </summary>
        public void GenerateSbom(ReportGenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.Config.ShouldGenerateSbom) return;

            var format = ParseSbomFormat(context.Config.SbomFormat);
            var fileName = format == Models.SbomFormat.SPDX ? SPDX_FILE_NAME : CYCLONEDX_FILE_NAME;
            var sbomPath = Path.Combine(context.ReportsFolderAbsolutePath, fileName);

            try
            {
                var json = format == Models.SbomFormat.SPDX
                    ? SerializeSpdx(context)
                    : SerializeCycloneDx(context);

                PathValidator.ValidateAbsolutePathLengthOrThrow(sbomPath);
                PrepareOutputPathForOverwrite(sbomPath);
                File.WriteAllText(sbomPath, json, Encoding.UTF8);
                TrySetReadOnly(sbomPath, format);

                _logger.LogMessage(AppLogLevel.Info,
                    $"SBOM generated ({format}): {sbomPath}",
                    shouldOutputMessageToConsole: true);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to write SBOM to '{sbomPath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
        }

        private static void PrepareOutputPathForOverwrite(string outputFileAbsolutePath)
        {
            if (!File.Exists(outputFileAbsolutePath))
            {
                return;
            }

            var attributes = File.GetAttributes(outputFileAbsolutePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(outputFileAbsolutePath, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(outputFileAbsolutePath);
        }

        private void TrySetReadOnly(string sbomPath, Models.SbomFormat format)
        {
            try
            {
                FileSystemUtility.TrySetReadOnly(sbomPath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to mark SBOM ({format}) as read-only: '{sbomPath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
        }

        // ──────────────────────────────────────────────
        // CycloneDX serialization
        // CycloneDX シリアライゼーション
        // ──────────────────────────────────────────────

        internal string SerializeCycloneDx(ReportGenerationContext context)
        {
            var components = BuildComponentList(context);
            var bom = new CycloneDxBom
            {
                SerialNumber = $"urn:uuid:{Guid.NewGuid()}",
                Metadata = new CycloneDxMetadata
                {
                    Timestamp = DateTimeOffset.Now.ToString("o"),
                    Tools = new List<CycloneDxTool>
                    {
                        new CycloneDxTool
                        {
                            Vendor = Common.Constants.APP_NAME,
                            Name = Common.Constants.APP_NAME,
                            Version = context.AppVersion
                        }
                    }
                },
                Components = components.Select(c => ToCycloneDxComponent(c, context)).ToList()
            };

            return SerializeJson(bom);
        }

        private static CycloneDxComponent ToCycloneDxComponent(SbomComponentInfo info, ReportGenerationContext context)
        {
            var component = new CycloneDxComponent
            {
                Type = IsAssemblyFile(info.RelativePath) ? "library" : "file",
                Name = info.RelativePath,
                Version = info.Version,
                Hashes = string.IsNullOrEmpty(info.Sha256)
                    ? new List<CycloneDxHash>()
                    : new List<CycloneDxHash> { new CycloneDxHash { Alg = "SHA-256", Content = info.Sha256 } },
                Properties = new List<CycloneDxProperty>
                {
                    new CycloneDxProperty { Name = "folderdiff:status", Value = info.Status },
                    new CycloneDxProperty { Name = "folderdiff:folder", Value = info.Folder }
                }
            };

            if (!string.IsNullOrEmpty(info.DiffDetail))
            {
                component.Properties.Add(new CycloneDxProperty { Name = "folderdiff:diffDetail", Value = info.DiffDetail });
            }

            return component;
        }

        // ──────────────────────────────────────────────
        // SPDX serialization
        // SPDX シリアライゼーション
        // ──────────────────────────────────────────────

        internal string SerializeSpdx(ReportGenerationContext context)
        {
            var components = BuildComponentList(context);
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var doc = new SpdxDocument
            {
                Name = $"{Common.Constants.APP_NAME}-SBOM",
                DocumentNamespace = $"https://folderdiff.local/sbom/{Guid.NewGuid()}",
                CreationInfo = new SpdxCreationInfo
                {
                    Created = timestamp,
                    Creators = new List<string> { $"Tool: {Common.Constants.APP_NAME}-{context.AppVersion}" }
                },
                Packages = components.Select((c, i) => ToSpdxPackage(c, i)).ToList()
            };

            return SerializeJson(doc);
        }

        private static SpdxPackage ToSpdxPackage(SbomComponentInfo info, int index)
        {
            return new SpdxPackage
            {
                SPDXID = $"SPDXRef-Package-{index}",
                Name = info.RelativePath,
                VersionInfo = info.Version,
                FilesAnalyzed = false,
                Checksums = string.IsNullOrEmpty(info.Sha256)
                    ? new List<SpdxChecksum>()
                    : new List<SpdxChecksum> { new SpdxChecksum { Algorithm = "SHA256", ChecksumValue = info.Sha256 } },
                Comment = string.IsNullOrEmpty(info.DiffDetail)
                    ? $"Status: {info.Status}"
                    : $"Status: {info.Status}, DiffDetail: {info.DiffDetail}"
            };
        }

        // ──────────────────────────────────────────────
        // Component extraction
        // コンポーネント抽出
        // ──────────────────────────────────────────────

        internal List<SbomComponentInfo> BuildComponentList(ReportGenerationContext context)
        {
            var components = new List<SbomComponentInfo>();

            // Added files (from new folder) / 追加ファイル（新フォルダから）
            foreach (var absPath in _fileDiffResultLists.AddedFilesAbsolutePath)
            {
                var relPath = Path.GetRelativePath(context.NewFolderAbsolutePath, absPath);
                components.Add(new SbomComponentInfo
                {
                    RelativePath = relPath,
                    Status = "Added",
                    Folder = "new",
                    Sha256 = ComputeFileHash(absPath)
                });
            }

            // Removed files (from old folder) / 削除ファイル（旧フォルダから）
            foreach (var absPath in _fileDiffResultLists.RemovedFilesAbsolutePath)
            {
                var relPath = Path.GetRelativePath(context.OldFolderAbsolutePath, absPath);
                components.Add(new SbomComponentInfo
                {
                    RelativePath = relPath,
                    Status = "Removed",
                    Folder = "old",
                    Sha256 = ComputeFileHash(absPath)
                });
            }

            // Modified files / 変更ファイル
            foreach (var relPath in _fileDiffResultLists.ModifiedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .TryGetValue(relPath, out var detail) ? detail.ToString() : string.Empty;
                var newFilePath = Path.Combine(context.NewFolderAbsolutePath, relPath);
                components.Add(new SbomComponentInfo
                {
                    RelativePath = relPath,
                    Status = "Modified",
                    Folder = "new",
                    DiffDetail = diffDetail,
                    Sha256 = ComputeFileHash(newFilePath)
                });
            }

            // Unchanged files / 未変更ファイル
            foreach (var relPath in _fileDiffResultLists.UnchangedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .TryGetValue(relPath, out var detail) ? detail.ToString() : string.Empty;
                var newFilePath = Path.Combine(context.NewFolderAbsolutePath, relPath);
                components.Add(new SbomComponentInfo
                {
                    RelativePath = relPath,
                    Status = "Unchanged",
                    Folder = "new",
                    DiffDetail = diffDetail,
                    Sha256 = ComputeFileHash(newFilePath)
                });
            }

            return components
                .OrderBy(c => c.Status, StringComparer.Ordinal)
                .ThenBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ヘルパー
        // ──────────────────────────────────────────────

        internal static SbomFormat ParseSbomFormat(string formatString)
        {
            if (string.Equals(formatString, "SPDX", StringComparison.OrdinalIgnoreCase))
                return Models.SbomFormat.SPDX;
            return Models.SbomFormat.CycloneDX;
        }

        private static bool IsAssemblyFile(string relativePath)
        {
            var ext = Path.GetExtension(relativePath);
            return s_assemblyExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        private static string ComputeFileHash(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string SerializeJson<T>(T value)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(value, options);
        }
    }

    /// <summary>
    /// Internal DTO for collecting component information before format-specific serialization.
    /// 形式固有のシリアライゼーション前にコンポーネント情報を収集するための内部 DTO。
    /// </summary>
    internal sealed class SbomComponentInfo
    {
        /// <summary>Relative path of the component file. / コンポーネントファイルの相対パス。</summary>
        public string RelativePath { get; init; } = string.Empty;

        /// <summary>Diff status: Added, Removed, Modified, Unchanged. / 差分ステータス。</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Which folder the file was taken from: "old" or "new". / ファイル取得元のフォルダ: "old" または "new"。</summary>
        public string Folder { get; init; } = string.Empty;

        /// <summary>Diff detail result (e.g. SHA256Match, ILMismatch). / 差分詳細結果。</summary>
        public string DiffDetail { get; init; } = string.Empty;

        /// <summary>SHA256 hash of the file. / ファイルの SHA256 ハッシュ。</summary>
        public string Sha256 { get; init; } = string.Empty;

        /// <summary>Assembly/package version if applicable. / 該当する場合のアセンブリ/パッケージバージョン。</summary>
        public string Version { get; init; } = string.Empty;
    }
}
