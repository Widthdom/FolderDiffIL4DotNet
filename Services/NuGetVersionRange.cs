using CoreNuGetVersionRange = FolderDiffIL4DotNet.Core.Versioning.NuGetVersionRange;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Thin forwarding wrapper to <see cref="FolderDiffIL4DotNet.Core.Versioning.NuGetVersionRange"/>.
    /// Implementation moved to Core for reusability.
    /// <see cref="FolderDiffIL4DotNet.Core.Versioning.NuGetVersionRange"/> への薄い転送ラッパー。
    /// 再利用性のため実装を Core に移動。
    /// </summary>
    internal static class NuGetVersionRange
    {
        internal static bool Contains(string versionRange, string version)
            => CoreNuGetVersionRange.Contains(versionRange, version);

        internal static int[]? ParseVersion(string version)
            => CoreNuGetVersionRange.ParseVersion(version);
    }
}
