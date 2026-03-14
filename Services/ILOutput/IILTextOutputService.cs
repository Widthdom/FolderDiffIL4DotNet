using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    public interface IILTextOutputService
    {
        Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded);
    }
}
