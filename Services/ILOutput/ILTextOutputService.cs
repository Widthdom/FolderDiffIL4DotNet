using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// *_IL.txt (old/new) の生成を担当するサービス。MVID 行を除外後に保存し、読み取り専用属性を付与する。
    /// </summary>
    public sealed class ILTextOutputService
    {
        /// <summary>
        /// 旧 IL フォルダの絶対パス
        /// </summary>
        private readonly string _ilOldFolderAbsolutePath;
        /// <summary>
        /// 新 IL フォルダの絶対パス
        /// </summary>
        private readonly string _ilNewFolderAbsolutePath;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ilOldFolderAbsolutePath">旧 IL フォルダの絶対パス</param>
        /// <param name="ilNewFolderAbsolutePath">新 IL フォルダの絶対パス</param>
        public ILTextOutputService(string ilOldFolderAbsolutePath, string ilNewFolderAbsolutePath)
        {
            _ilOldFolderAbsolutePath = ilOldFolderAbsolutePath ?? throw new ArgumentNullException(nameof(ilOldFolderAbsolutePath));
            _ilNewFolderAbsolutePath = ilNewFolderAbsolutePath ?? throw new ArgumentNullException(nameof(ilNewFolderAbsolutePath));
        }

        /// <summary>
        /// old/new 両側 IL 全文テキストを *_IL.txt に出力 (MVID 除外後) し読み取り専用化。
        /// </summary>
        /// <exception cref="Exception">IL テキストの書き出しに失敗した場合。</exception>
        public async Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
        {
            try
            {
                // 相対パスのサニタイズを実施した後、_IL.txt を付与してファイル名を決定
                string ilTextFileName = Utility.Sanitize(fileRelativePath) + Constants.ILTEXT_SUFFIX;

                // 出力先ファイルの絶対パスを決定・妥当性を検証
                string oldILFileAbsolutePath = Path.Combine(_ilOldFolderAbsolutePath, ilTextFileName);
                string newILFileAbsolutePath = Path.Combine(_ilNewFolderAbsolutePath, ilTextFileName);
                Utility.ValidateAbsolutePathLengthOrThrow(oldILFileAbsolutePath);
                Utility.ValidateAbsolutePathLengthOrThrow(newILFileAbsolutePath);
                File.Delete(oldILFileAbsolutePath);
                File.Delete(newILFileAbsolutePath);

                // ILの出力
                await File.WriteAllLinesAsync(oldILFileAbsolutePath, il1LinesMvidExcluded);
                await File.WriteAllLinesAsync(newILFileAbsolutePath, il2LinesMvidExcluded);

                // 読み取り専用属性の設定
                try
                {
                    Utility.TrySetReadOnly(oldILFileAbsolutePath);
                    Utility.TrySetReadOnly(newILFileAbsolutePath);
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, ex.Message, shouldOutputMessageToConsole: true, ex);
                }
            }
            catch (Exception)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Error, Constants.ERROR_FAILED_TO_OUTPUT_IL_TEXT, shouldOutputMessageToConsole: true);
                throw;
            }
        }
    }
}
