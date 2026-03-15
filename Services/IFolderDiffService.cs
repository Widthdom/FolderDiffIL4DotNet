using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// フォルダ間の差分比較処理を抽象化します。
    /// </summary>
    public interface IFolderDiffService
    {
        /// <summary>
        /// フォルダ差分比較を実行し、結果をレポートとして出力します。
        /// </summary>
        /// <returns>処理が完了したことを示す <see cref="Task"/>。</returns>
        Task ExecuteFolderDiffAsync();
    }
}
