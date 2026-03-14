using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    public interface IFileDiffService
    {
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePath, int maxParallel);

        Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1);
    }
}
