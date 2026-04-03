using System;
using FolderDiffIL4DotNet.Core.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Security
{
    /// <summary>
    /// Security tests verifying path traversal prevention in PathValidator.
    /// Ensures that directory separators, relative escapes, control characters,
    /// and Windows reserved names are all rejected.
    /// PathValidator におけるパストラバーサル防止のセキュリティテスト。
    /// ディレクトリ区切り文字、相対パスエスケープ、制御文字、Windows 予約名の
    /// すべてが拒否されることを検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PathTraversalSecurityTests
    {
        [Theory]
        [InlineData("../")]
        [InlineData("..\\")]
        [InlineData("../etc/passwd")]
        [InlineData("..\\windows\\system32")]
        [InlineData("foo/../../../etc/shadow")]
        public void FolderName_RelativePathEscapes_Rejected(string name)
        {
            // Directory separators in folder names enable path traversal
            // フォルダ名中のディレクトリ区切り文字はパストラバーサルを可能にする
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void FolderName_DotAndDotDot_Rejected(string name)
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("\0malicious")]
        [InlineData("path\0inject")]
        [InlineData("\x01evil")]
        [InlineData("test\x1Fname")]
        public void FolderName_NullByteAndControlChars_Rejected(string name)
        {
            // Null bytes and control characters can truncate or confuse path operations
            // NULL バイトや制御文字はパス操作を切り詰めたり混乱させる可能性がある
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT1")]
        [InlineData("con")]
        [InlineData("nul")]
        public void FolderName_WindowsReservedNames_Rejected(string name)
        {
            // Windows reserved device names cause unexpected behavior on Windows
            // Windows 予約デバイス名は Windows で予期しない動作を引き起こす
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("folder ")]
        [InlineData("folder.")]
        public void FolderName_TrailingSpaceOrDot_Rejected(string name)
        {
            // Windows silently strips trailing dots/spaces, causing name collisions
            // Windows は末尾のドットやスペースを黙って除去し、名前衝突を引き起こす
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("/etc/passwd")]
        [InlineData("C:\\Windows\\System32")]
        [InlineData("\\\\server\\share")]
        public void FolderName_AbsolutePaths_Rejected(string name)
        {
            // Absolute paths contain separators that should be rejected
            // 絶対パスには拒否されるべき区切り文字が含まれる
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateFolderNameOrThrow(name));
        }

        [Fact]
        public void FolderName_ValidUnicodeName_Accepted()
        {
            // Japanese/CJK folder names should be accepted
            // 日本語/CJK フォルダ名は受け入れられること
            var ex = Record.Exception(() => PathValidator.ValidateFolderNameOrThrow("テスト_ビルド_2026"));
            Assert.Null(ex);
        }

        [Fact]
        public void PathLength_ExtremelyLong_Rejected()
        {
            // Paths exceeding OS limits should be rejected
            // OS 上限を超えるパスは拒否されること
            var longPath = "/" + new string('a', 5000);
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow(longPath));
        }

        [Fact]
        public void PathLength_NullOrEmpty_Rejected()
        {
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow(null!));
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow(""));
            Assert.Throws<ArgumentException>(() => PathValidator.ValidateAbsolutePathLengthOrThrow("   "));
        }
    }
}
