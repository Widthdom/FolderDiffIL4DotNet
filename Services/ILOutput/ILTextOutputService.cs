using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// Generates *_IL.txt files (old/new) containing IL text with excluded lines removed, and marks them read-only.
    /// *_IL.txt (old/new) の生成を担当するサービス。比較時に除外した行を除いた内容を保存し、読み取り専用属性を付与します。
    /// </summary>
    public sealed class ILTextOutputService : IILTextOutputService
    {
        private const string ILTEXT_SUFFIX = "_" + Constants.LABEL_IL + ".txt";
        private const string ERROR_FAILED_TO_OUTPUT_IL_TEXT = $"Failed to output {Constants.LABEL_IL} Text.";
        private readonly string _ilOldFolderAbsolutePath;
        private readonly string _ilNewFolderAbsolutePath;
        private readonly ILoggerService _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ILTextOutputService"/>.
        /// <see cref="ILTextOutputService"/> の新しいインスタンスを初期化します。
        /// </summary>
        public ILTextOutputService(DiffExecutionContext executionContext, ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(executionContext);

            _ilOldFolderAbsolutePath = executionContext.IlOldFolderAbsolutePath;
            _ilNewFolderAbsolutePath = executionContext.IlNewFolderAbsolutePath;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Writes full old/new IL text (with excluded lines removed) to *_IL.txt and marks them read-only.
        /// old/new 両側の IL 全文テキストを *_IL.txt に出力（除外行適用後）し、読み取り専用化します。
        /// </summary>
        public async Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
        {
            string oldILFileAbsolutePath = string.Empty;
            string newILFileAbsolutePath = string.Empty;
            try
            {
                // Sanitize the relative path and append _IL.txt to form the file name
                // 相対パスのサニタイズを実施した後、_IL.txt を付与してファイル名を決定
                string ilTextFileName = TextSanitizer.Sanitize(fileRelativePath) + ILTEXT_SUFFIX;

                // Determine and validate the output file absolute paths
                // 出力先ファイルの絶対パスを決定・妥当性を検証
                oldILFileAbsolutePath = Path.Combine(_ilOldFolderAbsolutePath, ilTextFileName);
                newILFileAbsolutePath = Path.Combine(_ilNewFolderAbsolutePath, ilTextFileName);
                PathValidator.ValidateAbsolutePathLengthOrThrow(oldILFileAbsolutePath);
                PathValidator.ValidateAbsolutePathLengthOrThrow(newILFileAbsolutePath);
                PrepareOutputPathForOverwrite(oldILFileAbsolutePath);
                PrepareOutputPathForOverwrite(newILFileAbsolutePath);

                // Write IL output
                // IL の出力
                await File.WriteAllLinesAsync(oldILFileAbsolutePath, il1LinesMvidExcluded);
                await File.WriteAllLinesAsync(newILFileAbsolutePath, il2LinesMvidExcluded);

                // Set read-only attribute
                // 読み取り専用属性の設定
                TrySetReadOnly(fileRelativePath, oldILFileAbsolutePath);
                TrySetReadOnly(fileRelativePath, newILFileAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                _logger.LogMessage(AppLogLevel.Error,
                    $"{ERROR_FAILED_TO_OUTPUT_IL_TEXT} File='{fileRelativePath}', OldRoot='{_ilOldFolderAbsolutePath}', NewRoot='{_ilNewFolderAbsolutePath}', Old='{oldILFileAbsolutePath}', New='{newILFileAbsolutePath}' ({PathShapeDiagnostics.DescribeState("OldRoot", _ilOldFolderAbsolutePath)}, {PathShapeDiagnostics.DescribeState("NewRoot", _ilNewFolderAbsolutePath)}, {PathShapeDiagnostics.DescribeState("OldOutput", oldILFileAbsolutePath)}, {PathShapeDiagnostics.DescribeState("NewOutput", newILFileAbsolutePath)}, {ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                throw;
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

        private void TrySetReadOnly(string fileRelativePath, string outputFileAbsolutePath)
        {
            try
            {
                FileSystemUtility.TrySetReadOnly(outputFileAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                var (side, outputRoot) = DescribeOutputSide(outputFileAbsolutePath);
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to mark IL text output as read-only for '{fileRelativePath}' ({side}, OutputRoot='{outputRoot}', {PathShapeDiagnostics.DescribeState("OutputRoot", outputRoot)}, {PathShapeDiagnostics.DescribeState("OutputPath", outputFileAbsolutePath)}): '{outputFileAbsolutePath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        private (string Side, string OutputRoot) DescribeOutputSide(string outputFileAbsolutePath)
        {
            if (outputFileAbsolutePath.StartsWith(_ilOldFolderAbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                return ("Old", _ilOldFolderAbsolutePath);
            }

            if (outputFileAbsolutePath.StartsWith(_ilNewFolderAbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                return ("New", _ilNewFolderAbsolutePath);
            }

            return ("UnknownSide", string.Empty);
        }
    }
}
