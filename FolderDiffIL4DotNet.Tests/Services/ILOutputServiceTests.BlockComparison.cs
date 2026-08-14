using System.Collections.Generic;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class ILOutputServiceTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_TenThousandNestedClasses_UsesCompactContainerKeys()
        {
            const int depth = 10_000;
            var lines = new List<string>(depth * 3);
            for (int i = 0; i < depth; i++)
            {
                lines.Add(".class C");
                lines.Add("{");
            }
            for (int i = 0; i < depth; i++)
            {
                lines.Add("}");
            }

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines, new List<string>(lines)));
        }
    }
}
