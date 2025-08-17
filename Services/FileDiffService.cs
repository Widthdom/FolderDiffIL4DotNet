using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 個々のファイル比較（MD5/IL/テキスト）と、その前段となる事前計算の入口を提供するサービス。
    /// </summary>
    public sealed class FileDiffService
    {
        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 出力サービス
        /// </summary>
        private readonly ILOutputService _ilOutputService;

        /// <summary>
        /// 旧バージョン側（比較元）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _oldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _newFolderAbsolutePath;

        /// <summary>
        /// 依存関係を受け取り初期化します。
        /// </summary>
        public FileDiffService(ConfigSettings config, ILOutputService ilOutputService, string oldFolderAbsolutePath, string newFolderAbsolutePath)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ilOutputService = ilOutputService ?? throw new ArgumentNullException(nameof(ilOutputService));
            _oldFolderAbsolutePath = oldFolderAbsolutePath ?? throw new ArgumentNullException(nameof(oldFolderAbsolutePath));
            _newFolderAbsolutePath = newFolderAbsolutePath ?? throw new ArgumentNullException(nameof(newFolderAbsolutePath));
        }

        /// <summary>
        /// IL キャッシュ関連の事前計算を実行します（実体は ILOutputService に委譲）。
        /// </summary>
        public Task PrecomputeAsync(System.Collections.Generic.IEnumerable<string> filesAbsolutePath, int maxParallel)
            => _ilOutputService.PrecomputeAsync(filesAbsolutePath, maxParallel);

        /// <summary>
        /// 2つのファイルが等しいかどうかを比較します。
        /// ファイル種別に応じて MD5/IL/テキストの比較を選択し、詳細は <see cref="FileDiffResultLists"/> に記録します。
        /// </summary>
        /// <param name="fileRelativePath">フォルダ基準の相対パス。</param>
        /// <param name="maxParallel">テキスト比較時の最大並列数。</param>
        public async Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1)
        {
            string file1AbsolutePath = Path.Combine(_oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
            try
            {
                // 1) MD5
                if (await Utility.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath))
                {
                    FileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Match);
                    return true;
                }

                // 2) .NET アセンブリなら IL
                if (Utility.IsDotNetExecutable(file1AbsolutePath))
                {
                    try
                    {
                        bool areDotNetAssembliesEqual = await _ilOutputService.DiffDotNetAssembliesAsync(fileRelativePath, _oldFolderAbsolutePath, _newFolderAbsolutePath, _config.ShouldOutputILText);
                        FileDiffResultLists.RecordDiffDetail(fileRelativePath, areDotNetAssembliesEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch);
                        return areDotNetAssembliesEqual;
                    }
                    catch (InvalidOperationException ex)
                    {
                        LoggerService.LogMessage($"[ERROR] IL diff failed for '{fileRelativePath}'.", shouldOutputMessageToConsole: true, ex);
                        throw;
                    }
                }

                // 3) テキスト拡張子ならテキスト比較
                if (_config.TextFileExtensions.Contains(Path.GetExtension(file1AbsolutePath).ToLower()))
                {
                    bool areTextFilesEqual;
                    try
                    {
                        var file1Info = new FileInfo(file1AbsolutePath);
                        areTextFilesEqual = await Utility.DiffTextFilesParallelAsync(file1AbsolutePath, file2AbsolutePath, largeFileSizeThresholdBytes: 512 * 1024, maxParallel);
                        if (file1Info.Length < 512 * 1024)
                        {
                            areTextFilesEqual = await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                        }
                    }
                    catch
                    {
                        areTextFilesEqual = await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    FileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);
                    return areTextFilesEqual;
                }

                FileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Mismatch);
                return false;
            }
            catch (Exception)
            {
                LoggerService.LogMessage($"[ERROR] An error occurred while diffing '{file1AbsolutePath}' and '{file2AbsolutePath}'.", shouldOutputMessageToConsole: true);
                throw;
            }
        }
    }
}
