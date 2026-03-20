using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts folder-level diff comparison operations.
    /// フォルダ間の差分比較処理を抽象化します。
    /// </summary>
    public interface IFolderDiffService
    {
        /// <summary>
        /// Executes folder diff comparison and outputs the results as a report.
        /// フォルダ差分比較を実行し、結果をレポートとして出力します。
        /// </summary>
        Task ExecuteFolderDiffAsync();
    }
}
