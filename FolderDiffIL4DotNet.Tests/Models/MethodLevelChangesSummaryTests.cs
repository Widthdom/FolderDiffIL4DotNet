using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed class MethodLevelChangesSummaryTests
    {
        [Fact]
        public void HasChanges_DefaultInstance_ReturnsFalse()
        {
            var summary = new MethodLevelChangesSummary();
            Assert.False(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithAddedMethods_ReturnsTrue()
        {
            var summary = new MethodLevelChangesSummary
            {
                AddedMethods = new List<string> { "[public] Foo::Bar() : void" },
            };
            Assert.True(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithRemovedTypes_ReturnsTrue()
        {
            var summary = new MethodLevelChangesSummary
            {
                RemovedTypes = new List<string> { "MyApp.OldService" },
            };
            Assert.True(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithBodyChangedMethods_ReturnsTrue()
        {
            var summary = new MethodLevelChangesSummary
            {
                BodyChangedMethods = new List<string> { "[public] Foo::Run() : void" },
            };
            Assert.True(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithAddedProperties_ReturnsTrue()
        {
            var summary = new MethodLevelChangesSummary
            {
                AddedProperties = new List<string> { "Foo::Name" },
            };
            Assert.True(summary.HasChanges);
        }

        [Fact]
        public void HasChanges_WithRemovedFields_ReturnsTrue()
        {
            var summary = new MethodLevelChangesSummary
            {
                RemovedFields = new List<string> { "Foo::_bar" },
            };
            Assert.True(summary.HasChanges);
        }
    }
}
