using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    public interface IILOutputService
    {
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel);

        Task<(bool AreEqual, string DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText);
    }
}
