using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// *_IL.txt (old/new) の生成を担当するサービス。比較時に除外した行を除いた内容を保存し、読み取り専用属性を付与する。
    /// </summary>
    public sealed class ILTextOutputService : IILTextOutputService
    {
        /// <summary>
        /// IL 比較の HTML ログファイル名（サイドバイサイド表示）。
        /// </summary>
        private const string ILTEXT_SUFFIX = "_" + Constants.LABEL_IL + ".txt";

        /// <summary>
        /// IL テキスト出力失敗時のメッセージ
        /// </summary>
        private const string ERROR_FAILED_TO_OUTPUT_IL_TEXT = $"Failed to output {Constants.LABEL_IL} Text.";
        /// <summary>
        /// 旧 IL フォルダの絶対パス
        /// </summary>
        private readonly string _ilOldFolderAbsolutePath;
        /// <summary>
        /// 新 IL フォルダの絶対パス
        /// </summary>
        private readonly string _ilNewFolderAbsolutePath;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ilOldFolderAbsolutePath">旧 IL フォルダの絶対パス</param>
        /// <param name="ilNewFolderAbsolutePath">新 IL フォルダの絶対パス</param>
        /// <param name="logger">ログ出力サービス。</param>
        public ILTextOutputService(DiffExecutionContext executionContext, ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(executionContext);

            _ilOldFolderAbsolutePath = executionContext.IlOldFolderAbsolutePath;
            _ilNewFolderAbsolutePath = executionContext.IlNewFolderAbsolutePath;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// old/new 両側 IL 全文テキストを *_IL.txt に出力（比較時の除外行を適用後）し読み取り専用化。
        /// </summary>
        /// <exception cref="Exception">IL テキストの書き出しに失敗した場合。</exception>
        public async Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
        {
            try
            {
                // 相対パスのサニタイズを実施した後、_IL.txt を付与してファイル名を決定
                string ilTextFileName = TextSanitizer.Sanitize(fileRelativePath) + ILTEXT_SUFFIX;

                // 出力先ファイルの絶対パスを決定・妥当性を検証
                string oldILFileAbsolutePath = Path.Combine(_ilOldFolderAbsolutePath, ilTextFileName);
                string newILFileAbsolutePath = Path.Combine(_ilNewFolderAbsolutePath, ilTextFileName);
                PathValidator.ValidateAbsolutePathLengthOrThrow(oldILFileAbsolutePath);
                PathValidator.ValidateAbsolutePathLengthOrThrow(newILFileAbsolutePath);
                File.Delete(oldILFileAbsolutePath);
                File.Delete(newILFileAbsolutePath);

                // ILの出力
                await File.WriteAllLinesAsync(oldILFileAbsolutePath, il1LinesMvidExcluded);
                await File.WriteAllLinesAsync(newILFileAbsolutePath, il2LinesMvidExcluded);

                // 読み取り専用属性の設定
                try
                {
                    FileSystemUtility.TrySetReadOnly(oldILFileAbsolutePath);
                    FileSystemUtility.TrySetReadOnly(newILFileAbsolutePath);
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, ex.Message, shouldOutputMessageToConsole: true, ex);
                }
            }
            catch (Exception)
            {
                _logger.LogMessage(AppLogLevel.Error, ERROR_FAILED_TO_OUTPUT_IL_TEXT, shouldOutputMessageToConsole: true);
                throw;
            }
        }
    }
}
