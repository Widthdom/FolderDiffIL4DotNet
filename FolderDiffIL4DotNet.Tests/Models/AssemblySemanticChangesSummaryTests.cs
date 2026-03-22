using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    /// <summary>
    /// Tests for <see cref="AssemblySemanticChangesSummary"/> and <see cref="MemberChangeEntry"/> model classes.
    /// <see cref="AssemblySemanticChangesSummary"/> および <see cref="MemberChangeEntry"/> モデルクラスのテスト。
    /// </summary>
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
                    new("Added", "MyApp.Service", "", "public", "", "Method", "DoWork", "", "void", "int count", ""),
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
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "SomeBase", "public", "static", "Method", "GetName", "", "string", "string id", "");
            Assert.Equal("Added", entry.Change);
            Assert.Equal("MyApp.Service", entry.TypeName);
            Assert.Equal("SomeBase", entry.BaseType);
            Assert.Equal("public", entry.Access);
            Assert.Equal("static", entry.Modifiers);
            Assert.Equal("Method", entry.MemberKind);
            Assert.Equal("GetName", entry.MemberName);
            Assert.Equal("", entry.MemberType);
            Assert.Equal("string", entry.ReturnType);
            Assert.Equal("string id", entry.Parameters);
            Assert.Equal("", entry.Body);
            Assert.Equal(ChangeImportance.Low, entry.Importance);
        }

        [Fact]
        public void Entries_ImportanceCanBeSetExplicitly()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "public", "", "Method", "Execute", "", "void", "", "", ChangeImportance.High);
            Assert.Equal(ChangeImportance.High, entry.Importance);
        }

        [Fact]
        public void ImportanceCounts_ReturnCorrectValues()
        {
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "MyApp.Service", "", "public", "", "Method", "A", "", "void", "", "", ChangeImportance.High),
                    new("Removed", "MyApp.Service", "", "public", "", "Method", "B", "", "void", "", "", ChangeImportance.High),
                    new("Added", "MyApp.Service", "", "public", "", "Method", "C", "", "void", "", "", ChangeImportance.Medium),
                    new("Modified", "MyApp.Service", "", "public", "", "Method", "D", "", "void", "", "Changed", ChangeImportance.Low),
                },
            };
            Assert.Equal(2, summary.HighImportanceCount);
            Assert.Equal(1, summary.MediumImportanceCount);
            Assert.Equal(1, summary.LowImportanceCount);
        }

        [Fact]
        public void MaxImportance_ReturnsHighest()
        {
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Service", "", "public", "", "Method", "A", "", "void", "", "Changed", ChangeImportance.Low),
                    new("Added", "MyApp.Service", "", "public", "", "Method", "B", "", "void", "", "", ChangeImportance.Medium),
                },
            };
            Assert.Equal(ChangeImportance.Medium, summary.MaxImportance);
        }

        [Fact]
        public void MaxImportance_EmptyEntries_ReturnsLow()
        {
            var summary = new AssemblySemanticChangesSummary();
            Assert.Equal(ChangeImportance.Low, summary.MaxImportance);
        }

        [Fact]
        public void EntriesByImportance_SortsByChangeThenImportance()
        {
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Service", "", "public", "", "Method", "Low1", "", "void", "", "Changed", ChangeImportance.Low),
                    new("Removed", "MyApp.Service", "", "public", "", "Method", "High1", "", "void", "", "", ChangeImportance.High),
                    new("Added", "MyApp.Service", "", "public", "", "Method", "Med1", "", "void", "", "", ChangeImportance.Medium),
                    new("Added", "MyApp.Service", "", "public", "", "Method", "High2", "", "void", "", "", ChangeImportance.High),
                },
            };
            var sorted = summary.EntriesByImportance;
            // Added first (sorted by importance desc within Added)
            Assert.Equal("Added", sorted[0].Change);
            Assert.Equal(ChangeImportance.High, sorted[0].Importance);
            Assert.Equal("Added", sorted[1].Change);
            Assert.Equal(ChangeImportance.Medium, sorted[1].Importance);
            // Then Removed
            Assert.Equal("Removed", sorted[2].Change);
            Assert.Equal(ChangeImportance.High, sorted[2].Importance);
            // Then Modified
            Assert.Equal("Modified", sorted[3].Change);
            Assert.Equal(ChangeImportance.Low, sorted[3].Importance);
        }
    }
}
