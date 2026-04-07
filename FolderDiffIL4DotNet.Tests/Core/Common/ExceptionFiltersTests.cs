using System;
using System.ComponentModel;
using System.IO;
using FolderDiffIL4DotNet.Core.Common;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Common
{
    /// <summary>
    /// Unit tests for <see cref="ExceptionFilters"/> predicate methods.
    /// <see cref="ExceptionFilters"/> のフィルタ述語メソッドのユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ExceptionFiltersTests
    {
        // ── IsFileIoRecoverable ──

        [Fact]
        public void IsFileIoRecoverable_IOException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoRecoverable(new IOException()));
        }

        [Fact]
        public void IsFileIoRecoverable_UnauthorizedAccessException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoRecoverable(new UnauthorizedAccessException()));
        }

        [Fact]
        public void IsFileIoRecoverable_NotSupportedException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoRecoverable(new NotSupportedException()));
        }

        [Fact]
        public void IsFileIoRecoverable_InvalidOperationException_ReturnsFalse()
        {
            // InvalidOperationException is NOT in this filter / このフィルタに含まれない
            Assert.False(ExceptionFilters.IsFileIoRecoverable(new InvalidOperationException()));
        }

        [Fact]
        public void IsFileIoRecoverable_NullReferenceException_ReturnsFalse()
        {
            Assert.False(ExceptionFilters.IsFileIoRecoverable(new NullReferenceException()));
        }

        [Fact]
        public void IsFileIoRecoverable_ArgumentException_ReturnsFalse()
        {
            Assert.False(ExceptionFilters.IsFileIoRecoverable(new ArgumentException()));
        }

        [Fact]
        public void IsFileIoRecoverable_FileNotFoundException_ReturnsTrue()
        {
            // FileNotFoundException is a subclass of IOException / FileNotFoundException は IOException のサブクラス
            Assert.True(ExceptionFilters.IsFileIoRecoverable(new FileNotFoundException()));
        }

        // ── IsFileIoOrOperationRecoverable ──

        [Fact]
        public void IsFileIoOrOperationRecoverable_IOException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoOrOperationRecoverable(new IOException()));
        }

        [Fact]
        public void IsFileIoOrOperationRecoverable_InvalidOperationException_ReturnsTrue()
        {
            // This filter includes InvalidOperationException / このフィルタには含まれる
            Assert.True(ExceptionFilters.IsFileIoOrOperationRecoverable(new InvalidOperationException()));
        }

        [Fact]
        public void IsFileIoOrOperationRecoverable_UnauthorizedAccessException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoOrOperationRecoverable(new UnauthorizedAccessException()));
        }

        [Fact]
        public void IsFileIoOrOperationRecoverable_NotSupportedException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsFileIoOrOperationRecoverable(new NotSupportedException()));
        }

        [Fact]
        public void IsFileIoOrOperationRecoverable_NullReferenceException_ReturnsFalse()
        {
            Assert.False(ExceptionFilters.IsFileIoOrOperationRecoverable(new NullReferenceException()));
        }

        // ── IsPathOrFileIoRecoverable ──

        [Fact]
        public void IsPathOrFileIoRecoverable_ArgumentException_ReturnsTrue()
        {
            // This filter includes ArgumentException / このフィルタには含まれる
            Assert.True(ExceptionFilters.IsPathOrFileIoRecoverable(new ArgumentException()));
        }

        [Fact]
        public void IsPathOrFileIoRecoverable_ArgumentNullException_ReturnsTrue()
        {
            // Subclass of ArgumentException / ArgumentException のサブクラス
            Assert.True(ExceptionFilters.IsPathOrFileIoRecoverable(new ArgumentNullException()));
        }

        [Fact]
        public void IsPathOrFileIoRecoverable_IOException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsPathOrFileIoRecoverable(new IOException()));
        }

        [Fact]
        public void IsPathOrFileIoRecoverable_UnauthorizedAccessException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsPathOrFileIoRecoverable(new UnauthorizedAccessException()));
        }

        [Fact]
        public void IsPathOrFileIoRecoverable_InvalidOperationException_ReturnsFalse()
        {
            // Not in this filter / このフィルタに含まれない
            Assert.False(ExceptionFilters.IsPathOrFileIoRecoverable(new InvalidOperationException()));
        }

        // ── IsProcessExecutionRecoverable ──

        [Fact]
        public void IsProcessExecutionRecoverable_Win32Exception_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsProcessExecutionRecoverable(new Win32Exception()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_InvalidOperationException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsProcessExecutionRecoverable(new InvalidOperationException()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_IOException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsProcessExecutionRecoverable(new IOException()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_NotSupportedException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsProcessExecutionRecoverable(new NotSupportedException()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_UnauthorizedAccessException_ReturnsTrue()
        {
            Assert.True(ExceptionFilters.IsProcessExecutionRecoverable(new UnauthorizedAccessException()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_NullReferenceException_ReturnsFalse()
        {
            Assert.False(ExceptionFilters.IsProcessExecutionRecoverable(new NullReferenceException()));
        }

        [Fact]
        public void IsProcessExecutionRecoverable_ArgumentException_ReturnsFalse()
        {
            // ArgumentException is NOT in this filter / このフィルタに含まれない
            Assert.False(ExceptionFilters.IsProcessExecutionRecoverable(new ArgumentException()));
        }
    }
}
