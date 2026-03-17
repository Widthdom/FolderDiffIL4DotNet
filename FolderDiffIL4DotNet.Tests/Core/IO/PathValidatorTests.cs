using System;
using System.Runtime.InteropServices;
using FolderDiffIL4DotNet.Core.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.IO
{
    public class PathValidatorTests
    {

        [Theory]
        [InlineData("MyFolder")]
        [InlineData("release-v1.2.3")]
        [InlineData("2024-01-01_build")]
        [InlineData("a")]
        public void ValidateFolderNameOrThrow_ValidNames_DoesNotThrow(string name)
        {
            PathValidator.ValidateFolderNameOrThrow(name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateFolderNameOrThrow_NullOrEmpty_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void ValidateFolderNameOrThrow_DotNames_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("my\\folder")]
        [InlineData("my/folder")]
        [InlineData("my:folder")]
        [InlineData("my*folder")]
        [InlineData("my?folder")]
        [InlineData("my\"folder")]
        [InlineData("my<folder")]
        [InlineData("my>folder")]
        [InlineData("my|folder")]
        public void ValidateFolderNameOrThrow_InvalidChars_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("folder ")]
        [InlineData("folder.")]
        public void ValidateFolderNameOrThrow_TrailingSpaceOrDot_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("con")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT1")]
        public void ValidateFolderNameOrThrow_WindowsReservedNames_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Fact]
        public void ValidateFolderNameOrThrow_ControlChar_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow("my\x01folder"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateAbsolutePathLengthOrThrow_NullOrEmpty_ThrowsArgumentException(string path)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow(path));
        }

        [Fact]
        public void ValidateAbsolutePathLengthOrThrow_NormalPath_DoesNotThrow()
        {
            PathValidator.ValidateAbsolutePathLengthOrThrow("/usr/local/bin/test");
        }

        [Fact]
        public void ValidateAbsolutePathLengthOrThrow_ExceedsLimit_ThrowsArgumentException()
        {
            int limit;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                limit = 260;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                limit = 1024;
            else
                limit = 4096;

            var tooLongPath = "/" + new string('a', limit + 1);
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow(tooLongPath));
        }

        // -----------------------------------------------------------------------
        // Path traversal prevention
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("../etc/passwd")]          // forward slash already invalid
        [InlineData("..\\windows\\system32")] // backslash already invalid
        [InlineData("../../root")]
        [InlineData("a/../../b")]
        public void ValidateFolderNameOrThrow_PathTraversalAttempts_ThrowsArgumentException(string name)
        {
            // Path traversal via report label is blocked because '/', '\', and ".." components
            // are all rejected by the character or dot-name checks.
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("foo\0bar")]   // null byte - control char
        [InlineData("\0")]         // lone null byte
        public void ValidateFolderNameOrThrow_NullByteInName_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("/etc/passwd")]      // starts with / (absolute path)
        [InlineData("/tmp/evil")]
        public void ValidateFolderNameOrThrow_AbsolutePathAsLabel_ThrowsArgumentException(string name)
        {
            // An absolute-path string used as a report label would escape the Reports/ directory.
            // The leading '/' (or '\') is caught by the invalid-character check.
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }
    }
}
