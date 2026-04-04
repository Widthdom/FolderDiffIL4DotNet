using System.Collections.Generic;
using CoreILBlockParser = FolderDiffIL4DotNet.Core.IL.ILBlockParser;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// Thin forwarding wrapper to <see cref="FolderDiffIL4DotNet.Core.IL.ILBlockParser"/>.
    /// Implementation moved to Core for reusability.
    /// <see cref="FolderDiffIL4DotNet.Core.IL.ILBlockParser"/> への薄い転送ラッパー。
    /// 再利用性のため実装を Core に移動。
    /// </summary>
    internal static class ILBlockParser
    {
        internal static List<List<string>> ParseBlocks(IReadOnlyList<string> lines)
            => CoreILBlockParser.ParseBlocks(lines);

        internal static string ExtractBlockSignature(IReadOnlyList<string> blockLines)
            => CoreILBlockParser.ExtractBlockSignature(blockLines);
    }
}
