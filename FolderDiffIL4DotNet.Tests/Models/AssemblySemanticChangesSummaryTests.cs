using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed class AssemblySemanticChangesSummaryTests
    {
        [Fact]
        public void HasChanges_DefaultInstance_ReturnsFalse()
        {
            var summary = new AssemblySemanticChangesSummary();
            Assert.False(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithEntries_ReturnsTrue()
        {
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "public", "", "Method", "DoWork", "", "void", "int count", ""),
                },
            };
            Assert.True(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_EmptyEntries_ReturnsFalse()
        {
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>(),
            };
            Assert.False(summary.HasChanges);
        }

        [Fact]
        public void Entries_ContainStructuredData()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "public", "static", "Method", "GetName", "", "string", "string id", "");
            Assert.Equal("Added", entry.Change);
            Assert.Equal("MyApp.Service", entry.TypeName);
            Assert.Equal("public", entry.Access);
            Assert.Equal("static", entry.Modifiers);
            Assert.Equal("Method", entry.MemberKind);
            Assert.Equal("GetName", entry.MemberName);
            Assert.Equal("", entry.MemberType);
            Assert.Equal("string", entry.ReturnType);
            Assert.Equal("string id", entry.Parameters);
            Assert.Equal("", entry.Body);
        }
    }
}
